// Copyright (c) qlcheck contributors.
using System.Text.RegularExpressions;
using Qlcheck.Scan;

namespace Qlcheck.Run;

internal static partial class Suppressions
{
    private const int SuppressionDays = 365;

    private const int SameLineOffset = 1;

    private const int LineAboveOffset = 2;

    private const string RuleGroup = "rule";

    private const string DateGroup = "date";

    public static IReadOnlyList<Finding> Filter(
        IReadOnlyList<Finding> findings,
        IReadOnlyList<SourceScan.LoadedSource> loaded)
    {
        if (findings.Count == 0)
        {
            return findings;
        }

        var textByFile = loaded.ToDictionary(
            source => source.File.Path,
            source => source.File.Text,
            StringComparer.Ordinal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return findings.Where(finding => !IsSuppressed(finding, textByFile, today)).ToList();
    }

    private static bool IsSuppressed(Finding finding, IReadOnlyDictionary<string, string> textByFile, DateOnly today)
    {
        if (!textByFile.TryGetValue(finding.File, out var text))
        {
            return false;
        }

        var lines = text.Split('\n');
        return IsSuppressedOnLine(finding.Check, lines, finding.Line - SameLineOffset, today)
            || IsSuppressedOnLine(finding.Check, lines, finding.Line - LineAboveOffset, today);
    }

    private static bool IsSuppressedOnLine(string checkId, string[] lines, int index, DateOnly today)
    {
        if (index < 0 || index >= lines.Length)
        {
            return false;
        }

        var match = Pattern().Match(lines[index]);
        if (!match.Success
            || !string.Equals(match.Groups[RuleGroup].Value, checkId, StringComparison.OrdinalIgnoreCase)
            || !DateOnly.TryParse(match.Groups[DateGroup].Value, out var checkedOn))
        {
            return false;
        }

        return today.DayNumber - checkedOn.DayNumber <= SuppressionDays;
    }

    [GeneratedRegex(
        @"qlcheck-ignore:\s*(?<rule>[a-z0-9-]+)\s+checked-on:\s*(?<date>\d{4}-\d{2}-\d{2})",
        RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();
}
