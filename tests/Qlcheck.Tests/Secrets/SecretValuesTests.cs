using Qlcheck.Secrets;

namespace Qlcheck.Tests;

public class SecretValuesTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("changeme")]
    [InlineData("CHANGE-ME")]
    [InlineData("password")]
    [InlineData("xxxxxxxx")]
    [InlineData("********")]
    [InlineData("<your password>")]
    [InlineData("{password}")]
    [InlineData("${DB_PASSWORD}")]
    [InlineData("${{ secrets.TOKEN }}")]
    [InlineData("${PASSWORD:-local}")]
    [InlineData("$(Password)")]
    [InlineData("%PASSWORD%")]
    [InlineData("#{Password}#")]
    [InlineData("__PASSWORD__")]
    [InlineData("your-api-key-here")]
    [InlineData("@Microsoft.KeyVault(SecretUri=https://vault/secrets/db)")]
    [InlineData("not-a-real-FAKE-value")]
    public void IsPlaceholder_recognises_placeholders(string value)
    {
        Assert.True(SecretValues.IsPlaceholder(value));
    }

    [Theory]
    [InlineData("Hunter2!")]
    [InlineData("s3cr3t-value-123")]
    public void IsPlaceholder_rejects_real_looking_values(string value)
    {
        Assert.False(SecretValues.IsPlaceholder(value));
    }

    [Theory]
    [InlineData("JwtSecurityTokenHandler", null)]
    [InlineData("snake_case_word", null)]
    [InlineData("config-hardcoded-credential", null)]
    [InlineData("DB_PASSWORD", null)]
    [InlineData("ConnectionStrings:Default", null)]
    [InlineData("Smtp__Password", null)]
    [InlineData("https://example.org/login", null)]
    [InlineData("Password", "Password")]
    public void IsNonSecret_covers_identifiers_keys_and_plain_urls(string value, string? name)
    {
        Assert.True(SecretValues.IsNonSecret(value, name));
    }

    [Theory]
    [InlineData("Hunter2!")]
    [InlineData("hunter 2")]
    public void IsNonSecret_keeps_values_that_could_be_passwords(string value)
    {
        Assert.False(SecretValues.IsNonSecret(value, "password"));
    }

    [Fact]
    public void LooksRandom_needs_length_mixed_classes_and_entropy()
    {
        Assert.True(SecretValues.LooksRandom(FakeSecrets.Random));
        Assert.False(SecretValues.LooksRandom("short1A"));
        Assert.False(SecretValues.LooksRandom("aaaaaaaaaaaaaaaaaaaa1"));
        Assert.False(SecretValues.LooksRandom("alllowercaseletters"));
        Assert.False(SecretValues.LooksRandom("has spaces in 12345 it"));
    }

    [Fact]
    public void Entropy_is_zero_for_one_repeated_character_and_two_bits_for_four_even_ones()
    {
        Assert.Equal(0, SecretValues.Entropy("aaaa"));
        Assert.Equal(2, SecretValues.Entropy("abcd"), precision: 6);
    }
}
