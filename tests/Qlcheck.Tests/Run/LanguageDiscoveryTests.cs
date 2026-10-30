using Qlcheck.Languages.Config;

namespace Qlcheck.Tests;

public class LanguageDiscoveryTests
{
    [Fact]
    public void Discovers_config_and_csharp()
    {
        var languages = LanguageDiscovery.All();
        Assert.Equal([ConfigLanguage.LanguageId, CSharpLanguage.LanguageId], languages.Select(language => language.Id));
    }
}
