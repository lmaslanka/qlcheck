using Qlcheck.Secrets;

namespace Qlcheck.Tests;

public class SecretFormatsTests
{
    public static TheoryData<string, string> Tokens() => new()
    {
        { FakeSecrets.Aws, "AWS access key" },
        { FakeSecrets.GitHub, "GitHub token" },
        { FakeSecrets.GitHubFineGrained, "GitHub token" },
        { FakeSecrets.SlackToken, "Slack token" },
        { FakeSecrets.SlackWebhook, "Slack webhook URL" },
        { FakeSecrets.StripeLive, "Stripe live key" },
        { FakeSecrets.Google, "Google API key" },
        { FakeSecrets.PrivateKey, "private key" },
        { FakeSecrets.Jwt, "JSON Web Token" },
        { FakeSecrets.AzureStorage, "Azure storage account key" },
        { FakeSecrets.Anthropic, "Anthropic API key" },
        { FakeSecrets.OpenAi, "OpenAI API key" },
    };

    [Theory]
    [MemberData(nameof(Tokens))]
    public void ProviderOf_names_the_provider_of_a_known_token(string token, string provider)
    {
        Assert.Equal(provider, SecretFormats.ProviderOf(token));
        Assert.Equal(provider, SecretFormats.ProviderOf($"prefix {token} suffix"));
    }

    [Theory]
    [InlineData("AKIA")]
    [InlineData("ghp_short")]
    [InlineData("-----BEGIN PUBLIC KEY-----")]
    [InlineData("JwtSecurityTokenHandler")]
    [InlineData("")]
    public void ProviderOf_ignores_prefixes_and_ordinary_text(string value)
    {
        Assert.Null(SecretFormats.ProviderOf(value));
    }

    [Fact]
    public void ProviderOf_ignores_a_stripe_test_key()
    {
        Assert.Null(SecretFormats.ProviderOf(FakeSecrets.StripeTest));
    }

    [Fact]
    public void ProviderOf_allows_a_documented_example_token()
    {
        Assert.Null(SecretFormats.ProviderOf(FakeSecrets.AwsDocumented));
    }
}
