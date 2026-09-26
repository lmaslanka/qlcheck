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

    private const string FileNoun = "file";

    private const string DurationLabel = "Duration";

    private const string SecondsFormat = "0.00";

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
        IReadOnlyList<Finding> findings,
        TimeSpan duration,
        TextWriter stdout)
    {
        if (options.Human)
        {
            WriteHuman(findings, stdout);
        }
        else if (!options.Stats)
        {
            stdout.WriteLine(JsonSerializer.Serialize(new Payload(findings), JsonOptions));
        }

        if (!options.Stats)
        {
            return;
        }

        if (options.Human && findings.Count > 0)
        {
            stdout.WriteLine();
        }

        WriteStats(loaded.Select(l => l.File).ToList(), checks, findings, duration, stdout);
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
                foreach (var line in finding.Replacement.ReplaceLineEndings("\n").Split('\n'))
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
        stdout.WriteLine(Box(ResultsHeading, inner));
        stdout.WriteLine(BoxRow("  Findings", findings.Count, inner));
        stdout.WriteLine(BoxRow("  Files with findings", filesWithFindings, inner));
        stdout.WriteLine(BoxRow("  Clean files", files.Count - filesWithFindings, inner));
        stdout.WriteLine(Box(string.Empty, inner));
        stdout.WriteLine(Box("By check", inner));
        if (byCheck.Count == 0)
        {
            stdout.WriteLine(Box("  (none)", inner));
        }
        else
        {
            foreach (var row in byCheck)
            {
                stdout.WriteLine(BoxRow($"  {row.Check}", $"{row.Count}  ({Plural(row.Files, FileNoun)})", inner));
            }
        }

        stdout.WriteLine(Box(string.Empty, inner));
        stdout.WriteLine(Box("By file", inner));
        if (byFile.Count == 0)
        {
            stdout.WriteLine(Box("  (none)", inner));
        }
        else
        {
            foreach (var row in byFile)
            {
                stdout.WriteLine(BoxRow($"  {row.File}", row.Count, inner));
            }
        }

        stdout.WriteLine(Box(string.Empty, inner));
        stdout.WriteLine(BoxRow(DurationLabel, FormatDuration(duration), inner));
        stdout.WriteLine($"╰{new string('─', inner)}╯");
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

    private sealed record Payload(IReadOnlyList<Finding> Findings);
}
