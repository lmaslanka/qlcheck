using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class PatternsTests
{
    [Fact]
    public void SuffixFlags_reports_enum_ending_in_flags()
    {
        var ctx = MatchFixtures.Context("enum ColorFlags { None }");
        Patterns.SuffixFlags(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void SuffixEnum_reports_enum_ending_in_enum()
    {
        var ctx = MatchFixtures.Context("enum ColorEnum { None }");
        Patterns.SuffixEnum(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NullDeref_ignores_braceless_if_body()
    {
        var ctx = MatchFixtures.Context("class C { void M(object x) { if (x == null) M(x); } }");
        Patterns.NullDeref(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NullDeref_ignores_condition_that_is_not_an_equality_check()
    {
        var ctx = MatchFixtures.Context("class C { void M(bool b) { if (b) { } } }");
        Patterns.NullDeref(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NullDeref_ignores_comparison_to_a_non_null_literal()
    {
        var ctx = MatchFixtures.Context("class C { void M(int x) { if (x == 5) { } } }");
        Patterns.NullDeref(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LoopBound_ignores_for_loop_without_a_condition()
    {
        var ctx = MatchFixtures.Context("class C { void M() { for (var i = 0; ; i++) { } } }");
        Patterns.LoopBound(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void AllocDos_ignores_array_creation_with_no_parameter_derived_size()
    {
        var ctx = MatchFixtures.Context("class C { void M() { var local = 5; var x = new int[local]; } }");
        Patterns.AllocDos(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void AllocDos_ignores_identifier_outside_a_method()
    {
        var ctx = MatchFixtures.Context("class C { C(int x) { var arr = new int[x]; } }");
        Patterns.AllocDos(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ConstantReturn_ignores_expression_bodied_method()
    {
        var ctx = MatchFixtures.Context("class C { int M() => 1; }");
        Patterns.ConstantReturn(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ConstantReturn_ignores_method_returning_different_literals()
    {
        var ctx = MatchFixtures.Context("class C { int M(bool b) { if (b) { return 1; } return 2; } }");
        Patterns.ConstantReturn(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ElseRequired_ignores_if_without_an_else()
    {
        var ctx = MatchFixtures.Context("class C { void M(bool b) { if (b) { } } }");
        Patterns.ElseRequired(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void IdenticalBranch_ignores_if_without_an_else()
    {
        var ctx = MatchFixtures.Context("class C { void M(bool b) { if (b) { } } }");
        Patterns.IdenticalBranch(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void OneStatement_ignores_lines_matching_for_get_and_set_accessors()
    {
        var ctx = MatchFixtures.Context(
            """
            class C
            {
                int P
                {
                    get;
                    set;
                }

                void M()
                {
                    for (var i = 0; i < 10; i++) { }
                }
            }
            """);
        Patterns.OneStatement(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestAssert_ignores_method_without_a_test_attribute()
    {
        var ctx = MatchFixtures.Context("class C { void M() { } }");
        Patterns.TestAssert(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestAssert_ignores_method_with_an_unrelated_attribute()
    {
        var ctx = MatchFixtures.Context("class C { [System.Obsolete] void M() { } }");
        Patterns.TestAssert(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestCase_ignores_class_without_a_test_suffix()
    {
        var ctx = MatchFixtures.Context("class Foo { }");
        Patterns.TestCase(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestSignature_ignores_method_without_a_test_attribute()
    {
        var ctx = MatchFixtures.Context("class C { void M() { } }");
        Patterns.TestSignature(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }
}
