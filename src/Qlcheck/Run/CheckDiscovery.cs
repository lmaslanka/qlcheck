// Copyright (c) qlcheck contributors.
using Qlcheck.Checks;
using Qlcheck.Languages.CSharp.Catalog;
using Qlcheck.Scan;

namespace Qlcheck.Run;

public static class CheckDiscovery
{
    private const string UnknownPrefix = "Unknown check: ";

    private const string NoImplementation = "has no implementation.";

    public static IReadOnlyList<ICheck> All() =>
        Catalog.Checks.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();

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
        return RejectUnimplemented(checks, stderr);
    }

    // Every id in a .qlcheck_disable file must name an implemented check, so a typo cannot
    // silently disable nothing.
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

    private static string? DisabledError(IReadOnlyDictionary<string, ICheck> known, string id)
    {
        if (!known.TryGetValue(id, out var check))
        {
            return $"{UnknownPrefix}{id}";
        }

        return check is CatalogCheck && !CatalogWalker.IsImplemented(id) ? $"Check '{id}' {NoImplementation}" : null;
    }

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
        return RejectUnimplemented(checks, stderr);
    }

    private static bool RejectUnimplemented(IReadOnlyList<ICheck> checks, TextWriter stderr)
    {
        var missing = checks.OfType<CatalogCheck>().Where(check => !CatalogWalker.IsImplemented(check.Id)).ToList();
        if (missing.Count == 0)
        {
            return true;
        }

        foreach (var check in missing)
        {
            stderr.WriteLine($"Check '{check.Id}' {NoImplementation}");
        }

        return false;
    }
}
