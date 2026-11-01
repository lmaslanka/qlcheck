// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

// redundant-null-check (plan 07): a null check of a Never-null result. Counted shapes are `== null`,
// `!= null`, `is null`, `is not null`, `?.` and `??` (D4, D5), on a method result used directly,
// after `await`, or through a local initialised from the call that is not reassigned or passed by
// ref or out before the check, in source order (D3). Parameters, fields and properties can be null
// at run time whatever their annotations, so they are out of scope. A file with compile errors
// gives nothing (ADR 0003). The message names the method and why it cannot return null (D8), and
// there is no replacement (D6).
internal static class RedundantNullCheckPattern
{
    public static void Apply(WalkContext ctx, string id)
    {
        var hasErrors = new Lazy<bool>(() => ctx.Model.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var neverNull = new NeverNull(ctx.Model);
        foreach (var node in ctx.Tree.GetRoot().DescendantNodes())
        {
            if (Checked(node) is not { } check || Reason(ctx, neverNull, check.Value) is not { } reason || hasErrors.Value)
            {
                continue;
            }

            var message = $"{ctx.Message(id)} {Detail(reason)}";
            if (check.At is { } token)
            {
                ctx.ReportCustom(id, token, message);
            }
            else
            {
                ctx.ReportCustom(id, node, message);
            }
        }
    }

    // The value a null check tests, and the token to report at when it is not the whole node (`?.`).
    private static (ExpressionSyntax Value, SyntaxToken? At)? Checked(SyntaxNode node) =>
        node switch
        {
            BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression } comparison
                when IsNull(comparison.Right) => (comparison.Left, null),
            BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression } comparison
                when IsNull(comparison.Left) => (comparison.Right, null),
            BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce => (coalesce.Left, null),
            IsPatternExpressionSyntax { Pattern: var pattern } test when IsNullPattern(pattern) => (test.Expression, null),
            ConditionalAccessExpressionSyntax access => (access.Expression, access.OperatorToken),
            _ => null,
        };

    private static bool IsNull(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.NullLiteralExpression);

    private static bool IsNullPattern(PatternSyntax pattern) =>
        pattern switch
        {
            ConstantPatternSyntax constant => IsNull(constant.Expression),
            UnaryPatternSyntax { Pattern: ConstantPatternSyntax constant } => IsNull(constant.Expression),
            _ => false,
        };

    private static NeverNullReason? Reason(WalkContext ctx, NeverNull neverNull, ExpressionSyntax value)
    {
        var unwrapped = Unparenthesized(value);
        if (Call(unwrapped) is { } call)
        {
            return neverNull.Reason(call);
        }

        return unwrapped is IdentifierNameSyntax name
            && ctx.Model.GetSymbolInfo(name).Symbol is ILocalSymbol local
            && Initializer(local) is { } initializer
            && Call(Unparenthesized(initializer.Value)) is { } initial
            && !WrittenBetween(ctx, local, initializer, name)
                ? neverNull.Reason(initial)
                : null;
    }

    private static ExpressionSyntax Unparenthesized(ExpressionSyntax value) =>
        value is ParenthesizedExpressionSyntax parenthesized ? Unparenthesized(parenthesized.Expression) : value;

    // A call, or an awaited call.
    private static InvocationExpressionSyntax? Call(ExpressionSyntax value) =>
        value switch
        {
            InvocationExpressionSyntax invocation => invocation,
            AwaitExpressionSyntax { Expression: InvocationExpressionSyntax awaited } => awaited,
            _ => null,
        };

    private static EqualsValueClauseSyntax? Initializer(ILocalSymbol local) =>
        local.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).OfType<VariableDeclaratorSyntax>().FirstOrDefault()?.Initializer;

    // The local is assigned, or passed by ref or out, after its declaration and before the check.
    private static bool WrittenBetween(WalkContext ctx, ILocalSymbol local, SyntaxNode declaration, SyntaxNode check)
    {
        bool IsLocal(SyntaxNode node) => SymbolEqualityComparer.Default.Equals(ctx.Model.GetSymbolInfo(node).Symbol, local);
        var body = declaration.Ancestors().FirstOrDefault(ancestor => ancestor is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) ?? declaration.SyntaxTree.GetRoot();
        return body.DescendantNodes()
            .Where(node => node.SpanStart > declaration.Span.End && node.SpanStart < check.SpanStart)
            .Any(node => node switch
            {
                AssignmentExpressionSyntax assignment => IsLocal(assignment.Left),
                ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None) && IsLocal(argument.Expression),
                _ => false,
            });
    }

    private static string Detail(NeverNullReason reason) =>
        reason.Kind switch
        {
            NeverNullKind.Declared => $"`{reason.Method.Name}` is declared to return a non-nullable `{reason.Type.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}`.",
            NeverNullKind.Iterator => $"`{reason.Method.Name}` is an iterator, which never returns null.",
            _ => $"`{reason.Method.Name}` returns a new or empty value on every path.",
        };
}
