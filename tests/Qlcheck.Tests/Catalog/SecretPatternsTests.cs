using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

// Sources hold a password as `Hunter2!`, which no secret scanner matches. Tokens come from
// FakeSecrets at run time, split into two constants where folding is under test.
public class SecretPatternsTests
{
    private const string Credential = "hardcoded-credential";

    private const string Database = "database-password";

    private const string Secret = "hardcoded-secret";

    private const string Jwt = "no-hardcoded-jwt-secret";

    private const string TestPath = "tests/App.Tests/RepoTests.cs";

    private const string TokenSlot = "TOKEN";

    private const string AssemblyName = "secrets";

    private const int PrefixEnd = 4;

    private const int MiddleEnd = 10;

    private const string SigningKeyStub = """
        namespace Microsoft.IdentityModel.Tokens
        {
            public class SymmetricSecurityKey
            {
                public SymmetricSecurityKey(byte[] key)
                {
                }
            }
        }

        """;

    private static readonly string[] Ids = [Credential, Database, Secret, Jwt];

    [Theory]
    [InlineData("void M() { var pwd = \"Hunter2!\"; }")]
    [InlineData("string Password { get; } = \"Hunter2!\";")]
    [InlineData("string Password => \"Hunter2!\";")]
    [InlineData("string Password { get { return \"Hunter2!\"; } }")]
    [InlineData("void M(Options o) { o.Password = \"Hunter2!\"; }")]
    [InlineData("Options M() => new Options { Password = \"Hunter2!\" };")]
    [InlineData("void M(Client c) => c.Login(user: \"admin\", password: \"Hunter2!\");")]
    [InlineData("NetworkCredential M() => new NetworkCredential(\"admin\", \"Hunter2!\");")]
    [InlineData("Dictionary<string, string> M() => new() { [\"password\"] = \"Hunter2!\" };")]
    [InlineData("Dictionary<string, string> M() => new() { { \"Smtp:Password\", \"Hunter2!\" } };")]
    [InlineData("void M(Dictionary<string, string> d) => d.Add(\"password\", \"Hunter2!\");")]
    [InlineData("object M() => new KeyValuePair<string, string>(\"password\", \"Hunter2!\");")]
    [InlineData("object M() => new { Password = \"Hunter2!\" };")]
    [InlineData("void Login(string password = \"Hunter2!\") { }")]
    [InlineData("bool M(string password) => password == \"Hunter2!\";")]
    [InlineData("string GetPassword() { return \"Hunter2!\"; }")]
    public void Credential_finds_a_password_in_every_named_place(string member)
    {
        var finding = Assert.Single(Run(member));

        Assert.Equal(Credential, finding.Check);
    }

    [Theory]
    [InlineData("string M(Dictionary<string, string> config) => config[\"Password\"];")]
    [InlineData("string? M() => Environment.GetEnvironmentVariable(\"DB_PASSWORD\");")]
    [InlineData("const string PasswordPolicy = \"Strong1!\";")]
    [InlineData("void M() { var password = \"changeme\"; }")]
    [InlineData("void M() { var password = \"{password}\"; }")]
    [InlineData("void M() { var password = \"Password\"; }")]
    [InlineData("void M() { var user = \"Hunter2!\"; }")]
    [InlineData("void M(Func<string> f) { f = () => { return \"Hunter2!\"; }; }")]
    public void Credential_stays_quiet_on_lookups_placeholders_and_other_names(string member)
    {
        Assert.Empty(Run(member));
    }

    [Fact]
    public void Credential_is_skipped_on_test_paths()
    {
        Assert.Empty(Run("void M() { var password = \"Hunter2!\"; }", TestPath));
    }

    [Theory]
    [InlineData("const string Conn = \"Server=db;User Id=sa;Password=Hunter2!;\";")]
    [InlineData("string M(string name) => $\"Server=db;User Id=sa;Password=Hunter2!;Database={name}\";")]
    [InlineData("void M(SqlConnectionStringBuilder b) { b.Password = \"Hunter2!\"; }")]
    [InlineData("object M() => new SqlConnectionStringBuilder { Password = \"Hunter2!\" };")]
    public void Database_password_finds_a_password_in_a_connection_string(string member)
    {
        var finding = Assert.Single(Run(member));

        Assert.Equal(Database, finding.Check);
    }

    [Theory]
    [InlineData("const string Conn = \"Server=db;User Id=sa;Password=;\";")]
    [InlineData("string M(string pwd) => $\"Server=db;User Id=sa;Password={pwd};\";")]
    [InlineData("string M(string pwd) => \"Server=db;User Id=sa;Password=\" + pwd;")]
    public void Database_password_ignores_empty_and_supplied_passwords(string member)
    {
        Assert.Empty(Run(member));
    }

    [Fact]
    public void Database_password_finds_a_password_in_a_database_uri()
    {
        var member = FakeSecrets.Join("const string Conn = \"postgres://app:", "Hunter2!@db/app\";");

        Assert.Equal(Database, Assert.Single(Run(member)).Check);
    }

    [Fact]
    public void A_password_named_connection_string_gives_exactly_one_finding()
    {
        var finding = Assert.Single(Run("void M(Options o) { o.Password = \"Server=db;User Id=sa;Password=Hunter2!\"; }"));

        Assert.Equal(Database, finding.Check);
    }

    [Fact]
    public void Database_password_still_fires_on_test_paths()
    {
        Assert.Single(Run("const string Conn = \"Server=db;User Id=sa;Password=Hunter2!;\";", TestPath));
    }

    [Fact]
    public void Secret_folds_a_split_constant_and_names_the_provider()
    {
        var token = FakeSecrets.GitHub;
        var member = $"const string Value = \"{token[..PrefixEnd]}\" + (\"{token[PrefixEnd..MiddleEnd]}\" + \"{token[MiddleEnd..]}\");";

        var finding = Assert.Single(Run(member));

        Assert.Equal(Secret, finding.Check);
        Assert.EndsWith("It looks like a GitHub token.", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ReadOnlySpan<byte> Key => \"TOKEN\"u8;")]
    [InlineData("const string Key = @\"TOKEN\";")]
    [InlineData("const string Key = \"\"\"TOKEN\"\"\";")]
    [InlineData("string M(int n) => $\"{n}: TOKEN\";")]
    [InlineData("const string Inner = \"TOKEN\"; const string Outer = $\"{Inner}\";")]
    public void Secret_reads_every_string_form(string template)
    {
        var findings = Run(template.Replace(TokenSlot, FakeSecrets.Aws, StringComparison.Ordinal));

        Assert.NotEmpty(findings);
        Assert.All(findings, finding => Assert.Equal(Secret, finding.Check));
    }

    [Fact]
    public void Secret_needs_a_random_value_under_a_secret_name()
    {
        Assert.Single(Run($"const string ApiKey = \"{FakeSecrets.Random}\";"));
        Assert.Empty(Run("const string ApiKey = \"my-api-key\";"));
        Assert.Empty(Run("const string JwtSecurityTokenHandlerTypeName = \"JwtSecurityTokenHandler\";"));
    }

    [Fact]
    public void On_test_paths_a_format_still_fires_but_a_secret_name_does_not()
    {
        Assert.Single(Run($"string M() => FakeSecretsLike.Of(\"{FakeSecrets.StripeLive}\");", TestPath));
        Assert.Empty(Run($"const string ApiKey = \"{FakeSecrets.Random}\";", TestPath));
    }

    [Theory]
    [InlineData("SymmetricSecurityKey M() => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(\"my signing key phrase 2024\"));")]
    [InlineData("SymmetricSecurityKey M() => new(Encoding.ASCII.GetBytes(\"my signing key phrase 2024\"));")]
    [InlineData("const string Key = \"my signing key phrase 2024\"; SymmetricSecurityKey M() => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));")]
    [InlineData("static readonly byte[] Bytes = Encoding.UTF8.GetBytes(\"my signing key phrase 2024\"); SymmetricSecurityKey M() => new SymmetricSecurityKey(Bytes);")]
    [InlineData("SymmetricSecurityKey M() => new SymmetricSecurityKey(Convert.FromBase64String(\"bXkgc2lnbmluZyBrZXk=\"));")]
    [InlineData("SymmetricSecurityKey M() => new SymmetricSecurityKey(new byte[] { 1, 2, 3, 4 });")]
    [InlineData("Dictionary<string, string> M() => new() { [\"Jwt:Key\"] = \"abc\" };")]
    public void Jwt_secret_finds_a_signing_key_built_from_a_constant(string member)
    {
        var finding = Assert.Single(RunWith(SigningKeyStub, member));

        Assert.Equal(Jwt, finding.Check);
    }

    [Theory]
    [InlineData("SymmetricSecurityKey M() => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable(\"JWT_KEY\") ?? string.Empty));")]
    [InlineData("SymmetricSecurityKey M(Dictionary<string, string> c) => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(c[\"Jwt:Key\"]));")]
    [InlineData("SymmetricSecurityKey M() => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(\"<signing key>\"));")]
    public void Jwt_secret_stays_quiet_when_the_key_comes_from_outside(string member)
    {
        Assert.Empty(RunWith(SigningKeyStub, member));
    }

    [Fact]
    public void Findings_never_repeat_the_value_and_carry_no_replacement()
    {
        var token = FakeSecrets.Anthropic;
        var members = string.Join(
            "\n",
            "void A() { var pwd = \"Hunter2!\"; }",
            "const string Conn = \"Server=db;User Id=sa;Password=Hunter2!;\";",
            $"const string Key = \"{token}\";",
            "SymmetricSecurityKey B() => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(\"my signing key phrase 2024\"));");

        var findings = RunWith(SigningKeyStub, members);

        Assert.Equal(Ids.Order(StringComparer.Ordinal), findings.Select(finding => finding.Check).Order(StringComparer.Ordinal));
        Assert.All(findings, finding => Assert.Null(finding.Replacement));
        Assert.All(findings, finding => Assert.DoesNotContain("Hunter2!", finding.Message, StringComparison.Ordinal));
        Assert.All(findings, finding => Assert.DoesNotContain(token, finding.Message, StringComparison.Ordinal));
        Assert.All(findings, finding => Assert.DoesNotContain("my signing key", finding.Message, StringComparison.Ordinal));
    }

    private static IReadOnlyList<Finding> Run(string member, string path = "Repo.cs") => RunWith(string.Empty, member, path);

    private static IReadOnlyList<Finding> RunWith(string prefix, string member, string path = "Repo.cs")
    {
        var source = $$"""
            {{prefix}}namespace Sample
            {
                using System;
                using System.Collections.Generic;
                using System.Net;
                using System.Text;
                using Microsoft.IdentityModel.Tokens;

                class Options { public string Password { get; set; } = string.Empty; }

                class Client { public void Login(string user, string password) { } }

                class SqlConnectionStringBuilder { public string Password { get; set; } = string.Empty; }

                static class FakeSecretsLike { public static string Of(string value) => value; }

                class C
                {
                    {{member}}
                }
            }
            """;
        var file = new SourceFile(path, source);
        var tree = CSharpTrees.Parse(file);
        var model = CSharpCompilations.Create(AssemblyName, [tree]).GetSemanticModel(tree);
        var messages = Ids.ToDictionary(id => id, id => Catalog.Messages[id], StringComparer.Ordinal);
        var ctx = WalkContext.Create(file, tree, model, messages);
        SecretPatterns.Credential(ctx, Credential);
        SecretPatterns.DatabasePassword(ctx, Database);
        SecretPatterns.Secret(ctx, Secret);
        SecretPatterns.JwtSecret(ctx, Jwt);
        return ctx.Findings;
    }
}
