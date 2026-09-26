namespace Qlcheck.Tests;

public class StringConcatCheckTests
{
    private const string NestedReplacement = "$\"x{a}{b}\"";
    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
    {
        var file = new SourceFile(path, source);
        return new StringConcatCheck().Analyze(file, CSharpTrees.Parse(file));
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
        Assert.Equal(StringConcatCheck.CheckId, finding.Check);
        Assert.Equal(StringConcatCheck.Message, finding.Message);
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

        var finding = Assert.Single(Run(source));
        Assert.Equal(StringConcatCheck.Message, finding.Message);
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
}
