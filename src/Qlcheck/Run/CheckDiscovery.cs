// Copyright (c) qlcheck contributors.
using Qlcheck.Checks;
using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Run;

public static class CheckDiscovery
{
    private const string UnknownPrefix = "Unknown check: ";

    private const string NoImplementation = "has no implementation.";

    private static readonly Type[] CheckTypes =
        [.. typeof(ICheck).Assembly.GetTypes().Where(IsHouseCheck)];

    public static IReadOnlyList<ICheck> All()
    {
        var house = CheckTypes.Select(t => (ICheck)Activator.CreateInstance(t)!);
        return house.Concat(Catalog.Checks).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
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
        return RejectUnimplemented(checks, stderr);
    }

    private static bool IsHouseCheck(Type type) =>
        type is { IsClass: true, IsAbstract: false }
        && typeof(ICheck).IsAssignableFrom(type)
        && type.GetConstructor(Type.EmptyTypes) is not null;

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
