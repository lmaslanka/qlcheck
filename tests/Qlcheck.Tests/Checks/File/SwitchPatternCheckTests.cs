namespace Qlcheck.Tests;

public class SwitchPatternCheckTests
{
    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
    {
        var file = new SourceFile(path, source);
        return new SwitchPatternCheck().Analyze(file, CSharpTrees.Parse(file));
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
        Assert.Equal(SwitchPatternCheck.CheckId, finding.Check);
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
    public void Ignores_switch_with_too_few_sections()
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
                    }

                    return 0;
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_throwing_arm_mixed_with_a_return_arm()
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
                        default:
                            throw new System.Exception();
                    }
                }
            }
            """;

        Assert.Single(Run(source));
    }

    [Fact]
    public void Ignores_arms_with_mismatched_kinds()
    {
        var source = """
            class C
            {
                int y;

                int M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            return 10;
                        default:
                            y = 0;
                            break;
                    }

                    return 0;
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_assignment_arms_with_different_targets()
    {
        var source = """
            class C
            {
                int a;
                int b;

                void M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            a = 1;
                            break;
                        default:
                            b = 2;
                            break;
                    }
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_arm_that_is_not_return_throw_or_assignment()
    {
        var source = """
            class C
            {
                void M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            Foo();
                            break;
                        default:
                            Foo();
                            break;
                    }
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_switch_with_a_braced_single_statement_arm()
    {
        var source = """
            class C
            {
                int M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            {
                                return 10;
                            }
                        default:
                            return 0;
                    }
                }
            }
            """;

        Assert.Single(Run(source));
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
