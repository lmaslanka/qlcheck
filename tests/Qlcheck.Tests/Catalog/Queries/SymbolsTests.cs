using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp;
using Qlcheck.Languages.CSharp.Catalog;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Tests;

public class SymbolsTests
{
    [Fact]
    public void SymbolOf_resolves_the_referenced_symbol()
    {
        var ctx = CreateContext("class C { void M() { M(); } }");
        var invocation = ctx.Tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var symbol = Symbols.SymbolOf(ctx, invocation.Expression);
        Assert.NotNull(symbol);
        Assert.Equal("M", symbol.Name);
    }

    [Fact]
    public void Implements_is_false_for_null_type()
    {
        Assert.False(Symbols.Implements(null, "IDisposable"));
    }

    [Fact]
    public void Implements_is_true_when_type_itself_matches()
    {
        var compilation = CSharpCompilations.Create("fixture", []);
        var type = compilation.GetTypeByMetadataName("System.IDisposable");
        Assert.True(Symbols.Implements(type, "IDisposable"));
    }

    [Fact]
    public void IsDisposable_is_false_for_non_creation_node()
    {
        var ctx = CreateContext("class C { void M() { var x = 1; } }");
        var node = ctx.Tree.GetRoot().DescendantNodes().OfType<EqualsValueClauseSyntax>().First().Value;
        Assert.False(Symbols.IsDisposable(ctx, node));
    }

    private static WalkContext CreateContext(string source)
    {
        var file = new SourceFile("Repo.cs", source);
        var tree = CSharpTrees.Parse(file);
        var compilation = CSharpCompilations.Create("fixture", [tree]);
        var model = compilation.GetSemanticModel(tree);
        return WalkContext.Create(file, tree, model, new Dictionary<string, string>());
    }
}
