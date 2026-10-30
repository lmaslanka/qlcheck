using System.Globalization;

namespace Qlcheck.Tests;

// The config Language through the CLI: suppression comments in each format's own syntax,
// .qlcheck_disable, and --stats.
public sealed class ConfigCliTests : IDisposable
{
    private const string Credential = "config-hardcoded-credential";

    private const string Secret = "config-hardcoded-secret";

    private const string CheckFlag = "--check";

    private const string EnvFile = ".env";

    private readonly string _dir = Directory.CreateTempSubdirectory("qlcheck_configcli_").FullName;

    private static string Ignore(string id) =>
        $"qlcheck-ignore: {id} checked-on:{DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    public static TheoryData<string, string> SuppressedFiles() => new()
    {
        { "appsettings.json", $"{{\n  // {Ignore(Credential)}\n  \"Smtp\": {{ \"Password\": \"Hunter2!\" }}\n}}\n" },
        { "smtp.yaml", $"smtp:\n  # {Ignore(Credential)}\n  password: Hunter2!\n" },
        { "web.config", $"<configuration>\n  <!-- {Ignore(Credential)} -->\n  <appSettings><add key=\"SmtpPassword\" value=\"Hunter2!\" /></appSettings>\n</configuration>\n" },
    };

    [Theory]
    [MemberData(nameof(SuppressedFiles))]
    public void A_comment_in_the_formats_own_syntax_suppresses_a_finding(string name, string text)
    {
        Save(name, text);

        Assert.Equal(ExitCode.Clean, Run(CheckFlag, Credential, _dir));
        Save(name, text.Replace(Credential, "other-check", StringComparison.Ordinal));
        Assert.Equal(ExitCode.Findings, Run(CheckFlag, Credential, _dir));
    }

    [Fact]
    public void A_comment_suppresses_a_finding_in_a_tracked_dot_env_file()
    {
        Save(EnvFile, $"# {Ignore(Credential)}\nSMTP_PASSWORD=Hunter2!\nDB_PASSWORD=Hunter3!\n");
        TestGit.Run(_dir, "init", "-q");
        TestGit.Run(_dir, "add", EnvFile);

        using var stdout = new StringWriter();
        Assert.Equal(ExitCode.Findings, Run(stdout, CheckFlag, Credential, _dir));
        Assert.Contains(".env:3:", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(".env:2:", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_disable_file_turns_a_config_check_off()
    {
        Save("appsettings.json", $"{{ \"Aws\": {{ \"Id\": \"{FakeSecrets.Aws}\" }} }}\n");
        Assert.Equal(ExitCode.Findings, Run(_dir));

        Save(".qlcheck_disable", $"{Secret}\n");

        Assert.Equal(ExitCode.Clean, Run(_dir));
    }

    [Fact]
    public void Stats_count_config_files_with_source_files()
    {
        Save("appsettings.json", "{ \"Logging\": { \"Level\": \"Warning\" } }\n");
        Save("Program.cs", "class C { }\n");
        Save("launchSettings.json", "{ }\n");

        using var stdout = new StringWriter();
        Run(stdout, "--stats", _dir);

        Assert.Matches(@"Files checked\s+2", stdout.ToString());
    }

    private ExitCode Run(params string[] args)
    {
        using var stdout = new StringWriter();
        return Run(stdout, args);
    }

    private static ExitCode Run(StringWriter stdout, params string[] args)
    {
        using var stderr = new StringWriter();
        return QlcheckApp.Run(args, stdout, stderr);
    }

    private void Save(string name, string text) => File.WriteAllText(Path.Combine(_dir, name), text);
}
