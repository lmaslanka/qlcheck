namespace Qlcheck.Tests;

public class SwitchPatternCheckTests
{
    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
    {
        var file = new SourceFile(path, source);
        return new SwitchPatternCheck().Analyze(file, file.Tree);
    }

    [Fact]
    public void Reports_return_switch()
    {
        var source = """
            class C
            {
                int M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            return 10;
                        case 2:
                            return 20;
                        default:
                            return 0;
                    }
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal("switch-pattern", finding.Check);
        Assert.Equal(SwitchPatternCheck.Message, finding.Message);
        Assert.Null(finding.Replacement);
    }

    [Fact]
    public void Reports_assignment_switch()
    {
        var source = """
            class C
            {
                void M(int x)
                {
                    int y;
                    switch (x)
                    {
                        case 1:
                            y = 10;
                            break;
                        default:
                            y = 0;
                            break;
                    }
                }
            }
            """;

        Assert.Single(Run(source));
    }

    [Fact]
    public void Ignores_multi_statement_arms()
    {
        var source = """
            class C
            {
                int M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            Console.WriteLine(x);
                            return 10;
                        default:
                            return 0;
                    }
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_goto_case()
    {
        var source = """
            class C
            {
                int M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            goto case 2;
                        case 2:
                            return 0;
                    }
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_switch_expression()
    {
        var source = """
            class C
            {
                int M(int x) => x switch
                {
                    1 => 10,
                    _ => 0
                };
            }
            """;

        Assert.Empty(Run(source));
    }
}
