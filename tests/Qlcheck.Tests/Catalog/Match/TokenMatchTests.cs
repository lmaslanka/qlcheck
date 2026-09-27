using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class TokenMatchTests
{
    [Fact]
    public void Report_skips_when_no_token_matches()
    {
        var ctx = MatchFixtures.Context("class C { void M() { } }");
        TokenMatch.Report(ctx, MatchFixtures.Id, "nope");
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Report_matches_every_token_with_the_given_text()
    {
        var ctx = MatchFixtures.Context("class C { void M(int value) { var value2 = value; } }");
        TokenMatch.Report(ctx, MatchFixtures.Id, "value");
        Assert.Equal(2, ctx.Findings.Count);
    }
}
