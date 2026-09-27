using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class CreateMatchTests
{
    [Fact]
    public void Report_skips_non_matching_type()
    {
        var ctx = MatchFixtures.Context("class C { void M() { var x = new object(); } }");
        CreateMatch.Report(ctx, MatchFixtures.Id, "StringBuilder");
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Report_matches_created_type()
    {
        var ctx = MatchFixtures.Context("class C { void M() { var x = new System.Text.StringBuilder(); } }");
        CreateMatch.Report(ctx, MatchFixtures.Id, "StringBuilder");
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Any_reports_every_matching_type_name()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M() { var a = new object(); var b = new System.Text.StringBuilder(); } }");
        CreateMatch.Any(ctx, MatchFixtures.Id, ["object", "StringBuilder"]);
        Assert.Equal(2, ctx.Findings.Count);
    }
}
