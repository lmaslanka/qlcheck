using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class MemberMatchTests
{
    [Fact]
    public void Report_skips_when_member_name_does_not_match()
    {
        var ctx = MatchFixtures.Context("class C { void M() { var x = System.Console.Out; } }");
        MemberMatch.Report(ctx, MatchFixtures.Id, "Error", null);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Report_skips_when_receiver_type_does_not_match()
    {
        var ctx = MatchFixtures.Context("class C { void M() { var x = System.Console.Out; } }");
        MemberMatch.Report(ctx, MatchFixtures.Id, "Out", "Other");
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Report_matches_member_and_receiver_type()
    {
        var ctx = MatchFixtures.Context("class C { void M() { var x = System.Console.Out; } }");
        MemberMatch.Report(ctx, MatchFixtures.Id, "Out", "Console");
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Any_reports_every_matching_member_name()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M() { var a = System.Console.Out; var b = System.Console.Error; } }");
        MemberMatch.Any(ctx, MatchFixtures.Id, ["Out", "Error"]);
        Assert.Equal(2, ctx.Findings.Count);
    }
}
