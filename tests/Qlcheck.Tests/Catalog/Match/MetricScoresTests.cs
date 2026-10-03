using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class MetricScoresTests
{
    [Fact]
    public void Coupling_counts_property_types()
    {
        var node = FirstNode<ClassDeclarationSyntax>("class C { public string Name { get; set; } }");
        Assert.Equal(1, MetricScores.Coupling(node));
    }

    [Fact]
    public void Methods_yields_property_declarations()
    {
        var ctx = MatchFixtures.Context("class C { int P { get; } }");
        Assert.Single(MetricScores.Methods(ctx));
    }

    [Fact]
    public void Cyclomatic_counts_a_flat_switch_expression_once_regardless_of_arm_count()
    {
        var node = FirstNode<MethodDeclarationSyntax>(
            """
            class C
            {
                int M(int value) => value switch
                {
                    1 => 1,
                    2 => 2,
                    3 => 3,
                    4 => 4,
                    5 => 5,
                    6 => 6,
                    7 => 7,
                    8 => 8,
                    9 => 9,
                    10 => 10,
                    11 => 11,
                    _ => 0,
                };
            }
            """);
        Assert.Equal(2, MetricScores.Cyclomatic(node));
    }

    [Fact]
    public void Cyclomatic_counts_a_simple_ternary_inside_a_flat_switch_arm_once()
    {
        var node = FirstNode<MethodDeclarationSyntax>(
            """
            class C
            {
                int M(int value, bool flag) => value switch
                {
                    1 => flag ? 1 : 2,
                    _ => 0,
                };
            }
            """);
        Assert.Equal(3, MetricScores.Cyclomatic(node));
    }

    [Fact]
    public void Cyclomatic_still_counts_real_control_flow_nested_inside_a_switch_expression_arm()
    {
        var node = FirstNode<MethodDeclarationSyntax>(
            """
            class C
            {
                int M(int value, bool flag) => value switch
                {
                    1 => Compute(() =>
                    {
                        if (flag)
                        {
                            return 1;
                        }

                        return 2;
                    }),
                    _ => 0,
                };

                static int Compute(System.Func<int> f) => f();
            }
            """);
        Assert.Equal(4, MetricScores.Cyclomatic(node));
    }

    [Fact]
    public void Cyclomatic_counts_a_flat_coalesce_initializer_once_regardless_of_member_count()
    {
        var node = FirstNode<MethodDeclarationSyntax>(
            """
            class Item
            {
                public string A, B, C, D, E;
            }

            class C
            {
                object M(Item item) => new
                {
                    A = item?.A ?? string.Empty,
                    B = item?.B ?? string.Empty,
                    C = item?.C ?? string.Empty,
                    D = item?.D ?? string.Empty,
                    E = item?.E ?? string.Empty,
                };
            }
            """);
        Assert.Equal(2, MetricScores.Cyclomatic(node));
    }

    [Fact]
    public void Cyclomatic_still_counts_a_branching_initializer_member_in_full()
    {
        var node = FirstNode<MethodDeclarationSyntax>(
            """
            class Item
            {
                public string A, B;
                public bool Flag;
            }

            class C
            {
                object M(Item item) => new
                {
                    A = item?.A ?? string.Empty,
                    B = item.Flag ? item.A : item.B,
                };
            }
            """);
        Assert.Equal(3, MetricScores.Cyclomatic(node));
    }

    private static T FirstNode<T>(string source)
        where T : SyntaxNode =>
        CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<T>().First();
}
