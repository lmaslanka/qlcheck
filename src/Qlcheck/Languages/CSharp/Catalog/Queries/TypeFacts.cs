// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

internal static class TypeFacts
{
    public static string BaseType(SyntaxNode node)
    {
        var list = BaseListOf(node);
        if (list is null)
        {
            return string.Empty;
        }

        foreach (var type in list.Types)
        {
            return Names.TypeText(type.Type);
        }

        return string.Empty;
    }

    public static bool InNamespace(SyntaxNode node) =>
        Shapes.NestedIn(node, SyntaxKind.NamespaceDeclaration)
        || Shapes.NestedIn(node, SyntaxKind.FileScopedNamespaceDeclaration);

    public static bool IsPascal(string name)
    {
        if (name.Length == 0 || !char.IsUpper(name[0]))
        {
            return false;
        }

        foreach (var ch in name)
        {
            if (ch is not ('_' or (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsAsyncVoid(SyntaxNode node)
    {
        if (node is not MethodDeclarationSyntax method)
        {
            return false;
        }

        if (!Shapes.HasModifier(method, SyntaxKind.AsyncKeyword))
        {
            return false;
        }

        const string VoidName = "void";
        return Names.TypeText(method.ReturnType) == VoidName;
    }

    public static bool IsPublicField(SyntaxNode node) =>
        node is FieldDeclarationSyntax && Shapes.HasModifier(node, SyntaxKind.PublicKeyword);

    public static bool FieldNotPrivate(SyntaxNode node)
    {
        if (node is not FieldDeclarationSyntax)
        {
            return false;
        }

        if (Shapes.HasModifier(node, SyntaxKind.PrivateKeyword))
        {
            return false;
        }

        return !Shapes.HasModifier(node, SyntaxKind.ConstKeyword);
    }

    public static bool Extends(SyntaxNode node, string name) => BaseType(node) == name;

    public static bool Named(SyntaxNode node, string name) => Names.Declared(node) == name;

    public static bool EndsWith(SyntaxNode node, string suffix)
    {
        var name = Names.Declared(node);
        return name.EndsWith(suffix, StringComparison.Ordinal);
    }

    private static BaseListSyntax? BaseListOf(SyntaxNode node) =>
        node switch
        {
            TypeDeclarationSyntax type => type.BaseList,
            EnumDeclarationSyntax enumeration => enumeration.BaseList,
            _ => null,
        };
}
