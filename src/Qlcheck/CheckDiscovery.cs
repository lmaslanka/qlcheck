namespace Qlcheck;

public static class CheckDiscovery
{
    private static readonly Type[] CheckTypes =
        [.. typeof(ICheck).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        (typeof(IFileCheck).IsAssignableFrom(t) ||
                         typeof(ICompilationCheck).IsAssignableFrom(t)))];

    public static IReadOnlyList<ICheck> All()
    {
        return CheckTypes
            .Select(t => (ICheck)Activator.CreateInstance(t)!)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
    }
}
