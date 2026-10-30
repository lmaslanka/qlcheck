namespace Qlcheck.Tests;

// A shrink-only list of check ids exempt from a fixture guard, in the spirit of the architecture
// ratchet (ADR 0009): an id that newly fails the guard fails the test, and so does a listed id that
// now passes, so the file can only shrink. Lines starting with '#' are comments. Compare returns
// what is wrong, or an empty string when the ids match the file.
internal static class FixtureBaseline
{
    public static string Compare(string guard, string path, IEnumerable<string> failingIds)
    {
        var actual = failingIds.ToHashSet(StringComparer.Ordinal);
        var baseline = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);
        var added = actual.Except(baseline).Order(StringComparer.Ordinal).ToList();
        var fixedIds = baseline.Except(actual).Order(StringComparer.Ordinal).ToList();
        if (added.Count == 0 && fixedIds.Count == 0)
        {
            return string.Empty;
        }

        var message = $"{guard} failed.";
        if (added.Count > 0)
        {
            message += $"\n{added.Count} new id(s) fail it; fix their fixtures, do not baseline them:\n  {string.Join("\n  ", added)}";
        }

        if (fixedIds.Count > 0)
        {
            message += $"\n{fixedIds.Count} baselined id(s) now pass; delete them from {Path.GetFileName(path)}:\n  {string.Join("\n  ", fixedIds)}";
        }

        return message;
    }
}
