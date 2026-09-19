namespace Qlcheck.Tests;

public class IgnorePatternsTests
{
    [Fact]
    public void Ignores_named_directory_at_any_depth()
    {
        var ignore = IgnorePatterns.Parse(["skipme/"]);

        Assert.True(ignore.IsIgnored("skipme", isDirectory: true));
        Assert.True(ignore.IsIgnored("src/skipme", isDirectory: true));
        Assert.False(ignore.IsIgnored("Keep.cs", isDirectory: false));
    }

    [Fact]
    public void Root_anchored_pattern_does_not_match_nested()
    {
        var ignore = IgnorePatterns.Parse(["/skipme/"]);

        Assert.True(ignore.IsIgnored("skipme", isDirectory: true));
        Assert.False(ignore.IsIgnored("src/skipme", isDirectory: true));
    }

    [Fact]
    public void Relative_path_is_anchored_to_scan_root()
    {
        var ignore = IgnorePatterns.Parse(["vendor/lib/"]);

        Assert.True(ignore.IsIgnored("vendor/lib", isDirectory: true));
        Assert.False(ignore.IsIgnored("other/vendor/lib", isDirectory: true));
    }

    [Fact]
    public void Last_matching_rule_wins_for_negation()
    {
        var ignore = IgnorePatterns.Parse(["skipme/", "!skipme/"]);

        Assert.False(ignore.IsIgnored("skipme", isDirectory: true));
    }

    [Fact]
    public void Skips_comments_and_blank_lines()
    {
        var ignore = IgnorePatterns.Parse(["# hi", "", "  ", "skipme/"]);

        Assert.True(ignore.IsIgnored("skipme", isDirectory: true));
    }
}
