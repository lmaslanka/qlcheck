using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class StylePatternsTests
{
    [Fact]
    public void AbstractMixed_ignores_abstract_class_with_no_methods()
    {
        var ctx = MatchFixtures.Context("abstract class C { public abstract int P { get; } }");
        StylePatterns.AbstractMixed(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void VirtualFromCtor_ignores_constructor_outside_a_class()
    {
        var ctx = MatchFixtures.Context("struct S { public S() { } }");
        StylePatterns.VirtualFromCtor(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void DefaultMiddle_ignores_switch_with_few_sections()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M(int x) { switch (x) { case 1: break; default: break; } } }");
        StylePatterns.DefaultMiddle(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void AssignedNotReadonly_ignores_field_outside_a_class()
    {
        var ctx = MatchFixtures.Context("struct S { int x; }");
        StylePatterns.AssignedNotReadonly(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void IntDivToFloat_ignores_declarator_without_an_initializer()
    {
        var ctx = MatchFixtures.Context("class C { void M() { float x; } }");
        StylePatterns.IntDivToFloat(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ForWrongWay_ignores_for_loop_without_an_incrementor()
    {
        var ctx = MatchFixtures.Context("class C { void M() { for (var i = 0; i < 10;) { } } }");
        StylePatterns.ForWrongWay(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ForNoCounter_ignores_for_loop_without_a_declaration()
    {
        var ctx = MatchFixtures.Context("class C { void M(int i) { for (i = 0; i < 10; i++) { } } }");
        StylePatterns.ForNoCounter(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ForBoundChanges_ignores_for_loop_without_a_condition()
    {
        var ctx = MatchFixtures.Context("class C { void M() { for (var i = 0; ; i++) { } } }");
        StylePatterns.ForBoundChanges(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NestedSameIf_ignores_braceless_if_body()
    {
        var ctx = MatchFixtures.Context("class C { void M(bool b) { if (b) System.Console.WriteLine(); } }");
        StylePatterns.NestedSameIf(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void StaticInGeneric_ignores_instance_field()
    {
        var ctx = MatchFixtures.Context("class C<T> { int x; }");
        StylePatterns.StaticInGeneric(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void StaticWriteInstance_ignores_static_method()
    {
        var ctx = MatchFixtures.Context("class C { static int x; static void M() { x = 1; } }");
        StylePatterns.StaticWriteInstance(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void StaticWriteCtor_ignores_constructor_outside_a_class()
    {
        var ctx = MatchFixtures.Context("struct S { public S() { } }");
        StylePatterns.StaticWriteCtor(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void StaticNoInline_ignores_instance_field()
    {
        var ctx = MatchFixtures.Context("class C { int x; }");
        StylePatterns.StaticNoInline(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void RedundantReturn_reports_trailing_bare_return()
    {
        var ctx = MatchFixtures.Context("class C { void M() { System.Console.WriteLine(); return; } }");
        StylePatterns.RedundantReturn(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void RedundantReturn_ignores_return_that_is_not_the_last_block_statement()
    {
        var ctx = MatchFixtures.Context("class C { void M(bool b) { if (b) return; System.Console.WriteLine(); } }");
        StylePatterns.RedundantReturn(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void SelfArg_ignores_call_without_a_member_receiver()
    {
        var ctx = MatchFixtures.Context("class C { void M(int x) { Foo(x); } }");
        StylePatterns.SelfArg(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ReplaceElement_ignores_assignment_to_a_plain_variable()
    {
        var ctx = MatchFixtures.Context("class C { void M(int x) { x = 1; } }");
        StylePatterns.ReplaceElement(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ReplaceElement_ignores_element_assignment_with_a_non_literal_index()
    {
        var ctx = MatchFixtures.Context("class C { void M(int[] arr, int i, int x) { arr[i] = x; } }");
        StylePatterns.ReplaceElement(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ForeachAdd_ignores_braceless_foreach_body()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M(System.Collections.Generic.List<int> list, int[] xs) { foreach (var x in xs) list.Add(x); } }");
        StylePatterns.ForeachAdd(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void EqualsWithoutComparison_ignores_unqualified_equals_call()
    {
        var ctx = MatchFixtures.Context("class C { void M(object x) { Equals(x); } }");
        StylePatterns.EqualsWithoutComparison(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }
}
