using Qlcheck.Secrets;

namespace Qlcheck.Tests;

public class SecretRulesTests
{
    private const string Password = "Hunter2!";

    private const string PasswordName = "password";

    private const string JwtKeyPath = "Jwt:Key";

    private const string ShortKey = "abc";

    private const string ApiKeyName = "ApiKey";

    private const string PropertyName = "Password";

    private static SecretContext Plain => new(IsTestPath: false, IsConnectionStringSlot: false, IsSigningKeySlot: false);

    private static SecretContext TestPath => Plain with { IsTestPath = true };

    [Fact]
    public void A_signing_key_slot_is_a_jwt_key_whatever_the_value_looks_like()
    {
        var hit = SecretRules.Classify(null, null, "this is my signing key phrase", Plain with { IsSigningKeySlot = true });

        AssertKind(SecretKind.JwtKey, hit);
    }

    [Fact]
    public void A_jwt_config_key_path_is_a_jwt_key()
    {
        AssertKind(SecretKind.JwtKey, SecretRules.Classify(null, "Jwt:SigningKey", ShortKey, Plain));
    }

    [Fact]
    public void A_placeholder_in_a_signing_key_slot_is_not_a_key()
    {
        Assert.Null(SecretRules.Classify(null, JwtKeyPath, "<key>", Plain));
    }

    [Fact]
    public void A_connection_string_password_wins_over_a_password_name()
    {
        var value = FakeSecrets.Join("Server=db;User Id=sa;Password=", "Hunter2!;");

        AssertKind(SecretKind.ConnectionPassword, SecretRules.Classify(PropertyName, null, value, Plain));
    }

    [Fact]
    public void A_connection_string_builder_password_is_a_connection_password()
    {
        var hit = SecretRules.Classify(PropertyName, null, Password, Plain with { IsConnectionStringSlot = true });

        AssertKind(SecretKind.ConnectionPassword, hit);
    }

    [Fact]
    public void A_known_format_needs_no_name_and_names_its_provider()
    {
        var hit = SecretRules.Classify(null, null, FakeSecrets.GitHub, Plain);

        AssertKind(SecretKind.Format, hit);
        Assert.Equal("It looks like a GitHub token.", hit?.Detail);
    }

    [Fact]
    public void A_password_name_needs_no_entropy()
    {
        AssertKind(SecretKind.Password, SecretRules.Classify(PasswordName, null, Password, Plain));
    }

    [Fact]
    public void A_secret_name_needs_a_random_looking_value()
    {
        AssertKind(SecretKind.NamedSecret, SecretRules.Classify(ApiKeyName, null, FakeSecrets.Random, Plain));
        Assert.Null(SecretRules.Classify(ApiKeyName, null, "my-api-key", Plain));
    }

    [Fact]
    public void The_name_comes_from_the_last_key_segment_when_none_is_given()
    {
        AssertKind(SecretKind.Password, SecretRules.Classify(null, "Smtp:Password", Password, Plain));
    }

    [Theory]
    [InlineData(PasswordName, "")]
    [InlineData(PasswordName, "changeme")]
    [InlineData(PasswordName, "DB_PASSWORD")]
    [InlineData(PasswordName, "Smtp:Password")]
    [InlineData("TokenTypeName", "JwtSecurityTokenHandler")]
    [InlineData("PasswordLength", Password)]
    [InlineData("user", Password)]
    public void Names_and_values_that_are_not_secrets_stay_quiet(string name, string value)
    {
        Assert.Null(SecretRules.Classify(name, null, value, Plain));
    }

    [Fact]
    public void Test_paths_keep_the_name_free_kinds_and_drop_the_named_ones()
    {
        AssertKind(SecretKind.Format, SecretRules.Classify(null, null, FakeSecrets.Aws, TestPath));
        AssertKind(SecretKind.JwtKey, SecretRules.Classify(null, JwtKeyPath, ShortKey, TestPath));
        Assert.Null(SecretRules.Classify(PasswordName, null, Password, TestPath));
        Assert.Null(SecretRules.Classify(ApiKeyName, null, FakeSecrets.Random, TestPath));
    }

    // A helper rather than Assert.Equal on the enum, so the enum's values do not read as existing
    // consts for every Assert.Equal with a small number elsewhere (magic-literal-shared).
    private static void AssertKind(SecretKind expected, SecretHit? hit) => Assert.Equal(expected, hit?.Kind);
}
