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
