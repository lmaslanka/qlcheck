// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

internal static class LocalFacts
{
    public static IEnumerable<VariableDeclaratorSyntax> Declarators(SyntaxNode node)
    {
        if (node is not LocalDeclarationStatementSyntax local)
        {
            yield break;
        }

        foreach (var declarator in local.Declaration.Variables)
        {
            yield return declarator;
        }
    }

    public static bool Unused(VariableDeclaratorSyntax declarator)
    {
        var method = Shapes.Enclosing(declarator, SyntaxKind.MethodDeclaration);
        if (method is null)
        {
            return false;
        }

        var name = declarator.Identifier.Text;
        foreach (var token in method.DescendantTokens())
        {
            if (token.SpanStart == declarator.Identifier.SpanStart)
            {
                continue;
            }

            if (token.IsKind(SyntaxKind.IdentifierToken) && token.Text == name)
            {
                return false;
            }
        }

        return true;
    }

    public static bool PrivateUnused(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.PrivateKeyword))
        {
            return false;
        }

        var name = Names.Declared(node);
        if (name.Length == 0)
        {
            return false;
        }

        var type = ContainingType(node);
        if (type is null)
        {
            return false;
        }

        var uses = 0;
        foreach (var token in type.DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.IdentifierToken) && token.Text == name)
            {
                uses++;
            }
        }

        return uses == 1;
    }

    private static SyntaxNode? ContainingType(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is TypeDeclarationSyntax)
            {
                return current;
            }
        }

        return null;
    }
}
