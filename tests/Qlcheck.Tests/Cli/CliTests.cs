using CliModule = Qlcheck.Cli.Cli;

namespace Qlcheck.Tests;

public class CliTests
{
    [Fact]
    public void TryParse_prints_usage_for_help_flag()
    {
        var stderr = new StringWriter();

        var options = CliModule.TryParse(["--help"], stderr);

        Assert.Null(options);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public void TryParse_prints_usage_for_short_help_flag()
    {
        var stderr = new StringWriter();

        var options = CliModule.TryParse(["-h"], stderr);

        Assert.Null(options);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public void TryParse_fails_when_check_value_is_missing()
    {
        var stderr = new StringWriter();

        var options = CliModule.TryParse(["--check"], stderr);

        Assert.Null(options);
        Assert.Contains("Missing value for --check.", stderr.ToString());
    }

    [Fact]
    public void TryParse_fails_for_an_unknown_option()
    {
        var stderr = new StringWriter();

        var options = CliModule.TryParse(["--nope", "src"], stderr);

        Assert.Null(options);
        Assert.Contains("Unknown option: --nope", stderr.ToString());
    }

    [Fact]
    public void TryParse_fails_when_no_paths_are_given()
    {
        var stderr = new StringWriter();

        var options = CliModule.TryParse(["--human"], stderr);

        Assert.Null(options);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public void TryParse_reads_all_flags_and_paths()
    {
        var stderr = new StringWriter();

        var options = CliModule.TryParse(
            ["--human", "--stats", "--check", "no-public-field", "--check", "magic-literal", "src"],
            stderr);

        Assert.NotNull(options);
        Assert.True(options.Human);
        Assert.True(options.Stats);
        Assert.Equal(["no-public-field", "magic-literal"], options.CheckIds);
        Assert.Equal(["src"], options.Paths);
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public void TryParse_defaults_to_current_directory_with_unstaged()
    {
        var stderr = new StringWriter();

        var options = CliModule.TryParse(["--unstaged"], stderr);

        Assert.NotNull(options);
        Assert.True(options.Unstaged);
        Assert.Equal(["."], options.Paths);
    }

    [Fact]
    public void TryParse_keeps_paths_with_unstaged()
    {
        var stderr = new StringWriter();

        var options = CliModule.TryParse(["--unstaged", "src"], stderr);

        Assert.NotNull(options);
        Assert.True(options.Unstaged);
        Assert.Equal(["src"], options.Paths);
    }
}
