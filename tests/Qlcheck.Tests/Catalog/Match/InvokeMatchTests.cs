using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class InvokeMatchTests
{
    [Fact]
    public void Report_skips_when_receiver_type_does_not_match()
    {
        var ctx = MatchFixtures.Context("class C { void M() { System.Console.WriteLine(); } }");
        InvokeMatch.Report(ctx, MatchFixtures.Id, "WriteLine", "Other");
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Report_matches_when_receiver_type_matches()
    {
        var ctx = MatchFixtures.Context("class C { void M() { System.Console.WriteLine(); } }");
        InvokeMatch.Report(ctx, MatchFixtures.Id, "WriteLine", "Console");
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Any_reports_every_matching_method_name()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M() { System.Console.WriteLine(); System.Console.Write(); } }");
        InvokeMatch.Any(ctx, MatchFixtures.Id, ["WriteLine", "Write"]);
        Assert.Equal(2, ctx.Findings.Count);
    }
}
