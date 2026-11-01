using Qlcheck.Languages;
using Qlcheck.Languages.Config;

namespace Qlcheck.Tests;

// Connection strings with a password are assembled at run time (FakeSecrets.Join) so this file
// holds none whole; tokens come from FakeSecrets.
public sealed class ConfigLanguageTests : IDisposable
{
    private const string Secret = "config-hardcoded-secret";

    private const string Credential = "config-hardcoded-credential";

    private const string Database = "config-database-password";

    private const string Jwt = "config-hardcoded-jwt-secret";

    private const string Settings = "appsettings.json";

    private const string EnvFile = ".env";

    private readonly string _dir = Directory.CreateTempSubdirectory("qlcheck_config_").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("appsettings.json", true)]
    [InlineData("AppSettings.Production.json", true)]
    [InlineData("web.config", true)]
    [InlineData("NuGet.config", true)]
    [InlineData("docker-compose.override.yml", true)]
    [InlineData("deploy.yaml", true)]
    [InlineData("launchSettings.json", false)]
    [InlineData("package.json", false)]
    [InlineData("Program.cs", false)]
    public void Matches_claims_configuration_files_by_name(string name, bool expected)
    {
        Assert.Equal(expected, new ConfigLanguage().Matches(name));
    }

    [Fact]
    public void Matches_claims_a_dot_env_file_only_when_git_tracks_it()
    {
        var tracked = Write(EnvFile, "A=1\n");
        var untracked = Write(".env.local", "A=1\n");
        Assert.False(new ConfigLanguage().Matches(tracked));

        Git("init", "-q");
        Git("add", EnvFile);

        var language = new ConfigLanguage();
        Assert.True(language.Matches(tracked));
        Assert.False(language.Matches(untracked));
    }

    [Fact]
    public void Each_kind_is_found_in_appsettings_json()
    {
        var text = FakeSecrets.Join(
            """
            {
              "ConnectionStrings": { "Default": "Server=db;User Id=app;Password=
            """,
            """
            Hunter2!" },
              "Smtp": { "Password": "Hunter2!" },
              "Jwt": { "Key": "my signing key phrase 2024" },
            """,
            $"  \"Stripe\": {{ \"Key\": \"{FakeSecrets.StripeLive}\" }}\n}}\n");

        var findings = Run(Settings, text);

        Assert.Equal([Database, Credential, Jwt, Secret], findings.Select(finding => finding.Check));
        Assert.Equal(ConfigText.PositionOf(text, "Server=db").Line, findings[0].Line);
    }

    [Fact]
    public void Each_kind_is_found_in_a_config_file()
    {
        var text = FakeSecrets.Join(
            """
            <configuration>
              <connectionStrings>
                <add name="Default" connectionString="Server=db;User Id=app;Password=
            """,
            """
            Hunter2!" />
              </connectionStrings>
              <packageSourceCredentials>
                <Feed><add key="ClearTextPassword" value="Hunter2!" /></Feed>
              </packageSourceCredentials>
            </configuration>
            """);

        Assert.Equal([Database, Credential], Run("web.config", text).Select(finding => finding.Check));
    }

    [Fact]
    public void Each_kind_is_found_in_yaml_and_dot_env()
    {
        var yaml = $"services:\n  web:\n    environment:\n      - SMTP_PASSWORD=Hunter2!\n      - GITHUB_TOKEN={FakeSecrets.GitHub}\n";
        var env = FakeSecrets.Join("ConnectionStrings__Default=Server=db;User Id=app;Password=", "Hunter2!\nJwt__Key=my signing key phrase 2024\n");

        Assert.Equal([Credential, Secret], Run("docker-compose.yml", yaml).Select(finding => finding.Check));
        Assert.Equal([Database, Jwt], Run("prod.env", env, ".env.production").Select(finding => finding.Check));
    }

    [Fact]
    public void A_value_gets_one_finding_and_the_message_names_the_provider_not_the_value()
    {
        var token = FakeSecrets.GitHub;

        var finding = Assert.Single(Run(Settings, $"{{ \"GitHub\": {{ \"Token\": \"{token}\" }} }}"));

        Assert.Equal(Secret, finding.Check);
        Assert.EndsWith("It looks like a GitHub token.", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(token, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_private_key_block_is_found_in_raw_text()
    {
        var text = $"tls:\n  key: |\n    {FakeSecrets.PrivateKey}\n    MIIEvQIBADANBgkqhkiG9w0BAQEFAASC\n";

        var finding = Assert.Single(Run("tls.yaml", text));

        Assert.Equal(Secret, finding.Check);
        Assert.Contains("private key", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Test_paths_keep_formats_and_drop_named_passwords()
    {
        var text = $"{{ \"Smtp\": {{ \"Password\": \"Hunter2!\" }}, \"Aws\": {{ \"Id\": \"{FakeSecrets.Aws}\" }} }}";

        Assert.Equal([Secret], Run(Settings, text, "tests/App.Tests/appsettings.json").Select(finding => finding.Check));
    }

    [Fact]
    public void Placeholders_and_empty_values_stay_quiet()
    {
        const string text = """
            {
              "ConnectionStrings": { "Default": "Server=db;User Id=app;Password=;" },
              "Smtp": { "Password": "" , "ApiKey": "${SMTP_API_KEY}" },
              "Jwt": { "Key": "<set in user-secrets>" }
            }
            """;

        Assert.Empty(Run(Settings, text));
    }

    private IReadOnlyList<Finding> Run(string name, string text, string? relative = null)
    {
        var path = Write(relative ?? name, text);
        var source = new SourceScan.LoadedSource(path, new SourceFile(relative ?? name, text));
        var language = new ConfigLanguage();
        return language.Execute([source], language.Checks, RunInputs.None).Findings;
    }

    private string Write(string relative, string text)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private void Git(params string[] args) => TestGit.Run(_dir, args);
}
