using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class QuietPatternsTests
{
    [Fact]
    public void CompareThenAssign_ignores_if_bodies_that_are_not_a_matching_assignment()
    {
        var ctx = MatchFixtures.Context(
            """
            class C
            {
                void M(int a, int b)
                {
                    if (a == b) { return; }
                    if (a == b) { Foo(); }
                }
            }
            """);
        QuietPatterns.CompareThenAssign(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ConsecutiveReturn_ignores_block_without_two_consecutive_returns()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M(bool b) { if (b) return; DoSomething(); return; } }");
        QuietPatterns.ConsecutiveReturn(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ReadonlyAssign_ignores_assignment_to_a_plain_variable()
    {
        var ctx = MatchFixtures.Context("class C { void M(int x) { x = 1; } }");
        QuietPatterns.ReadonlyAssign(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void StaticOrder_ignores_non_static_field()
    {
        var ctx = MatchFixtures.Context("class C { int x; }");
        QuietPatterns.StaticOrder(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void StaticOrder_ignores_static_field_outside_a_class()
    {
        var ctx = MatchFixtures.Context("struct S { static int x; }");
        QuietPatterns.StaticOrder(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LocalCouldBeConst_ignores_local_const()
    {
        var ctx = MatchFixtures.Context("class C { void M() { const int x = 1; } }");
        QuietPatterns.LocalCouldBeConst(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LocalCouldBeConst_ignores_declaration_outside_a_method()
    {
        var ctx = MatchFixtures.Context("class C { C() { var x = 1; } }");
        QuietPatterns.LocalCouldBeConst(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LocalCouldBeConst_ignores_variable_that_is_reassigned()
    {
        var ctx = MatchFixtures.Context("class C { void M() { var x = 1; x = 2; } }");
        QuietPatterns.LocalCouldBeConst(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LocalCouldBeConst_ignores_variable_that_is_incremented()
    {
        var ctx = MatchFixtures.Context("class C { void M() { var x = 1; x++; } }");
        QuietPatterns.LocalCouldBeConst(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LocalCouldBeConst_ignores_variable_written_via_ref_argument()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M() { var changed = 0; System.Threading.Interlocked.Increment(ref changed); } }");
        QuietPatterns.LocalCouldBeConst(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LocalCouldBeConst_ignores_variable_written_via_out_argument()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M() { var ok = false; bool.TryParse(\"true\", out ok); } }");
        QuietPatterns.LocalCouldBeConst(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }
}
