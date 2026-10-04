using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Tests;

public class ShapesTests
{
    [Fact]
    public void ReturnType_returns_method_return_type()
    {
        var node = FirstNode<MethodDeclarationSyntax>("class C { int M() => 1; }");
        Assert.Equal("int", Shapes.ReturnType(node));
    }

    [Fact]
    public void ReturnType_returns_property_type()
    {
        var node = FirstNode<PropertyDeclarationSyntax>("class C { int P => 1; }");
        Assert.Equal("int", Shapes.ReturnType(node));
    }

    [Fact]
    public void ReturnType_returns_empty_for_unhandled_node()
    {
        var node = FirstNode<FieldDeclarationSyntax>("class C { int F; }");
        Assert.Equal(string.Empty, Shapes.ReturnType(node));
    }

    [Fact]
    public void IsEmptyBlock_is_false_for_non_block_node()
    {
        var node = FirstNode<MethodDeclarationSyntax>("class C { void M() { } }");
        Assert.False(Shapes.IsEmptyBlock(node));
    }

    [Fact]
    public void IsEmptyMethod_is_false_for_non_method_node()
    {
        var node = FirstNode<FieldDeclarationSyntax>("class C { int F; }");
        Assert.False(Shapes.IsEmptyMethod(node));
    }

    [Fact]
    public void IsEmptyMethod_is_false_when_body_is_null()
    {
        var node = FirstNode<MethodDeclarationSyntax>("class C { int M() => 1; }");
        Assert.False(Shapes.IsEmptyMethod(node));
    }

    [Fact]
    public void Body_returns_accessor_body()
    {
        var node = FirstNode<AccessorDeclarationSyntax>("class C { int P { get { return 1; } } }");
        Assert.Equal(node.Body, Shapes.Body(node));
    }

    [Fact]
    public void Body_returns_null_for_unhandled_node()
    {
        var node = FirstNode<BinaryExpressionSyntax>("class C { void M() { var x = 1 + 1; } }");
        Assert.Null(Shapes.Body(node));
    }

    [Fact]
    public void FileLines_returns_zero_for_empty_text()
    {
        Assert.Equal(0, Shapes.FileLines(string.Empty));
    }

    [Fact]
    public void HasModifier_reads_accessor_modifiers()
    {
        var node = FirstNode<AccessorDeclarationSyntax>("class C { int P { get; private set; } }", 1);
        Assert.True(Shapes.HasModifier(node, SyntaxKind.PrivateKeyword));
    }

    [Fact]
    public void HasModifier_reads_parameter_modifiers()
    {
        var node = FirstNode<ParameterSyntax>("class C { void M(ref int x) { } }");
        Assert.True(Shapes.HasModifier(node, SyntaxKind.RefKeyword));
    }

    [Fact]
    public void HasModifier_is_false_for_unhandled_node()
    {
        var node = FirstNode<BinaryExpressionSyntax>("class C { void M() { var x = 1 + 1; } }");
        Assert.False(Shapes.HasModifier(node, SyntaxKind.PrivateKeyword));
    }

    [Fact]
    public void ParameterCount_reads_constructor_parameters()
    {
        var node = FirstNode<ConstructorDeclarationSyntax>("class C { C(int a, int b) { } }");
        Assert.Equal(2, Shapes.ParameterCount(node));
    }

    [Fact]
    public void ParameterCount_reads_local_function_parameters()
    {
        var node = FirstNode<LocalFunctionStatementSyntax>("class C { void M() { void Local(int a) { } } }");
        Assert.Equal(1, Shapes.ParameterCount(node));
    }

    [Fact]
    public void ParameterCount_is_zero_for_unhandled_node()
    {
        var node = FirstNode<PropertyDeclarationSyntax>("class C { int P => 1; }");
        Assert.Equal(0, Shapes.ParameterCount(node));
    }

    [Fact]
    public void IsStaticConstantsHolderClass_is_true_for_const_and_readonly_static_fields()
    {
        var node = FirstNode<ClassDeclarationSyntax>(
            "static class Globals { public const int Max = 10; public static readonly int Min = 0; }");
        Assert.True(Shapes.IsStaticConstantsHolderClass(node));
    }

    [Fact]
    public void IsStaticConstantsHolderClass_is_false_for_a_mutable_static_field()
    {
        var node = FirstNode<ClassDeclarationSyntax>(
            "static class Globals { public static int Counter; public const int Max = 10; }");
        Assert.False(Shapes.IsStaticConstantsHolderClass(node));
    }

    [Fact]
    public void IsStaticConstantsHolderClass_is_false_for_a_static_property_with_a_setter()
    {
        var node = FirstNode<ClassDeclarationSyntax>(
            "static class Globals { public static int Counter { get; set; } }");
        Assert.False(Shapes.IsStaticConstantsHolderClass(node));
    }

    private static T FirstNode<T>(string source, int skip = 0)
        where T : SyntaxNode =>
        CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<T>().Skip(skip).First();
}
