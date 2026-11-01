using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Tests;

public class ReachTests
{
    [Theory]
    [InlineData("if (false) { Console.WriteLine(); }", 1)]
    [InlineData("if (Enabled) { Console.WriteLine(); }", 1)]
    [InlineData("const bool off = false; if (off && flag) { Console.WriteLine(); }", 1)]
    [InlineData("if (flag) { Console.WriteLine(); }", 0)]
    [InlineData("if (true) { Console.WriteLine(); }", 0)]
    [InlineData("return; if (false) { Console.WriteLine(); }", 0)]
    public void An_if_body_that_cannot_run_is_reported(string body, int findings)
    {
        Assert.Equal(findings, Run(Reach.UnreachableBranch, $"class C {{ const bool Enabled = false; void M(bool flag) {{ {body} }} }}"));
    }

    [Theory]
    [InlineData("while (true) { Console.WriteLine(); }", 1)]
    [InlineData("for (;;) { Console.WriteLine(); }", 1)]
    [InlineData("do { Console.WriteLine(); } while (true);", 1)]
    [InlineData("while (Forever) { }", 1)]
    [InlineData("while (true) { switch (x) { case 1: break; } }", 1)]
    [InlineData("while (true) { Func<int> f = () => { return 1; }; f(); }", 1)]
    [InlineData("while (true) { if (x > 1) { return; } }", 0)]
    [InlineData("while (true) { if (x > 1) { break; } }", 0)]
    [InlineData("while (true) { if (x > 1) { throw new InvalidOperationException(); } }", 0)]
    [InlineData("while (x > 0) { }", 0)]
    [InlineData("while (true) { token.ThrowIfCancellationRequested(); }", 0)]
    [InlineData("while (true) { if (token.IsCancellationRequested) { } }", 0)]
    public void A_loop_that_can_never_end_is_reported(string body, int findings)
    {
        Assert.Equal(findings, Run(Reach.InfiniteLoop, $"using System; using System.Threading; class C {{ const bool Forever = true; void M(int x, CancellationToken token) {{ {body} }} }}"));
    }

    [Fact]
    public void A_loop_that_awaits_is_not_reported()
    {
        Assert.Equal(0, Run(Reach.InfiniteLoop, "using System.Threading.Tasks; class C { async Task M() { while (true) { await Task.Delay(1); } } }"));
    }

    private static int Run(Action<Qlcheck.Languages.CSharp.Catalog.WalkContext, string> check, string source)
    {
        var ctx = MatchFixtures.Context(source.StartsWith("using", StringComparison.Ordinal) ? source : $"using System; {source}");
        check(ctx, MatchFixtures.Id);
        return ctx.Findings.Count;
    }
}
