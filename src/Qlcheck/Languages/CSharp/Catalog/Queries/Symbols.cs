// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

internal static class Symbols
{
    public static ISymbol? Declared(WalkContext ctx, SyntaxNode node) =>
        ctx.Model.GetDeclaredSymbol(node);

    public static ITypeSymbol? TypeOf(WalkContext ctx, SyntaxNode node)
    {
        var info = ctx.Model.GetTypeInfo(node);
        return info.Type;
    }

    public static ISymbol? SymbolOf(WalkContext ctx, SyntaxNode node) =>
        ctx.Model.GetSymbolInfo(node).Symbol;

    public static bool Implements(ITypeSymbol? type, string interfaceName)
    {
        if (type is null)
        {
            return false;
        }

        if (type.Name == interfaceName || type.Interfaces.Any(item => item.Name == interfaceName))
        {
            return true;
        }

        return type.AllInterfaces.Any(item => item.Name == interfaceName);
    }

    public static bool IsDisposable(WalkContext ctx, SyntaxNode creation)
    {
        if (creation is not ObjectCreationExpressionSyntax objectCreation)
        {
            return false;
        }

        var type = TypeOf(ctx, objectCreation.Type);
        const string Disposable = "IDisposable";
        return Implements(type, Disposable);
    }

    public static bool DerivesFrom(ITypeSymbol? type, string typeName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == typeName)
            {
                return true;
            }
        }

        return false;
    }

    // The type, one of its base types or one of its interfaces has this fully qualified metadata
    // name (`System.Data.Common.DbCommand`, `System.Collections.Generic.List`1`, `Outer+Inner`).
    // Generic types match by their definition, so `List<int>` is `List`1`. Unlike DerivesFrom, a
    // user type that merely shares the simple name does not match.
    public static bool Is(ITypeSymbol? type, string qualifiedName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (QualifiedName(current) == qualifiedName)
            {
                return true;
            }
        }

        return type is not null && type.AllInterfaces.Any(item => QualifiedName(item) == qualifiedName);
    }

    public static string QualifiedName(ITypeSymbol type)
    {
        var definition = type.OriginalDefinition;
        if (definition.ContainingType is { } outer)
        {
            return $"{QualifiedName(outer)}+{definition.MetadataName}";
        }

        return definition.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? $"{ns.ToDisplayString()}.{definition.MetadataName}"
            : definition.MetadataName;
    }

    public static int InheritanceDepth(WalkContext ctx, SyntaxNode node)
    {
        var symbol = Declared(ctx, node) as INamedTypeSymbol;
        return Depth(symbol);
    }

    private static int Depth(INamedTypeSymbol? symbol)
    {
        var depth = 0;
        var current = symbol?.BaseType;
        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            depth++;
            current = current.BaseType;
        }

        return depth;
    }
}
