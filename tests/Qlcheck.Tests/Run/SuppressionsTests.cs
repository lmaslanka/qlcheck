using Qlcheck.Run;
using Qlcheck.Scan;

namespace Qlcheck.Tests;

public class SuppressionsTests
{
    private const string CheckId = "jwt-strong-signature";

    private static IReadOnlyList<Finding> Filter(string text, int line, string checkId = CheckId)
    {
        var path = "Repo.cs";
        var loaded = new List<SourceScan.LoadedSource>
        {
            new(path, new SourceFile(path, text)),
        };
        var findings = new List<Finding> { new($"{checkId}:{path}:{line}:1", checkId, path, line, 1, "message") };
        return Suppressions.Filter(findings, loaded);
    }

    [Fact]
    public void Filter_suppresses_a_finding_with_a_same_line_comment_checked_today()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var text = $"var creds = new SigningCredentials(); // qlcheck-ignore: {CheckId} checked-on:{today}\n";
        Assert.Empty(Filter(text, line: 1));
    }

    [Fact]
    public void Filter_suppresses_a_finding_with_a_comment_on_the_line_above()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var text = $"// qlcheck-ignore: {CheckId} checked-on:{today}\nvar creds = new SigningCredentials();\n";
        Assert.Empty(Filter(text, line: 2));
    }

    [Fact]
    public void Filter_keeps_a_finding_with_no_suppression_comment()
    {
        var text = "var creds = new SigningCredentials();\n";
        Assert.Single(Filter(text, line: 1));
    }

    [Fact]
    public void Filter_keeps_a_finding_when_the_comment_is_for_a_different_rule()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var text = $"var creds = new SigningCredentials(); // qlcheck-ignore: sql-injection checked-on:{today}\n";
        Assert.Single(Filter(text, line: 1));
    }

    [Fact]
    public void Filter_keeps_a_finding_once_the_comment_is_older_than_one_year()
    {
        var expired = DateTime.UtcNow.AddDays(-366).ToString("yyyy-MM-dd");
        var text = $"var creds = new SigningCredentials(); // qlcheck-ignore: {CheckId} checked-on:{expired}\n";
        Assert.Single(Filter(text, line: 1));
    }

    [Fact]
    public void Filter_suppresses_a_finding_on_the_last_day_of_the_one_year_window()
    {
        var almostExpired = DateTime.UtcNow.AddDays(-365).ToString("yyyy-MM-dd");
        var text = $"var creds = new SigningCredentials(); // qlcheck-ignore: {CheckId} checked-on:{almostExpired}\n";
        Assert.Empty(Filter(text, line: 1));
    }

    [Fact]
    public void Filter_keeps_a_finding_with_a_future_dated_comment()
    {
        var future = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        var text = $"var creds = new SigningCredentials(); // qlcheck-ignore: {CheckId} checked-on:{future}\n";
        Assert.Single(Filter(text, line: 1));
    }

    [Fact]
    public void Filter_keeps_a_finding_with_a_malformed_date()
    {
        var text = $"var creds = new SigningCredentials(); // qlcheck-ignore: {CheckId} checked-on:not-a-date\n";
        Assert.Single(Filter(text, line: 1));
    }

    [Fact]
    public void Filter_is_case_insensitive_on_the_rule_id()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var text = $"var creds = new SigningCredentials(); // QLCHECK-IGNORE: {CheckId.ToUpperInvariant()} CHECKED-ON:{today}\n";
        Assert.Empty(Filter(text, line: 1));
    }

    [Fact]
    public void Filter_ignores_a_comment_two_lines_above()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var text = $"// qlcheck-ignore: {CheckId} checked-on:{today}\n\nvar creds = new SigningCredentials();\n";
        Assert.Single(Filter(text, line: 3));
    }

    [Fact]
    public void Filter_suppresses_a_finding_listed_first_in_a_comma_separated_rule_list()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var text =
            $"var creds = new SigningCredentials(); // qlcheck-ignore: {CheckId}, sql-injection checked-on:{today}\n";
        Assert.Empty(Filter(text, line: 1));
    }

    [Fact]
    public void Filter_suppresses_a_finding_listed_second_in_a_comma_separated_rule_list()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var text =
            $"var creds = new SigningCredentials(); // qlcheck-ignore: sql-injection, {CheckId} checked-on:{today}\n";
        Assert.Empty(Filter(text, line: 1));
    }

    [Fact]
    public void Filter_keeps_a_finding_not_named_in_a_comma_separated_rule_list()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var text =
            "var creds = new SigningCredentials(); "
            + $"// qlcheck-ignore: sql-injection, no-console-log checked-on:{today}\n";
        Assert.Single(Filter(text, line: 1));
    }
}
