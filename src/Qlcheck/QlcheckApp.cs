using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qlcheck;

public static class QlcheckApp
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static ExitCode Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
    {
        if (!TryParse(args, stderr, out var options))
        {
            return ExitCode.Error;
        }

        var checks = CheckDiscovery.All();
        if (options.CheckIds.Count > 0)
        {
            var unknown = options.CheckIds.Where(id => checks.All(c => c.Id != id)).ToList();
            if (unknown.Count > 0)
            {
                stderr.WriteLine("Unknown check: " + string.Join(", ", unknown));
                return ExitCode.Error;
            }
        }

        if (!options.InlineSql && !options.CheckIds.Contains(InlineSqlCheck.CheckId))
        {
            checks = checks.Where(c => c.Id != InlineSqlCheck.CheckId).ToList();
        }

        if (options.CheckIds.Count > 0)
        {
            checks = checks.Where(c => options.CheckIds.Contains(c.Id)).ToList();
        }

        List<string> files;
        try
        {
            files = CollectFiles(options.Paths);
        }
        catch (Exception ex)
        {
            stderr.WriteLine(ex.Message);
            return ExitCode.Error;
        }

        var loaded = files
            .AsParallel()
            .AsOrdered()
            .Select(path => (Full: path, File: new SourceFile(DisplayPath(path), File.ReadAllText(path))))
            .ToList();
        var sourceFiles = loaded.Select(l => l.File).ToList();
        var timer = Stopwatch.StartNew();
        var findings = sourceFiles
            .AsParallel()
            .AsOrdered()
            .SelectMany(file =>
            {
                var tree = file.Tree;
                return checks.SelectMany(c => c.Analyze(file, tree));
            })
            .ToList();
        var included = sourceFiles.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var group in loaded.GroupBy(l => CSharpCompilations.FindCsproj(l.Full) ?? ""))
        {
            var name = string.IsNullOrEmpty(group.Key)
                ? "adhoc"
                : Path.GetFileNameWithoutExtension(group.Key);
            var compilation = CSharpCompilations.Create(name, group.Select(g => g.File.Tree));
            foreach (var check in checks)
            {
                findings.AddRange(check.AnalyzeCompilation(compilation, included));
            }
        }

        timer.Stop();

        if (options.Human)
        {
            WriteHuman(findings, stdout);
        }
        else if (!options.Stats)
        {
            stdout.WriteLine(JsonSerializer.Serialize(new Report(findings), JsonOptions));
        }

        if (options.Stats)
        {
            if (options.Human && findings.Count > 0)
            {
                stdout.WriteLine();
            }

            WriteStats(sourceFiles, checks, findings, timer.Elapsed, stdout);
        }

        return findings.Count == 0 ? ExitCode.Clean : ExitCode.Findings;
    }

    private static bool TryParse(IReadOnlyList<string> args, TextWriter stderr, out Options options)
    {
        var human = false;
        var stats = false;
        var inlineSql = false;
        var checkIds = new List<string>();
        var paths = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h")
            {
                stderr.WriteLine(Usage);
                options = null!;
                return false;
            }

            if (arg == "--human")
            {
                human = true;
                continue;
            }

            if (arg == "--stats")
            {
                stats = true;
                continue;
            }

            if (arg == "--inline-sql")
            {
                inlineSql = true;
                continue;
            }

            if (arg == "--check")
            {
                if (i + 1 >= args.Count)
                {
                    stderr.WriteLine("Missing value for --check.");
                    stderr.WriteLine(Usage);
                    options = null!;
                    return false;
                }

                checkIds.Add(args[++i]);
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                stderr.WriteLine("Unknown option: " + arg);
                stderr.WriteLine(Usage);
                options = null!;
                return false;
            }

            paths.Add(arg);
        }

        if (paths.Count == 0)
        {
            stderr.WriteLine(Usage);
            options = null!;
            return false;
        }

        options = new Options(human, stats, inlineSql, checkIds, paths);
        return true;
    }

    private static List<string> CollectFiles(IReadOnlyList<string> paths)
    {
        var files = new List<string>();
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(Path.GetFullPath(path));
                }

                continue;
            }

            if (Directory.Exists(path))
            {
                var root = Path.GetFullPath(path);
                CollectFromDirectory(root, IgnorePatterns.Load(root), files);
                continue;
            }

            throw new DirectoryNotFoundException("Path not found: " + path);
        }

        return files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.Ordinal).ToList();
    }

    private static void CollectFromDirectory(string root, IgnorePatterns ignore, List<string> files)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
            {
                var relative = Path.GetRelativePath(root, file);
                if (!ignore.IsIgnored(relative, isDirectory: false))
                {
                    files.Add(Path.GetFullPath(file));
                }
            }

            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var relative = Path.GetRelativePath(root, sub);
                if (!ignore.IsIgnored(relative, isDirectory: true))
                {
                    pending.Push(sub);
                }
            }
        }
    }

    private static string DisplayPath(string fullPath)
    {
        var cwd = Path.GetFullPath(Directory.GetCurrentDirectory());
        fullPath = Path.GetFullPath(fullPath);
        var prefix = cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(cwd, fullPath).Replace('\\', '/');
        }

        return fullPath.Replace('\\', '/');
    }

    private static void WriteHuman(IReadOnlyList<Finding> findings, TextWriter stdout)
    {
        foreach (var finding in findings)
        {
            stdout.WriteLine($"{finding.File}:{finding.Line}:{finding.Column}  {finding.Check}");
            stdout.WriteLine("  " + finding.Message);
            if (finding.Replacement is not null)
            {
                stdout.WriteLine();
                foreach (var line in finding.Replacement.ReplaceLineEndings("\n").Split('\n'))
                {
                    stdout.WriteLine("  " + line);
                }
            }

            stdout.WriteLine();
        }
    }

    private static void WriteStats(
        IReadOnlyList<SourceFile> files,
        IReadOnlyList<ICheck> checks,
        IReadOnlyList<Finding> findings,
        TimeSpan duration,
        TextWriter stdout)
    {
        var filesWithFindings = findings.Select(f => f.File).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var lines = files.Sum(LineCount);
        var byCheck = findings
            .GroupBy(f => f.Check, StringComparer.Ordinal)
            .Select(g => (
                Check: g.Key,
                Count: g.Count(),
                Files: g.Select(f => f.File).Distinct(StringComparer.OrdinalIgnoreCase).Count()))
            .OrderBy(g => g.Check, StringComparer.Ordinal)
            .ToList();
        var byFile = findings
            .GroupBy(f => f.File, StringComparer.OrdinalIgnoreCase)
            .Select(g => (File: g.Key, Count: g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.File, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var inner = 46;
        foreach (var row in byFile)
        {
            var countText = row.Count.ToString("N0", CultureInfo.InvariantCulture);
            inner = Math.Max(inner, 2 + row.File.Length + 1 + countText.Length + 2);
        }
        stdout.WriteLine("╭" + new string('─', inner) + "╮");
        stdout.WriteLine(Box("QLCHECK", inner));
        stdout.WriteLine("├" + new string('─', inner) + "┤");
        stdout.WriteLine(Box("Scan", inner));
        stdout.WriteLine(BoxRow("  Files checked", files.Count, inner));
        stdout.WriteLine(BoxRow("  Lines checked", lines, inner));
        stdout.WriteLine(BoxRow("  Checks run", checks.Count, inner));
        stdout.WriteLine(Box("", inner));
        stdout.WriteLine(Box("Results", inner));
        stdout.WriteLine(BoxRow("  Findings", findings.Count, inner));
        stdout.WriteLine(BoxRow("  Files with findings", filesWithFindings, inner));
        stdout.WriteLine(BoxRow("  Clean files", files.Count - filesWithFindings, inner));
        stdout.WriteLine(Box("", inner));
        stdout.WriteLine(Box("By check", inner));
        if (byCheck.Count == 0)
        {
            stdout.WriteLine(Box("  (none)", inner));
        }
        else
        {
            foreach (var row in byCheck)
            {
                stdout.WriteLine(BoxRow("  " + row.Check, $"{row.Count}  ({Plural(row.Files, "file")})", inner));
            }
        }

        stdout.WriteLine(Box("", inner));
        stdout.WriteLine(Box("By file", inner));
        if (byFile.Count == 0)
        {
            stdout.WriteLine(Box("  (none)", inner));
        }
        else
        {
            foreach (var row in byFile)
            {
                stdout.WriteLine(BoxRow("  " + row.File, row.Count, inner));
            }
        }

        stdout.WriteLine(Box("", inner));
        stdout.WriteLine(BoxRow("Duration", FormatDuration(duration), inner));
        stdout.WriteLine("╰" + new string('─', inner) + "╯");
    }

    private static int LineCount(SourceFile file)
    {
        var text = file.Text;
        if (text.Length == 0)
        {
            return 0;
        }

        var lines = 1;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                lines++;
            }
        }

        if (text[^1] == '\n')
        {
            lines--;
        }

        return lines;
    }

    private static string Box(string text, int inner)
    {
        if (text.Length > inner - 2)
        {
            text = Truncate(text, inner - 2);
        }

        return "│ " + text.PadRight(inner - 2) + " │";
    }

    private static string BoxRow(string label, int value, int inner) =>
        BoxRow(label, value.ToString("N0", CultureInfo.InvariantCulture), inner);

    private static string BoxRow(string label, string value, int inner)
    {
        var width = inner - 2;
        var space = width - label.Length - value.Length;
        if (space < 1)
        {
            label = Truncate(label, width - value.Length - 1);
            space = 1;
        }

        return "│ " + label + new string(' ', space) + value + " │";
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        return max <= 1 ? text[..max] : "…" + text[^(max - 1)..];
    }

    private static string Plural(int count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{count.ToString("N0", CultureInfo.InvariantCulture)} {noun}s";

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds >= 1)
        {
            return duration.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s";
        }

        return Math.Max(0, (int)duration.TotalMilliseconds) + " ms";
    }

    private const string Usage = "Usage: qlcheck [--human] [--stats] [--check <id>] [--inline-sql] <path>...";

    private sealed record Options(
        bool Human,
        bool Stats,
        bool InlineSql,
        IReadOnlyList<string> CheckIds,
        IReadOnlyList<string> Paths);

    private sealed record Report(IReadOnlyList<Finding> Findings);
}
