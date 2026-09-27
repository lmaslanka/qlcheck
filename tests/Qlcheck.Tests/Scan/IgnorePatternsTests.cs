namespace Qlcheck.Tests;

public class IgnorePatternsTests
{
    private const string SkipMe = "skipme";

    private const string NestedSkipMe = "src/skipme";

    private const string VendorLib = "vendor/lib";

    private const string OtherVendorLib = "other/vendor/lib";

    [Fact]
    public void Ignores_named_directory_at_any_depth()
    {
        var ignore = IgnorePatterns.Parse(["skipme/"]);

        Assert.True(ignore.IsIgnored(SkipMe, isDirectory: true));
        Assert.True(ignore.IsIgnored(NestedSkipMe, isDirectory: true));
        Assert.False(ignore.IsIgnored("Keep.cs", isDirectory: false));
    }

    [Fact]
    public void Root_anchored_pattern_does_not_match_nested()
    {
        var ignore = IgnorePatterns.Parse(["/skipme/"]);

        Assert.True(ignore.IsIgnored(SkipMe, isDirectory: true));
        Assert.False(ignore.IsIgnored(NestedSkipMe, isDirectory: true));
    }

    [Fact]
    public void Relative_path_is_anchored_to_scan_root()
    {
        var ignore = IgnorePatterns.Parse(["vendor/lib/"]);

        Assert.True(ignore.IsIgnored(VendorLib, isDirectory: true));
        Assert.False(ignore.IsIgnored(OtherVendorLib, isDirectory: true));
    }

    [Fact]
    public void Last_matching_rule_wins_for_negation()
    {
        var ignore = IgnorePatterns.Parse(["skipme/", "!skipme/"]);

        Assert.False(ignore.IsIgnored(SkipMe, isDirectory: true));
    }

    [Fact]
    public void Skips_comments_and_blank_lines()
    {
        var ignore = IgnorePatterns.Parse(["# hi", string.Empty, "  ", "skipme/"]);

        Assert.True(ignore.IsIgnored(SkipMe, isDirectory: true));
    }

    [Fact]
    public void IsIgnored_is_false_for_empty_or_dot_path()
    {
        var ignore = IgnorePatterns.Parse(["skipme/"]);

        Assert.False(ignore.IsIgnored(string.Empty, isDirectory: false));
        Assert.False(ignore.IsIgnored(".", isDirectory: false));
    }

    [Fact]
    public void Parse_skips_a_pattern_that_becomes_empty_after_trimming()
    {
        var ignore = IgnorePatterns.Parse(["!/", "skipme/"]);

        Assert.True(ignore.IsIgnored(SkipMe, isDirectory: true));
    }

    [Fact]
    public void Globstar_prefix_matches_at_any_depth()
    {
        var ignore = IgnorePatterns.Parse(["**/build/"]);

        Assert.True(ignore.IsIgnored("build", isDirectory: true));
        Assert.True(ignore.IsIgnored("a/b/build", isDirectory: true));
    }

    [Fact]
    public void Mid_pattern_globstar_matches_across_directories()
    {
        var ignore = IgnorePatterns.Parse(["src/**/test/"]);

        Assert.True(ignore.IsIgnored("src/a/b/test", isDirectory: true));
    }

    [Fact]
    public void Globstar_not_followed_by_a_slash_matches_like_a_plain_star()
    {
        var ignore = IgnorePatterns.Parse(["test**"]);

        Assert.True(ignore.IsIgnored("testing", isDirectory: false));
    }

    [Fact]
    public void Star_and_question_wildcards_match_single_segment_text()
    {
        var ignore = IgnorePatterns.Parse(["*.log", "file?.txt"]);

        Assert.True(ignore.IsIgnored("debug.log", isDirectory: false));
        Assert.True(ignore.IsIgnored("file1.txt", isDirectory: false));
        Assert.False(ignore.IsIgnored("file12.txt", isDirectory: false));
    }

    [Theory]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData(".git")]
    [InlineData(".vs")]
    [InlineData(".idea")]
    [InlineData(".svn")]
    [InlineData(".hg")]
    [InlineData("node_modules")]
    [InlineData("bower_components")]
    [InlineData("packages")]
    [InlineData("TestResults")]
    [InlineData("coverage")]
    [InlineData("dist")]
    public void Load_skips_common_non_source_directories(string name)
    {
        var ignore = IgnorePatterns.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Assert.True(ignore.IsIgnored(name, isDirectory: true));
        Assert.True(ignore.IsIgnored($"src/{name}", isDirectory: true));
    }
}
