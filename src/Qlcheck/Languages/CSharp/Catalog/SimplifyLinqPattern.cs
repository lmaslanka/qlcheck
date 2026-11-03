// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

// LINQ calls with a shorter equivalent (plan 09), resolved to System.Linq.Enumerable or Queryable:
// `Where(p)` followed by a parameterless `Any`, `Count`, `First` and the like, which all take `p`
// themselves, and a `Select` whose lambda returns its parameter unchanged.
internal static class SimplifyLinqPattern
{
    private const string WhereName = "Where";

    private const string SelectName = "Select";

    private const string ExpressionTypeName = "System.Linq.Expressions.Expression`1";

    private const int PredicateTypeArguments = 2;

    private static readonly HashSet<string> LinqTypes = new(StringComparer.Ordinal) { "System.Linq.Enumerable", "System.Linq.Queryable" };

    private static readonly HashSet<string> PredicateOverloads = new(StringComparer.Ordinal)
    {
        "Any", "Count", "LongCount", "First", "FirstOrDefault", "Single", "SingleOrDefault", "Last", "LastOrDefault",
    };

    public static void Apply(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation)
            {
                continue;
            }

            var name = member.Name.Identifier.Text;
            if (PredicateOverloads.Contains(name) && invocation.ArgumentList.Arguments.Count == 0 && IsWhereWithPredicate(ctx, member.Expression) && IsLinq(ctx, invocation))
            {
                ctx.Report(id, invocation, $"Use `{name}(predicate)` instead of `Where(predicate).{name}()`.");
            }
            else if (name == SelectName && IsIdentityLambda(invocation) && IsLinq(ctx, invocation))
            {
                ctx.Report(id, invocation, "Drop the `Select` that returns its argument unchanged.");
            }
        }
    }

    private static bool IsLinq(WalkContext ctx, InvocationExpressionSyntax invocation) =>
        ctx.Model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method && LinqTypes.Contains(Symbols.QualifiedName(method.ContainingType));

    // `Where` with a one-argument predicate: the indexed overload `(x, i) => ...` has no counterpart.
    private static bool IsWhereWithPredicate(WalkContext ctx, ExpressionSyntax expression) =>
        expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: WhereName }, ArgumentList.Arguments.Count: 1 } where
        && IsLinq(ctx, where)
        && ctx.Model.GetSymbolInfo(where).Symbol is IMethodSymbol { Parameters: [.., var predicate] }
        && Delegate(predicate.Type) is { TypeArguments.Length: PredicateTypeArguments };

    private static INamedTypeSymbol? Delegate(ITypeSymbol type) =>
        type is INamedTypeSymbol named && Symbols.QualifiedName(named) == ExpressionTypeName
            ? named.TypeArguments[0] as INamedTypeSymbol
            : type as INamedTypeSymbol;

    private static bool IsIdentityLambda(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments is [{ Expression: var argument }]
        && argument switch
        {
            SimpleLambdaExpressionSyntax lambda => Returns(lambda.Body, lambda.Parameter),
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters: [var parameter] } lambda => Returns(lambda.Body, parameter),
            _ => false,
        };

    private static bool Returns(CSharpSyntaxNode body, ParameterSyntax parameter) =>
        body is IdentifierNameSyntax identifier && identifier.Identifier.Text == parameter.Identifier.Text;
}
