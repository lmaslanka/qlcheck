// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

internal static class Shapes
{
    private const int EmptyCount = 0;

    public static bool HasModifier(SyntaxNode node, SyntaxKind kind)
    {
        var modifiers = ModifiersOf(node);
        return modifiers.Any(token => token.IsKind(kind));
    }

    public static int ParameterCount(SyntaxNode node)
    {
        var list = ParametersOf(node);
        return list is null ? EmptyCount : list.Parameters.Count;
    }

    public static string ReturnType(SyntaxNode node) =>
        node switch
        {
            MethodDeclarationSyntax method => Names.TypeText(method.ReturnType),
            PropertyDeclarationSyntax property => Names.TypeText(property.Type),
            _ => string.Empty,
        };

    public static bool IsEmptyBlock(SyntaxNode node)
    {
        if (node is not BlockSyntax block)
        {
            return false;
        }

        return block.Statements.Count == EmptyCount;
    }

    public static bool IsEmptyMethod(SyntaxNode node)
    {
        if (node is not MethodDeclarationSyntax method || method.Body is null)
        {
            return false;
        }

        return IsEmptyBlock(method.Body);
    }

    public static int MemberCount(SyntaxNode node)
    {
        var count = EmptyCount;
        foreach (var child in node.ChildNodes())
        {
            if (IsMemberKind(child.Kind()))
            {
                count++;
            }
        }

        return count;
    }

    private static readonly HashSet<SyntaxKind> MemberKinds =
    [
        SyntaxKind.MethodDeclaration,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.FieldDeclaration,
        SyntaxKind.ConstructorDeclaration,
        SyntaxKind.DestructorDeclaration,
        SyntaxKind.EventDeclaration,
        SyntaxKind.EventFieldDeclaration,
        SyntaxKind.IndexerDeclaration,
        SyntaxKind.OperatorDeclaration,
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.EnumDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.DelegateDeclaration,
    ];

    public static bool IsMemberKind(SyntaxKind kind) => MemberKinds.Contains(kind);

    public static SyntaxNode? Body(SyntaxNode node) =>
        node switch
        {
            MethodDeclarationSyntax method => method.Body,
            ConstructorDeclarationSyntax ctor => ctor.Body,
            DestructorDeclarationSyntax dtor => dtor.Body,
            AccessorDeclarationSyntax accessor => accessor.Body,
            _ => null,
        };

    public static SyntaxNode? Enclosing(SyntaxNode node, SyntaxKind kind)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current.IsKind(kind))
            {
                return current;
            }
        }

        return null;
    }

    public static bool NestedIn(SyntaxNode node, SyntaxKind kind) => Enclosing(node, kind) is not null;

    public static int LineSpan(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    public static int FileLines(string text)
    {
        if (text.Length == 0)
        {
            return EmptyCount;
        }

        var lines = 1;
        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                lines++;
            }
        }

        if (text[^1] == '\n')
        {
            lines--;
        }

        return lines;
    }

    private static SyntaxTokenList ModifiersOf(SyntaxNode node) =>
        node switch
        {
            MemberDeclarationSyntax member => member.Modifiers,
            AccessorDeclarationSyntax accessor => accessor.Modifiers,
            LocalDeclarationStatementSyntax local => local.Modifiers,
            ParameterSyntax parameter => parameter.Modifiers,
            _ => default,
        };

    private static ParameterListSyntax? ParametersOf(SyntaxNode node) =>
        node switch
        {
            MethodDeclarationSyntax method => method.ParameterList,
            ConstructorDeclarationSyntax ctor => ctor.ParameterList,
            LocalFunctionStatementSyntax local => local.ParameterList,
            _ => null,
        };
}
