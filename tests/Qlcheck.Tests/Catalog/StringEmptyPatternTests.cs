using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class StringEmptyPatternTests
{
    private static IReadOnlyList<Finding> Run(string source)
    {
        var ctx = MatchFixtures.Context(source);
        StringEmptyPattern.Apply(ctx, MatchFixtures.Id);
        return ctx.Findings;
    }

    [Fact]
    public void Reports_empty_string_literal()
    {
        var source = """
            class C
            {
                string M() => "";
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(MatchFixtures.Id, finding.Check);
        Assert.Equal("string.Empty", finding.Replacement);
    }

    [Fact]
    public void Skips_const_and_default_parameter()
    {
        var source = """
            class C
            {
                const string Name = "";
                void M(string s = "") { }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_local_const()
    {
        var source = """
            class C
            {
                void M()
                {
                    const string Name = "";
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_attribute_and_switch_case()
    {
        var source = """
            class C
            {
                [System.Obsolete("")]
                string M(string s)
                {
                    switch (s)
                    {
                        case "":
                            return s;
                    }
                    return s;
                }
            }
            """;

        Assert.Empty(Run(source));
    }
}
