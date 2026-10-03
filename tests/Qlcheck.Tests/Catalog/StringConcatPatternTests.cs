using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class StringConcatPatternTests
{
    private const string NestedReplacement = "$\"x{a}{b}\"";

    private static IReadOnlyList<Finding> Run(string source)
    {
        var ctx = MatchFixtures.Context(source);
        StringConcatPattern.Apply(ctx, MatchFixtures.Id);
        return ctx.Findings;
    }

    [Fact]
    public void Reports_plus_with_string_literal()
    {
        var source = """
            class C
            {
                string M(string name) => "Hello " + name;
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(MatchFixtures.Id, finding.Check);
        Assert.Equal("$\"Hello {name}\"", finding.Replacement);
    }

    [Fact]
    public void Reports_one_finding_for_a_plus_chain()
    {
        var source = """
            class C
            {
                string M(string a, string b) => "x" + a + b;
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(NestedReplacement, finding.Replacement);
    }

    [Fact]
    public void Ignores_numeric_addition()
    {
        var source = """
            class C
            {
                int M() => 1 + 2;
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_unknown_identifier_addition()
    {
        var source = """
            class C
            {
                int M(int a, int b) => a + b;
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_string_concat_call()
    {
        var source = """
            class C
            {
                string M(string name) => string.Concat("Hello ", name);
            }
            """;

        Assert.Single(Run(source));
    }

    [Fact]
    public void Skips_sql_concatenation()
    {
        var source = """
            class C
            {
                string M() => "NULLIF(" + column.SqlExpression + ", '') " + sqlDirection + " NULLS LAST";
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_const_concatenation()
    {
        var source = """
            class C
            {
                const string Name = "a" + "b";
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_sql_concatenation_via_concat_call()
    {
        var source = """
            class C
            {
                string M(string dir) => string.Concat("SELECT * FROM t WHERE x = ", dir);
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_sql_concatenation_starting_with_an_interpolated_segment()
    {
        var source = """
            class C
            {
                string M(string col, string suffix) => $"SELECT {col} " + suffix;
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_parenthesized_concatenation()
    {
        var source = """
            class C
            {
                string M(string a) => ("x" + a);
            }
            """;

        Assert.Single(Run(source));
    }

    [Fact]
    public void Reports_concatenation_with_a_parenthesized_operand()
    {
        var source = """
            class C
            {
                string M(string name) => ("Hello ") + name;
            }
            """;

        Assert.Single(Run(source));
    }

    [Fact]
    public void Ignores_unqualified_concat_call()
    {
        var source = """
            class C
            {
                string M(string a, string b) => Concat(a, b);
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_invocation_whose_callee_is_not_a_name()
    {
        var source = """
            class C
            {
                string M(string a, string b) => funcs[0](a, b);
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_string_dot_net_type_concat_call()
    {
        var source = """
            class C
            {
                string M(string name) => String.Concat("Hello ", name);
            }
            """;

        Assert.Single(Run(source));
    }

    [Fact]
    public void Reports_fully_qualified_string_concat_call()
    {
        var source = """
            class C
            {
                string M(string name) => System.String.Concat("Hello ", name);
            }
            """;

        Assert.Single(Run(source));
    }

    [Fact]
    public void Skips_concatenation_inside_an_attribute()
    {
        var source = """
            class C
            {
                [System.Obsolete("a" + "b")]
                void M() { }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_local_const_concatenation()
    {
        var source = """
            class C
            {
                void M()
                {
                    const string x = "a" + "b";
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_concatenation_with_an_interpolated_segment_without_a_replacement()
    {
        var source = """
            class C
            {
                string M(string a) => $"x{a}" + "y";
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Null(finding.Replacement);
    }

    [Fact]
    public void Escapes_special_characters_in_the_suggested_replacement()
    {
        var source = """
            class C
            {
                string M(string name) => "line1\n\ttab\\quote\"brace{x}end\r" + name;
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Contains("\\n", finding.Replacement);
        Assert.Contains("\\t", finding.Replacement);
        Assert.Contains("\\\\", finding.Replacement);
        Assert.Contains("\\\"", finding.Replacement);
        Assert.Contains("{{", finding.Replacement);
        Assert.Contains("}}", finding.Replacement);
        Assert.Contains("\\r", finding.Replacement);
    }
}
