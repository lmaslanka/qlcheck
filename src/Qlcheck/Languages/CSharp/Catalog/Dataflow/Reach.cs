// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// The Flow rows that ask the compiler's control-flow analysis whether code can run or end, rather
// than reading statement shapes: `if (Enabled)` with a const false is as dead as `if (false)`, and
// `for (;;)` or a `break` that only leaves a nested switch loops as surely as `while (true) { }`.
internal static class Reach
{
    private const string CancellationTokenType = "System.Threading.CancellationToken";

    // reachable-branch: an `if` that can run whose body cannot. Only conditions that mention a
    // boolean literal or a constant can make that so, which keeps the analysis off most ifs.
    public static void UnreachableBranch(WalkContext ctx, string id)
    {
        var hasErrors = HasErrors(ctx);
        foreach (var statement in ctx.Nodes(SyntaxKind.IfStatement).OfType<IfStatementSyntax>())
        {
            if (MentionsConstant(ctx, statement.Condition) && !hasErrors.Value && IsDead(ctx, statement))
            {
                ctx.Report(id, statement);
            }
        }
    }

    private static bool IsDead(WalkContext ctx, IfStatementSyntax statement) =>
        ctx.Model.AnalyzeControlFlow(statement) is { Succeeded: true, StartPointIsReachable: true }
        && ctx.Model.AnalyzeControlFlow(statement.Statement) is { Succeeded: true, StartPointIsReachable: false };

    // no-infinite-loop: a loop whose condition is always true (or absent) that can be entered but
    // never left: no break, return or goto out of it, and no throw or yield in its body. A loop that
    // awaits or uses a CancellationToken is meant to run until cancelled (D9).
    public static void InfiniteLoop(WalkContext ctx, string id)
    {
        var hasErrors = HasErrors(ctx);
        var loops = ctx.Nodes(SyntaxKind.WhileStatement).Concat(ctx.Nodes(SyntaxKind.DoStatement)).Concat(ctx.Nodes(SyntaxKind.ForStatement));
        foreach (var loop in loops.OfType<StatementSyntax>())
        {
            if (AlwaysTrue(ctx, Condition(loop)) && !hasErrors.Value && NeverEnds(ctx, loop))
            {
                ctx.Report(id, loop);
            }
        }
    }

    private static bool NeverEnds(WalkContext ctx, StatementSyntax loop) =>
        ctx.Model.AnalyzeControlFlow(loop) is { Succeeded: true, StartPointIsReachable: true, EndPointIsReachable: false, ExitPoints.IsEmpty: true }
        && !Leaves(ctx, Body(loop));

    private static ExpressionSyntax? Condition(StatementSyntax loop) =>
        loop switch
        {
            WhileStatementSyntax statement => statement.Condition,
            DoStatementSyntax statement => statement.Condition,
            ForStatementSyntax statement => statement.Condition,
            _ => null,
        };

    private static StatementSyntax Body(StatementSyntax loop) =>
        loop switch
        {
            WhileStatementSyntax statement => statement.Statement,
            DoStatementSyntax statement => statement.Statement,
            ForStatementSyntax statement => statement.Statement,
            _ => loop,
        };

    private static bool AlwaysTrue(WalkContext ctx, ExpressionSyntax? condition) =>
        condition is null || ctx.Model.GetConstantValue(condition) is { HasValue: true, Value: true };

    // Flow analysis of a file with compile errors is unreliable (ADR 0003); asked only once a
    // candidate is found.
    private static Lazy<bool> HasErrors(WalkContext ctx) =>
        new(() => ctx.Model.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

    // A boolean literal or a constant in the condition itself; a literal inside a pattern
    // (`x is { Done: false }`) is a value to match, not a constant condition.
    private static bool MentionsConstant(WalkContext ctx, ExpressionSyntax condition) =>
        condition.DescendantNodesAndSelf(node => node is not PatternSyntax).Any(node =>
            node.Kind() is SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression
            || node is IdentifierNameSyntax && ctx.Model.GetSymbolInfo(node).Symbol is IFieldSymbol { IsConst: true } or ILocalSymbol { IsConst: true });

    // A throw, yield or await in the body itself (not in a lambda or local function declared there),
    // or any use of a CancellationToken.
    private static bool Leaves(WalkContext ctx, StatementSyntax body) =>
        body.DescendantNodesAndSelf(node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .Any(node => node is ThrowStatementSyntax or ThrowExpressionSyntax or YieldStatementSyntax or AwaitExpressionSyntax
                || node is IdentifierNameSyntax && Symbols.Is(ctx.Model.GetTypeInfo(node).Type, CancellationTokenType));
}
