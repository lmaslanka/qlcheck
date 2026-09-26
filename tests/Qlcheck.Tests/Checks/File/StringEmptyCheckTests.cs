namespace Qlcheck.Tests;

public class StringEmptyCheckTests
{
    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
    {
        var file = new SourceFile(path, source);
        return new StringEmptyCheck().Analyze(file, CSharpTrees.Parse(file));
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
        Assert.Equal(StringEmptyCheck.CheckId, finding.Check);
        Assert.Equal(StringEmptyCheck.Message, finding.Message);
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
