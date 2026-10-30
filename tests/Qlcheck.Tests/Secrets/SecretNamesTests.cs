using Qlcheck.Secrets;

namespace Qlcheck.Tests;

public class SecretNamesTests
{
    [Theory]
    [InlineData("password", "Password")]
    [InlineData("DbPassword", "Password")]
    [InlineData("DB_PASSWORD", "Password")]
    [InlineData("smtp-pwd", "Password")]
    [InlineData("Credentials", "Password")]
    [InlineData("ApiKey", "Secret")]
    [InlineData("API_KEY", "Secret")]
    [InlineData("APIKey", "Secret")]
    [InlineData("clientSecret", "Secret")]
    [InlineData("AccessToken", "Secret")]
    [InlineData("SigningKey", "Secret")]
    [InlineData("Token", "Secret")]
    [InlineData("PasswordPolicy", "None")]
    [InlineData("PasswordLength", "None")]
    [InlineData("TokenTypeName", "None")]
    [InlineData("ApiKeyHeader", "None")]
    [InlineData("PasswordResetUrl", "None")]
    [InlineData("JwtSecurityTokenHandlerTypeName", "None")]
    [InlineData("TokenCacheKey", "None")]
    [InlineData("PasswordKey", "None")]
    [InlineData("MonkeyName", "None")]
    [InlineData("Keyboard", "None")]
    [InlineData("user", "None")]
    [InlineData(null, "None")]
    public void Classify_reads_the_words_of_a_name(string? name, string expected)
    {
        Assert.Equal(expected, SecretNames.Classify(name).ToString());
    }

    [Theory]
    [InlineData("getHTTPResponseCode", new[] { "get", "http", "response", "code" })]
    [InlineData("Jwt__Signing_key", new[] { "jwt", "signing", "key" })]
    [InlineData("OAuth2Token", new[] { "o", "auth", "2", "token" })]
    [InlineData("ConnectionStrings:Default", new[] { "connection", "strings", "default" })]
    public void Words_split_every_naming_style(string name, string[] expected)
    {
        Assert.Equal(expected, SecretNames.Words(name));
    }

    [Theory]
    [InlineData("Jwt:Key", true)]
    [InlineData("JwtSettings:IssuerSigningKey", true)]
    [InlineData("Authentication:JwtBearer:Secret", true)]
    [InlineData("Jwt__SigningKey", true)]
    [InlineData("Jwt:Issuer", false)]
    [InlineData("Key", false)]
    [InlineData(null, false)]
    public void IsJwtKeyPath_needs_a_jwt_section_and_a_key_name(string? keyPath, bool expected)
    {
        Assert.Equal(expected, SecretNames.IsJwtKeyPath(keyPath));
    }

    [Theory]
    [InlineData("ConnectionStrings:Default", "Default")]
    [InlineData("Smtp__Password", "Password")]
    [InlineData("password", "password")]
    public void LastSegment_is_the_name_a_value_is_stored_under(string keyPath, string expected)
    {
        Assert.Equal(expected, SecretNames.LastSegment(keyPath));
    }
}
