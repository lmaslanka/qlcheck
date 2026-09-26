using Qlcheck.Languages;

namespace Qlcheck.Run;

internal static class LanguageDiscovery
{
    private static readonly Type[] LanguageTypes =
        [.. typeof(ILanguage).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        typeof(ILanguage).IsAssignableFrom(t))];

    public static IReadOnlyList<ILanguage> All()
    {
        var languages = LanguageTypes
            .Select(t => (ILanguage)Activator.CreateInstance(t)!)
            .OrderBy(language => language.Id, StringComparer.Ordinal)
            .ToList();
        var duplicate = languages
            .GroupBy(language => language.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate language '{duplicate.Key}'.");
        }

        return languages;
    }
}
