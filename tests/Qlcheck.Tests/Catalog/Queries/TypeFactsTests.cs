using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Tests;

public class TypeFactsTests
{
    [Fact]
    public void BaseType_is_empty_for_unhandled_node()
    {
        var node = FirstNode<MethodDeclarationSyntax>("class C { void M() { } }");
        Assert.Equal(string.Empty, TypeFacts.BaseType(node));
    }

    [Fact]
    public void BaseType_is_empty_when_base_list_has_no_types()
    {
        var baseList = SyntaxFactory.BaseList(SyntaxFactory.SeparatedList<BaseTypeSyntax>());
        var classDeclaration = SyntaxFactory.ClassDeclaration("C").WithBaseList(baseList);
        Assert.Equal(string.Empty, TypeFacts.BaseType(classDeclaration));
    }

    [Fact]
    public void IsPascal_is_false_for_a_name_with_disallowed_characters()
    {
        Assert.False(TypeFacts.IsPascal("Foo Bar"));
    }

    [Fact]
    public void IsAsyncVoid_is_false_for_non_method_node()
    {
        Assert.False(TypeFacts.IsAsyncVoid(Expression("1")));
    }

    [Fact]
    public void FieldNotPrivate_is_false_for_non_field_node()
    {
        Assert.False(TypeFacts.FieldNotPrivate(Expression("1")));
    }

    [Fact]
    public void Named_matches_declared_name()
    {
        var node = FirstNode<BaseTypeDeclarationSyntax>("class Foo { }");
        Assert.True(TypeFacts.Named(node, "Foo"));
        Assert.False(TypeFacts.Named(node, "Bar"));
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
