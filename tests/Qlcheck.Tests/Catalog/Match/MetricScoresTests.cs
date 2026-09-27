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

    private static T FirstNode<T>(string source)
        where T : SyntaxNode =>
        CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<T>().First();
}
