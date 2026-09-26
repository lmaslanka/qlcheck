// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

internal static class Names
{
    public static string Invocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
        {
            return string.Empty;
        }

        return Simple(invocation.Expression);
    }

    public static string InvocationType(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
        {
            return string.Empty;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return string.Empty;
        }

        return TypeText(member.Expression);
    }

    public static string Creation(SyntaxNode node)
    {
        if (node is not ObjectCreationExpressionSyntax creation)
        {
            return string.Empty;
        }

        return TypeText(creation.Type);
    }

    public static string Member(SyntaxNode node)
    {
        if (node is not MemberAccessExpressionSyntax member)
        {
            return string.Empty;
        }

        return member.Name.Identifier.Text;
    }

    public static string MemberType(SyntaxNode node)
    {
        if (node is not MemberAccessExpressionSyntax member)
        {
            return string.Empty;
        }

        return TypeText(member.Expression);
    }

    public static string Attribute(SyntaxNode node)
    {
        if (node is not AttributeSyntax attribute)
        {
            return string.Empty;
        }

        var name = TypeText(attribute.Name);
        const string Suffix = "Attribute";
        if (name.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return name[..^Suffix.Length];
        }

        return name;
    }

    public static string Declared(SyntaxNode node) =>
        node switch
        {
            BaseTypeDeclarationSyntax type => type.Identifier.Text,
            MethodDeclarationSyntax method => method.Identifier.Text,
            PropertyDeclarationSyntax property => property.Identifier.Text,
            EnumMemberDeclarationSyntax member => member.Identifier.Text,
            ParameterSyntax parameter => parameter.Identifier.Text,
            VariableDeclaratorSyntax variable => variable.Identifier.Text,
            _ => string.Empty,
        };

    public static string Simple(SyntaxNode node) =>
        node switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.Text,
            _ => string.Empty,
        };

    public static string TypeText(SyntaxNode node) =>
        node switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
            PredefinedTypeSyntax predefined => predefined.Keyword.Text,
            NullableTypeSyntax nullable => TypeText(nullable.ElementType),
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            _ => string.Empty,
        };
}
