using Qlcheck.Checks;

namespace Qlcheck.Run;

public static class CheckDiscovery
{
    private static readonly Type[] CheckTypes =
        [.. typeof(ICheck).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        typeof(ICheck).IsAssignableFrom(t))];

    public static IReadOnlyList<ICheck> All()
    {
        return CheckTypes
            .Select(t => (ICheck)Activator.CreateInstance(t)!)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
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
            var unknown = checkIds.Where(id => all.All(c => c.Id != id)).ToList();
            if (unknown.Count > 0)
            {
                stderr.WriteLine($"Unknown check: {string.Join(", ", unknown)}");
                checks = [];
                return false;
            }

            checks = all.Where(c => checkIds.Contains(c.Id)).ToList();
            return true;
        }

        checks = all.Where(c => c.EnabledByDefault || enableIds.Contains(c.Id)).ToList();
        return true;
    }
}
