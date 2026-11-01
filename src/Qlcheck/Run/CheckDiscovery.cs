// Copyright (c) qlcheck contributors.
using Qlcheck.Checks;
using Qlcheck.Languages;
using Qlcheck.Scan;

namespace Qlcheck.Run;

public static class CheckDiscovery
{
    private const string UnknownPrefix = "Unknown check: ";

    public static IReadOnlyList<ICheck> All() => All(LanguageDiscovery.All());

    // Ids are globally unique across Languages (ADR 0005), so a `.qlcheck_disable` line or a
    // suppression comment names exactly one Check.
    internal static IReadOnlyList<ICheck> All(IReadOnlyList<ILanguage> languages)
    {
        var checks = languages.SelectMany(language => language.Checks).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var duplicate = checks.GroupBy(check => check.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate check '{duplicate.Key}'.");
        }

        return checks;
    }

    internal static bool TrySelect(
        IReadOnlyList<string> checkIds,
        IReadOnlyList<string> enableIds,
        out IReadOnlyList<ICheck> checks,
        TextWriter stderr)
    {
        var all = All();
        if (checkIds.Count > 0)
        {
            return SelectExplicit(all, checkIds, stderr, out checks);
        }

        checks = all.Where(c => c.EnabledByDefault || enableIds.Contains(c.Id)).ToList();
        return true;
    }

    // Every id in a .qlcheck_disable file must name a known check, so a typo cannot silently
    // disable nothing.
    internal static bool TryValidateDisabled(IReadOnlyList<DisableFiles.Entry> entries, TextWriter stderr)
    {
        var known = All().ToDictionary(check => check.Id, StringComparer.Ordinal);
        var errors = entries
            .Select(entry => (Entry: entry, Error: DisabledError(known, entry.Check)))
            .Where(item => item.Error is not null)
            .ToList();
        foreach (var (entry, error) in errors)
        {
            stderr.WriteLine($"{error} ({entry.File}:{entry.Line})");
        }

        return errors.Count == 0;
    }

    private static string? DisabledError(IReadOnlyDictionary<string, ICheck> known, string id) =>
        known.ContainsKey(id) ? null : $"{UnknownPrefix}{id}";

    private static bool SelectExplicit(
        IReadOnlyList<ICheck> all,
        IReadOnlyList<string> checkIds,
        TextWriter stderr,
        out IReadOnlyList<ICheck> checks)
    {
        var unknown = checkIds.Where(id => all.All(c => c.Id != id)).ToList();
        if (unknown.Count > 0)
        {
            stderr.WriteLine($"{UnknownPrefix}{string.Join(", ", unknown)}");
            checks = [];
            return false;
        }

        checks = all.Where(c => checkIds.Contains(c.Id)).ToList();
        return true;
    }
}
