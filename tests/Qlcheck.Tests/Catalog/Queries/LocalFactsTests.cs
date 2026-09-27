using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Tests;

public class LocalFactsTests
{
    [Fact]
    public void Declarators_is_empty_for_non_local_declaration_node()
    {
        Assert.Empty(LocalFacts.Declarators(Expression("1")));
    }

    [Fact]
    public void Declarators_yields_each_declared_variable()
    {
        var node = FirstNode<LocalDeclarationStatementSyntax>("class C { void M() { int a = 1, b = 2; } }");
        Assert.Equal(["a", "b"], LocalFacts.Declarators(node).Select(d => d.Identifier.Text));
    }

    [Fact]
    public void Unused_is_false_when_not_inside_a_method()
    {
        var node = FirstNode<VariableDeclaratorSyntax>("class C { C() { var x = 1; } }");
        Assert.False(LocalFacts.Unused(node));
    }

    [Fact]
    public void Unused_is_true_when_never_referenced_again()
    {
        var node = FirstNode<VariableDeclaratorSyntax>("class C { void M() { var x = 1; } }");
        Assert.True(LocalFacts.Unused(node));
    }

    [Fact]
    public void PrivateUnused_is_false_when_name_cannot_be_determined()
    {
        var node = FirstNode<FieldDeclarationSyntax>("class C { private int Field; }");
        Assert.False(LocalFacts.PrivateUnused(node));
    }

    [Fact]
    public void PrivateUnused_is_false_when_node_has_no_enclosing_type()
    {
        var method = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                "M")
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
            .WithBody(SyntaxFactory.Block());
        var unit = SyntaxFactory.CompilationUnit().WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method));
        Assert.False(LocalFacts.PrivateUnused(unit.Members[0]));
    }

    [Fact]
    public void PrivateUnused_is_false_when_used_from_another_partial_declaration()
    {
        var (node, model) = PartialMethod(
            declaringPart: "public partial class Widget { private void Helper() { } }",
            otherPart: "public partial class Widget { void M() { Helper(); } }",
            methodName: "Helper");
        Assert.False(LocalFacts.PrivateUnused(node, model));
    }

    [Fact]
    public void PrivateUnused_is_true_when_unused_across_all_partial_declarations()
    {
        var (node, model) = PartialMethod(
            declaringPart: "public partial class Widget { private void Helper() { } }",
            otherPart: "public partial class Widget { void M() { } }",
            methodName: "Helper");
        Assert.True(LocalFacts.PrivateUnused(node, model));
    }

    private static (SyntaxNode Node, SemanticModel Model) PartialMethod(
        string declaringPart,
        string otherPart,
        string methodName)
    {
        var declaringTree = CSharpSyntaxTree.ParseText(declaringPart);
        var otherTree = CSharpSyntaxTree.ParseText(otherPart);
        var compilation = CSharpCompilation.Create(
            "PartialTest",
            [declaringTree, otherTree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(declaringTree);
        var node = declaringTree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == methodName);
        return (node, model);
    }

    private static ExpressionSyntax Expression(string expression)
    {
        var source = $"class C {{ void M() {{ var _ = {expression}; }} }}";
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<EqualsValueClauseSyntax>().First().Value;
    }

    private static T FirstNode<T>(string source)
        where T : SyntaxNode =>
        CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<T>().First();
}
