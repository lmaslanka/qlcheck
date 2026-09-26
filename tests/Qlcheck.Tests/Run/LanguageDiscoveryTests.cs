namespace Qlcheck.Tests;

public class LanguageDiscoveryTests
{
    [Fact]
    public void Discovers_csharp()
    {
        var languages = LanguageDiscovery.All();
        Assert.Equal([CSharpLanguage.LanguageId], languages.Select(language => language.Id));
    }
}
