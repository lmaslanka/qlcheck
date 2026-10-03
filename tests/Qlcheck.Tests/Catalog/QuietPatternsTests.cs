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
    public void ReturnUsing_ignores_a_return_of_an_unrelated_value_inside_a_using_block()
    {
        var ctx = MatchFixtures.Context(
            """
            class Holder
            {
                object M()
                {
                    using (var item = new object())
                    {
                        var result = Compute();
                        return result;
                    }
                }

                object Compute() => new object();
            }
            """);
        QuietPatterns.ReturnUsing(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ReturnUsing_ignores_a_non_disposable_member_of_the_using_resource()
    {
        var ctx = MatchFixtures.Context(
            """
            class Holder
            {
                long M()
                {
                    using (var item = new System.IO.MemoryStream())
                    {
                        return item.Length;
                    }
                }
            }
            """);
        QuietPatterns.ReturnUsing(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ReturnUsing_reports_a_disposable_member_of_the_using_resource()
    {
        var ctx = MatchFixtures.Context(
            """
            class Holder
            {
                System.IO.Stream M()
                {
                    using (var reader = new System.IO.StreamReader(new System.IO.MemoryStream()))
                    {
                        return reader.BaseStream;
                    }
                }
            }
            """);
        QuietPatterns.ReturnUsing(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
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
    public void ReadonlyAssign_ignores_direct_assignment_to_a_readonly_field()
    {
        var ctx = MatchFixtures.Context(
            "interface IFoo { } class Holder { readonly IFoo foo; Holder(IFoo foo) { this.foo = foo; } }");
        QuietPatterns.ReadonlyAssign(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ReadonlyAssign_ignores_property_write_on_a_readonly_reference_type_field()
    {
        var ctx = MatchFixtures.Context(
            "class Foo { public int Count { get; set; } } class Holder { readonly Foo foo = new Foo(); void M() { foo.Count = 1; } }");
        QuietPatterns.ReadonlyAssign(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ReadonlyAssign_ignores_property_write_on_a_readonly_interface_typed_field()
    {
        var ctx = MatchFixtures.Context(
            "interface ICounter { int Count { get; set; } } class Holder { readonly ICounter counter; Holder(ICounter counter) { this.counter = counter; } void M() { counter.Count = 1; } }");
        QuietPatterns.ReadonlyAssign(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ReadonlyAssign_reports_property_write_on_a_readonly_unconstrained_generic_field()
    {
        var ctx = MatchFixtures.Context(
            "interface ICounter { int Count { get; set; } } class Holder<T> where T : ICounter { readonly T state; Holder(T state) { this.state = state; } void M() { state.Count = 1; } }");
        QuietPatterns.ReadonlyAssign(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
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
