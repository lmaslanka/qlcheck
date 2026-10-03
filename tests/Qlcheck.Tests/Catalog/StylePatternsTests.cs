using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class StylePatternsTests
{
    [Fact]
    public void BoolCompare_reports_comparison_against_a_plain_bool()
    {
        var ctx = MatchFixtures.Context("class C { void M(bool flag) { var x = flag == true; } }");
        StylePatterns.BoolCompare(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void BoolCompare_ignores_comparison_against_a_nullable_bool()
    {
        var ctx = MatchFixtures.Context("class C { void M(bool? flag) { var x = flag == true; } }");
        StylePatterns.BoolCompare(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ForeachAdd_ignores_a_loop_body_that_awaits_before_adding()
    {
        var ctx = MatchFixtures.Context(
            """
            class Holder
            {
                async System.Threading.Tasks.Task M(int[] source, System.Collections.Generic.List<int> items)
                {
                    foreach (var item in source)
                    {
                        if (await Check(item))
                        {
                            items.Add(item);
                        }
                    }
                }

                System.Threading.Tasks.Task<bool> Check(int item) => System.Threading.Tasks.Task.FromResult(true);
            }
            """);
        StylePatterns.ForeachAdd(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

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

    [Fact]
    public void PublicConst_ignores_static_class_with_only_const_fields()
    {
        var ctx = MatchFixtures.Context("static class C { public const int N = 1; public const int M = 2; }");
        StylePatterns.PublicConst(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void PublicConst_flags_static_class_with_const_and_other_members()
    {
        var ctx = MatchFixtures.Context("static class C { public const int N = 1; public static void M() { } }");
        StylePatterns.PublicConst(ctx, MatchFixtures.Id);
        Assert.NotEmpty(ctx.Findings);
    }

    [Fact]
    public void PublicConst_flags_non_static_class_with_const_fields()
    {
        var ctx = MatchFixtures.Context("class C { public const int N = 1; }");
        StylePatterns.PublicConst(ctx, MatchFixtures.Id);
        Assert.NotEmpty(ctx.Findings);
    }

    [Fact]
    public void PublicConst_ignores_const_used_in_a_case_label()
    {
        var ctx = MatchFixtures.Context(
            "class C { public const string A = \"a\"; void M(string s) { switch (s) { case A: break; } } }");
        StylePatterns.PublicConst(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void PublicConst_ignores_const_used_in_an_is_pattern()
    {
        var ctx = MatchFixtures.Context(
            "class C { public const string A = \"a\"; public const string B = \"b\"; bool M(string s) => s is A or B; }");
        StylePatterns.PublicConst(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void PublicConst_ignores_const_used_in_an_attribute_argument()
    {
        var ctx = MatchFixtures.Context(
            "class C { public const string A = \"a\"; [System.Obsolete(A)] void M() { } }");
        StylePatterns.PublicConst(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void PublicConst_flags_const_only_read_normally()
    {
        var ctx = MatchFixtures.Context(
            "class C { public const string A = \"a\"; string M() => A; }");
        StylePatterns.PublicConst(ctx, MatchFixtures.Id);
        Assert.NotEmpty(ctx.Findings);
    }

    [Fact]
    public void AssignedNotReadonly_flags_a_field_only_ever_assigned_in_the_constructor()
    {
        var ctx = MatchFixtures.Context("class C { private int n; C(int value) { n = value; } }");
        StylePatterns.AssignedNotReadonly(ctx, MatchFixtures.Id);
        Assert.NotEmpty(ctx.Findings);
    }

    [Fact]
    public void AssignedNotReadonly_ignores_a_field_mutated_with_a_compound_assignment_later()
    {
        var ctx = MatchFixtures.Context(
            "class C { private int n; C(int value) { n = value; } void Advance(int delta) { n += delta; } }");
        StylePatterns.AssignedNotReadonly(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void AssignedNotReadonly_ignores_a_field_incremented_later()
    {
        var ctx = MatchFixtures.Context(
            "class C { private int n; C(int value) { n = value; } void Advance() { n++; } }");
        StylePatterns.AssignedNotReadonly(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }
}
