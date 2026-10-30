using Microsoft.CodeAnalysis;
using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class CompilationContextTests
{
    [Fact]
    public void ErrorTrees_hold_every_tree_with_an_error_diagnostic()
    {
        var unresolved = Tree("Unresolved.cs", "class A { Missing M() => null!; }");
        var partialWithoutBody = Tree("Partial.cs", "partial class B { public partial void M(); }");
        var clean = Tree("Clean.cs", "class C { }");
        var ctx = Context(unresolved, partialWithoutBody, clean);

        Assert.True(ctx.ErrorTrees.SetEquals([unresolved, partialWithoutBody]));
        Assert.Contains(ctx.Diagnostics, diagnostic => diagnostic.Id == "CS8795");
    }

    [Fact]
    public void Diagnostics_are_computed_once()
    {
        var ctx = Context(Tree("A.cs", "class A { }"));

        Assert.Equal(ctx.Diagnostics, ctx.Diagnostics);
    }

    private static SyntaxTree Tree(string path, string source) => CSharpTrees.Parse(new SourceFile(path, source));

    private static CompilationContext Context(params SyntaxTree[] trees) =>
        new(CSharpCompilations.Create("context", trees), trees.ToHashSet(), new Dictionary<string, string>());
}
