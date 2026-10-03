// Copyright (c) qlcheck contributors.
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

internal static class ConstantUsage
{
    private static readonly ConditionalWeakTable<Compilation, HashSet<ISymbol>> Index = new();

    public static bool IsRequiredElsewhere(WalkContext ctx, FieldDeclarationSyntax field)
    {
        var required = Index.GetValue(ctx.Model.Compilation, BuildIndex);
        foreach (var variable in field.Declaration.Variables)
        {
            if (ctx.Model.GetDeclaredSymbol(variable) is ISymbol symbol && required.Contains(symbol))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<ISymbol> BuildIndex(Compilation compilation)
    {
        var symbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var expression = ConstantContextExpression(node);
                if (expression is not null)
                {
                    CollectReferencedFields(model, expression, symbols);
                }
            }
        }

        return symbols;
    }

    private static ExpressionSyntax? ConstantContextExpression(SyntaxNode node) => node switch
    {
        CaseSwitchLabelSyntax label => label.Value,
        ConstantPatternSyntax pattern => pattern.Expression,
        AttributeArgumentSyntax argument => argument.Expression,
        _ => null,
    };

    private static void CollectReferencedFields(SemanticModel model, ExpressionSyntax expression, HashSet<ISymbol> symbols)
    {
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            if (node is not (IdentifierNameSyntax or MemberAccessExpressionSyntax))
            {
                continue;
            }

            var symbol = model.GetSymbolInfo(node).Symbol;
            if (symbol is IFieldSymbol or IPropertySymbol)
            {
                symbols.Add(symbol);
            }
        }
    }
}
