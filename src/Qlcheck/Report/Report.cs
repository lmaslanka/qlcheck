using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qlcheck.Checks;
using Qlcheck.Scan;

namespace Qlcheck.Report;

internal static class Report
{
    private const int MinInnerWidth = 46;

    private const int BoxPadding = 2;

    private const string NumberFormat = "N0";

    private const string Title = "QLCHECK";

    private const string ScanHeading = "Scan";

    private const string ResultsHeading = "Results";

    private const string DisabledHeading = "Disabled";

    private const string ArchitectureHeading = "Architecture";

    private const string ConstraintNoun = "constraint";

    private const string FileNoun = "file";

    private const string DurationLabel = "Duration";

    private const string SecondsFormat = "0.00";

    private static readonly string NewlineText = '\n'.ToString();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Write(
        Qlcheck.Cli.Cli.Options options,
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        IReadOnlyList<ICheck> checks,
        RunResult run,
        IReadOnlyList<DisableFiles.Entry> disabled,
        TimeSpan duration,
        TextWriter stdout)
    {
        var findings = run.Findings;
        var architecture = run.Architecture ?? [];
        if (options.Human)
        {
            WriteHuman(findings, stdout);
        }

        var jsonOnly = !options.Human && !options.Stats;
        if (jsonOnly)
        {
            var payload = new Payload(findings, DisabledJson(disabled), ToJson(run.Coverage, loaded), ArchitectureJson(architecture));
            stdout.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        }

        if (!options.Stats)
        {
            return;
        }

        if (options.Human && findings.Count > 0)
        {
            stdout.WriteLine();
        }

        WriteStats(loaded.Select(l => l.File).ToList(), checks, findings, disabled, architecture, duration, stdout);
    }

    private static void WriteHuman(IReadOnlyList<Finding> findings, TextWriter stdout)
    {
        foreach (var finding in findings)
        {
            stdout.WriteLine($"{finding.File}:{finding.Line}:{finding.Column}  {finding.Check}");
            stdout.WriteLine($"  {finding.Message}");
            if (finding.Replacement is not null)
            {
                stdout.WriteLine();
                foreach (var line in finding.Replacement.ReplaceLineEndings(NewlineText).Split('\n'))
                {
                    stdout.WriteLine($"  {line}");
                }
            }

            stdout.WriteLine();
        }
    }

    private static void WriteStats(
        IReadOnlyList<SourceFile> files,
        IReadOnlyList<ICheck> checks,
        IReadOnlyList<Finding> findings,
        IReadOnlyList<DisableFiles.Entry> disabled,
        IReadOnlyList<ArchitectureFile> architecture,
        TimeSpan duration,
        TextWriter stdout)
    {
        var filesWithFindings = findings.Select(f => f.File).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var lines = 0;
        foreach (var file in files)
        {
            lines += LineCount(file);
        }
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

        var inner = MinInnerWidth;
        foreach (var row in byFile)
        {
            var countText = row.Count.ToString(NumberFormat, CultureInfo.InvariantCulture);
            inner = Math.Max(inner, BoxPadding + row.File.Length + 1 + countText.Length + BoxPadding);
        }
        stdout.WriteLine($"╭{new string('─', inner)}╮");
        stdout.WriteLine(Box(Title, inner));
        stdout.WriteLine($"├{new string('─', inner)}┤");
        stdout.WriteLine(Box(ScanHeading, inner));
        stdout.WriteLine(BoxRow("  Files checked", files.Count, inner));
        stdout.WriteLine(BoxRow("  Lines checked", lines, inner));
        stdout.WriteLine(BoxRow("  Checks run", checks.Count, inner));
        stdout.WriteLine(Box(string.Empty, inner));
        WriteDisabled(disabled, inner, stdout);
        WriteArchitecture(architecture, inner, stdout);
        stdout.WriteLine(Box(ResultsHeading, inner));
        stdout.WriteLine(BoxRow("  Findings", findings.Count, inner));
        stdout.WriteLine(BoxRow("  Files with findings", filesWithFindings, inner));
        stdout.WriteLine(BoxRow("  Clean files", files.Count - filesWithFindings, inner));
        stdout.WriteLine(Box(string.Empty, inner));
        WriteBreakdown("By check", byCheck.Select(row => (Label: $"  {row.Check}", Value: $"{row.Count}  ({Plural(row.Files, FileNoun)})")), inner, stdout);
        stdout.WriteLine(Box(string.Empty, inner));
        WriteBreakdown("By file", byFile.Select(row => (Label: $"  {row.File}", Value: row.Count.ToString(NumberFormat, CultureInfo.InvariantCulture))), inner, stdout);
        stdout.WriteLine(Box(string.Empty, inner));
        stdout.WriteLine(BoxRow(DurationLabel, FormatDuration(duration), inner));
        stdout.WriteLine($"╰{new string('─', inner)}╯");
    }

    private static void WriteBreakdown(string heading, IEnumerable<(string Label, string Value)> rows, int inner, TextWriter stdout)
    {
        stdout.WriteLine(Box(heading, inner));
        var any = false;
        foreach (var (label, value) in rows)
        {
            stdout.WriteLine(BoxRow(label, value, inner));
            any = true;
        }

        if (!any)
        {
            stdout.WriteLine(Box("  (none)", inner));
        }
    }

    private static void WriteDisabled(IReadOnlyList<DisableFiles.Entry> disabled, int inner, TextWriter stdout)
    {
        if (disabled.Count == 0)
        {
            return;
        }

        stdout.WriteLine(Box(DisabledHeading, inner));
        foreach (var entry in disabled)
        {
            stdout.WriteLine(BoxRow($"  {entry.Check}", entry.Under, inner));
        }

        stdout.WriteLine(Box(string.Empty, inner));
    }

    // The architecture files that applied, so a clean run can't be mistaken for an unchecked one.
    private static void WriteArchitecture(IReadOnlyList<ArchitectureFile> architecture, int inner, TextWriter stdout)
    {
        if (architecture.Count == 0)
        {
            return;
        }

        stdout.WriteLine(Box(ArchitectureHeading, inner));
        foreach (var file in architecture)
        {
            stdout.WriteLine(BoxRow($"  {DisplayPath(file.Path)}", Plural(file.Constraints.Count, ConstraintNoun), inner));
        }

        stdout.WriteLine(Box(string.Empty, inner));
    }

    private static IReadOnlyList<ArchitecturePayload>? ArchitectureJson(IReadOnlyList<ArchitectureFile> architecture) =>
        architecture.Count == 0 ? null : architecture.Select(file => new ArchitecturePayload(DisplayPath(file.Path), file.Constraints.Count)).ToList();

    // Relative to the working folder when it is below it, like Finding paths.
    private static string DisplayPath(string path)
    {
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), path).Replace('\\', '/');
        return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative;
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
        if (text.Length > inner - BoxPadding)
        {
            text = Truncate(text, inner - BoxPadding);
        }

        return $"│ {text.PadRight(inner - BoxPadding)} │";
    }

    private static string BoxRow(string label, int value, int inner) =>
        BoxRow(label, value.ToString(NumberFormat, CultureInfo.InvariantCulture), inner);

    private static string BoxRow(string label, string value, int inner)
    {
        var width = inner - BoxPadding;
        var space = width - label.Length - value.Length;
        if (space < 1)
        {
            label = Truncate(label, width - value.Length - 1);
            space = 1;
        }

        return $"│ {label}{new string(' ', space)}{value} │";
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        return max <= 1 ? text[..max] : $"…{text[^(max - 1)..]}";
    }

    private static string Plural(int count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{count.ToString(NumberFormat, CultureInfo.InvariantCulture)} {noun}s";

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds >= 1)
        {
            return $"{duration.TotalSeconds.ToString(SecondsFormat, CultureInfo.InvariantCulture)} s";
        }

        return $"{Math.Max(0, (int)duration.TotalMilliseconds)} ms";
    }

    private static IReadOnlyList<DisabledPayload>? DisabledJson(IReadOnlyList<DisableFiles.Entry> disabled) =>
        disabled.Count == 0 ? null : disabled.Select(entry => new DisabledPayload(entry.Check, entry.Under)).ToList();

    private static IReadOnlyList<FilePayload>? ToJson(
        IReadOnlyList<CoverageFile> coverage,
        IReadOnlyList<SourceScan.LoadedSource> loaded)
    {
        if (coverage.Count == 0)
        {
            return null;
        }

        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in loaded)
        {
            sources[source.File.Path] = source.File.Text;
        }

        var files = new List<FilePayload>(coverage.Count);
        foreach (var file in coverage)
        {
            sources.TryGetValue(file.File, out var text);
            files.Add(new FilePayload(file.File, file.Missing ? true : null, MethodPayloads(file, text)));
        }

        return files;
    }

    private static IReadOnlyList<MethodPayload>? MethodPayloads(CoverageFile file, string? text)
    {
        if (file.Methods.Count == 0)
        {
            return null;
        }

        var methods = new List<MethodPayload>(file.Methods.Count);
        foreach (var method in file.Methods)
        {
            var name = string.IsNullOrEmpty(method.Name) ? null : method.Name;
            methods.Add(new MethodPayload(name, MethodText(text, method.Lines)));
        }

        return methods;
    }

    private static string MethodText(string? text, IReadOnlyList<int> lines) =>
        string.Join('\n', lines.Select(line => $"{line.ToString(CultureInfo.InvariantCulture)}|{SourceLine(text, line)}"));

    private static string SourceLine(string? text, int line)
    {
        if (string.IsNullOrEmpty(text) || line < 1)
        {
            return string.Empty;
        }

        var current = 1;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
            {
                continue;
            }

            if (current == line)
            {
                return StripCr(text[start..i]);
            }

            current++;
            start = i + 1;
        }

        return current == line ? StripCr(text[start..]) : string.Empty;
    }

    private static string StripCr(string value) =>
        value.EndsWith('\r') ? value[..^1] : value;

    private sealed record Payload(
        IReadOnlyList<Finding> Findings,
        IReadOnlyList<DisabledPayload>? Disabled,
        IReadOnlyList<FilePayload>? Coverage,
        IReadOnlyList<ArchitecturePayload>? Architecture);

    private sealed record ArchitecturePayload(string File, int Constraints);

    private sealed record DisabledPayload(string Check, string Under);

    private sealed record FilePayload(
        string File,
        bool? Missing,
        IReadOnlyList<MethodPayload>? Methods);

    private sealed record MethodPayload(string? Name, string Text);
}
