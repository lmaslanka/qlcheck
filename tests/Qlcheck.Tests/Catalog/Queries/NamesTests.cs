using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Tests;

public class NamesTests
{
    [Fact]
    public void Invocation_returns_empty_for_non_invocation()
    {
        Assert.Equal(string.Empty, Names.Invocation(Expression("1")));
    }

    [Fact]
    public void Invocation_returns_identifier_name()
    {
        Assert.Equal("M", Names.Invocation(Expression("M()")));
    }

    [Fact]
    public void InvocationType_returns_empty_for_non_invocation()
    {
        Assert.Equal(string.Empty, Names.InvocationType(Expression("1")));
    }

    [Fact]
    public void InvocationType_returns_empty_when_callee_is_not_member_access()
    {
        Assert.Equal(string.Empty, Names.InvocationType(Expression("M()")));
    }

    [Fact]
    public void InvocationType_returns_receiver_type_name()
    {
        Assert.Equal("Console", Names.InvocationType(Expression("Console.WriteLine()")));
    }

    [Fact]
    public void InvocationType_returns_member_access_receiver_name()
    {
        Assert.Equal("b", Names.InvocationType(Expression("a.b.Method()")));
    }

    [Fact]
    public void Creation_returns_empty_for_non_creation()
    {
        Assert.Equal(string.Empty, Names.Creation(Expression("1")));
    }

    [Fact]
    public void Creation_returns_qualified_type_name()
    {
        Assert.Equal("StringBuilder", Names.Creation(Expression("new System.Text.StringBuilder()")));
    }

    [Fact]
    public void Member_returns_empty_for_non_member_access()
    {
        Assert.Equal(string.Empty, Names.Member(Expression("1")));
    }

    [Fact]
    public void Member_returns_member_name()
    {
        Assert.Equal("Field", Names.Member(Expression("obj.Field")));
    }

    [Fact]
    public void MemberType_returns_empty_for_non_member_access()
    {
        Assert.Equal(string.Empty, Names.MemberType(Expression("1")));
    }

    [Fact]
    public void MemberType_returns_receiver_type_name()
    {
        Assert.Equal("Console", Names.MemberType(Expression("Console.Out")));
    }

    [Fact]
    public void Attribute_returns_empty_for_non_attribute()
    {
        Assert.Equal(string.Empty, Names.Attribute(Expression("1")));
    }

    [Fact]
    public void Attribute_strips_attribute_suffix()
    {
        Assert.Equal("Sample", Names.Attribute(FirstAttribute("[SampleAttribute] class C { }")));
    }

    [Fact]
    public void Attribute_keeps_name_without_attribute_suffix()
    {
        Assert.Equal("Sample", Names.Attribute(FirstAttribute("[Sample] class C { }")));
    }

    [Fact]
    public void Declared_returns_type_name()
    {
        var node = FirstNode<BaseTypeDeclarationSyntax>("class Foo { }");
        Assert.Equal("Foo", Names.Declared(node));
    }

    [Fact]
    public void Declared_returns_method_name()
    {
        var node = FirstNode<MethodDeclarationSyntax>("class C { void M() { } }");
        Assert.Equal("M", Names.Declared(node));
    }

    [Fact]
    public void Declared_returns_property_name()
    {
        var node = FirstNode<PropertyDeclarationSyntax>("class C { int P { get; } }");
        Assert.Equal("P", Names.Declared(node));
    }

    [Fact]
    public void Declared_returns_enum_member_name()
    {
        var node = FirstNode<EnumMemberDeclarationSyntax>("enum E { A }");
        Assert.Equal("A", Names.Declared(node));
    }

    [Fact]
    public void Declared_returns_parameter_name()
    {
        var node = FirstNode<ParameterSyntax>("class C { void M(int x) { } }");
        Assert.Equal("x", Names.Declared(node));
    }

    [Fact]
    public void Declared_returns_variable_name()
    {
        var node = FirstNode<VariableDeclaratorSyntax>("class C { void M() { var x = 1; } }");
        Assert.Equal("x", Names.Declared(node));
    }

    [Fact]
    public void Declared_returns_empty_for_unhandled_node()
    {
        var node = FirstNode<BinaryExpressionSyntax>("class C { void M() { var x = 1 + 1; } }");
        Assert.Equal(string.Empty, Names.Declared(node));
    }

    [Fact]
    public void Simple_returns_generic_name()
    {
        var node = ((InvocationExpressionSyntax)Expression("M<int>()")).Expression;
        Assert.Equal("M", Names.Simple(node));
    }

    [Fact]
    public void Simple_returns_member_binding_name()
    {
        var node = FirstNode<MemberBindingExpressionSyntax>("class C { void M() { var x = obj?.Field; } }");
        Assert.Equal("Field", Names.Simple(node));
    }

    [Fact]
    public void Simple_returns_empty_for_unhandled_node()
    {
        Assert.Equal(string.Empty, Names.Simple(Expression("1")));
    }

    [Fact]
    public void TypeText_returns_generic_type_name()
    {
        var node = ((ObjectCreationExpressionSyntax)Expression("new List<int>()")).Type;
        Assert.Equal("List", Names.TypeText(node));
    }

    [Fact]
    public void TypeText_returns_qualified_type_name()
    {
        var node = ((ObjectCreationExpressionSyntax)Expression("new System.Text.StringBuilder()")).Type;
        Assert.Equal("StringBuilder", Names.TypeText(node));
    }

    [Fact]
    public void TypeText_returns_alias_qualified_name()
    {
        var node = FirstNode<AliasQualifiedNameSyntax>("class C { void M() { global::System.Console.WriteLine(); } }");
        Assert.Equal("System", Names.TypeText(node));
    }

    [Fact]
    public void TypeText_returns_predefined_keyword()
    {
        var node = FirstNode<PredefinedTypeSyntax>("class C { int F = 1; }");
        Assert.Equal("int", Names.TypeText(node));
    }

    [Fact]
    public void TypeText_unwraps_nullable_type()
    {
        var node = FirstNode<NullableTypeSyntax>("class C { void M() { int? x = null; } }");
        Assert.Equal("int", Names.TypeText(node));
    }

    [Fact]
    public void TypeText_returns_empty_for_unhandled_node()
    {
        Assert.Equal(string.Empty, Names.TypeText(Expression("1")));
    }

    private static ExpressionSyntax Expression(string expression)
    {
        var source = $"class C {{ void M() {{ var _ = {expression}; }} }}";
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<EqualsValueClauseSyntax>().First().Value;
    }

    private static AttributeSyntax FirstAttribute(string source) =>
        CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<AttributeSyntax>().First();

    private static T FirstNode<T>(string source)
        where T : SyntaxNode =>
        CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<T>().First();
}
