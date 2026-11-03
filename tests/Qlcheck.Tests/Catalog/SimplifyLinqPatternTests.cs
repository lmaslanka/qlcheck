using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public sealed class SimplifyLinqPatternTests
{
    [Theory]
    [InlineData("Any")]
    [InlineData("Count")]
    [InlineData("LongCount")]
    [InlineData("First")]
    [InlineData("FirstOrDefault")]
    [InlineData("Single")]
    [InlineData("SingleOrDefault")]
    [InlineData("Last")]
    [InlineData("LastOrDefault")]
    public void Where_then_a_parameterless_call_is_reported_with_the_predicate_overload(string call)
    {
        var ctx = Context($"var r = items.Where(x => x > 1).{call}();");

        var finding = Assert.Single(ctx.Findings);
        Assert.EndsWith($"Use `{call}(predicate)` instead of `Where(predicate).{call}()`.", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Where_on_a_queryable_is_reported()
    {
        var ctx = Context("var r = items.AsQueryable().Where(x => x > 1).Any();");

        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void An_identity_select_is_reported()
    {
        var ctx = Context("var r = items.Select(x => x).ToList();");

        var finding = Assert.Single(ctx.Findings);
        Assert.EndsWith("Drop the `Select` that returns its argument unchanged.", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("var r = items.Where(x => x > 1).Any(x => x < 5);")]
    [InlineData("var r = items.Where((x, i) => i > 1).Any();")]
    [InlineData("var r = items.Where(x => x > 1).ToList();")]
    [InlineData("var r = items.Select(x => x + 1).ToList();")]
    [InlineData("var r = items.Select((x, i) => x).ToList();")]
    [InlineData("var r = new Box().Where(x => x > 1).Any();")]
    [InlineData("foreach (var x in items) { list.Add(x); }")]
    public void Other_shapes_are_not_reported(string statement)
    {
        Assert.Empty(Context(statement).Findings);
    }

    private static WalkContext Context(string statement)
    {
        var ctx = MatchFixtures.Context($$"""
            using System.Collections.Generic;
            using System.Linq;

            class Box
            {
                public Box Where(System.Func<int, bool> predicate) => this;

                public bool Any() => true;
            }

            class C
            {
                void M(int[] items, List<int> list)
                {
                    {{statement}}
                }
            }
            """);
        SimplifyLinqPattern.Apply(ctx, MatchFixtures.Id);
        return ctx;
    }
}
