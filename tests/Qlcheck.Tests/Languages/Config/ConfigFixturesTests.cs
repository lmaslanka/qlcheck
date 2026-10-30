using System.Text.Json;
using Qlcheck.Languages.Config;

namespace Qlcheck.Tests;

// Each config Check's bad and good fixture, written under its path and run through the CLI, so the
// file must also be claimed by ConfigLanguage.Matches. `{{fake:name}}` markers become FakeSecrets
// tokens at run time, because a committed file must never hold a whole token.
public class ConfigFixturesTests
{
    private const string FixtureFile = "tests/Qlcheck.Tests/Languages/Config/fixtures.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<string, Func<string>> Fakes = new(StringComparer.Ordinal)
    {
        ["{{fake:github}}"] = () => FakeSecrets.GitHub,
        ["{{fake:aws}}"] = () => FakeSecrets.Aws,
        ["{{fake:stripe}}"] = () => FakeSecrets.StripeLive,
        ["{{fake:pem}}"] = () => FakeSecrets.PrivateKey,
    };

    [Fact]
    public void Each_config_check_has_a_failing_and_clean_fixture()
    {
        var fixtures = LoadFixtures();
        var failures = new List<string>();
        foreach (var check in ConfigCatalog.Checks)
        {
            if (!fixtures.TryGetValue(check.Id, out var fixture))
            {
                failures.Add($"{check.Id} has no fixture");
                continue;
            }

            if (Run(check.Id, fixture.Path, fixture.Bad) != ExitCode.Findings)
            {
                failures.Add($"{check.Id} missed");
            }

            if (Run(check.Id, fixture.Path, fixture.Good) != ExitCode.Clean)
            {
                failures.Add($"{check.Id} clean");
            }
        }

        Assert.Empty(failures);
    }

    private static ExitCode Run(string id, string relative, string text)
    {
        var dir = Directory.CreateTempSubdirectory("qlcheck_configfx_");
        try
        {
            var path = Path.Combine(dir.FullName, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Fakes.Aggregate(text, (current, fake) => current.Replace(fake.Key, fake.Value(), StringComparison.Ordinal)));
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            return QlcheckApp.Run(["--check", id, dir.FullName], stdout, stderr);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static Dictionary<string, Fixture> LoadFixtures()
    {
        var path = Path.Combine(RepoPaths.Root(), FixtureFile);
        return JsonSerializer.Deserialize<Dictionary<string, Fixture>>(File.ReadAllText(path), JsonOptions) ?? [];
    }

    private sealed record Fixture(string Bad, string Good, string Path);
}
