// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class MetricMatch
{
    private const string OpenDetail = "(";

    private const string CloseDetail = ")";

    private static readonly Dictionary<string, Action<WalkContext, string>> ByName =
        new(StringComparer.Ordinal)
        {
            [MetricNames.Cyclomatic] = Cyclomatic,
            [MetricNames.Cognitive] = Cognitive,
            [MetricNames.Nesting] = Nesting,
            [MetricNames.Parameters] = Parameters,
            [MetricNames.FileLength] = FileLength,
            [MetricNames.FunctionLength] = FunctionLength,
            [MetricNames.Inheritance] = Inheritance,
            [MetricNames.Coupling] = Coupling,
            [MetricNames.Arity] = Arity,
            [MetricNames.CaseLength] = CaseLength,
            [MetricNames.DuplicateString] = DuplicateString,
            [MetricNames.Expression] = Expression,
            [MetricNames.SwitchCases] = SwitchCases,
            [MetricNames.Logs] = Logs,
        };

    public static void Report(WalkContext ctx, string id, string metric)
    {
        if (ByName.TryGetValue(metric, out var apply))
        {
            apply(ctx, id);
        }
    }

    private static void Cyclomatic(WalkContext ctx, string id)
    {
        foreach (var node in MetricScores.Methods(ctx))
        {
            var score = MetricScores.Cyclomatic(node);
            if (score > Thresholds.MethodComplexity)
            {
                ctx.Report(id, node, Detail(score));
            }
        }
    }

    private static void Cognitive(WalkContext ctx, string id)
    {
        foreach (var node in MetricScores.Methods(ctx))
        {
            var limit = node.IsKind(SyntaxKind.PropertyDeclaration)
                ? Thresholds.CognitiveProperty
                : Thresholds.CognitiveMethod;
            var score = MetricScores.Cognitive(node);
            if (score > limit)
            {
                ctx.Report(id, node, Detail(score));
            }
        }
    }

    private static void Nesting(WalkContext ctx, string id)
    {
        foreach (var node in MetricScores.Methods(ctx))
        {
            var score = MetricScores.Nesting(node);
            if (score > Thresholds.NestingDepth)
            {
                ctx.Report(id, node, Detail(score));
            }
        }
    }

    private static void Parameters(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.MethodDeclaration))
        {
            var score = Shapes.ParameterCount(node);
            if (score > Thresholds.TooManyParameters)
            {
                ctx.Report(id, node, Detail(score));
            }
        }
    }

    private static void FileLength(WalkContext ctx, string id)
    {
        var score = Shapes.FileLines(ctx.Text);
        if (score > Thresholds.FileLength)
        {
            ctx.Report(id, ctx.Tree.GetRoot(), Detail(score));
        }
    }

    private static void FunctionLength(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.MethodDeclaration))
        {
            var score = Shapes.LineSpan(node);
            if (score > Thresholds.FunctionLength)
            {
                ctx.Report(id, node, Detail(score));
            }
        }
    }

    private static void Inheritance(WalkContext ctx, string id)
    {
        foreach (var node in MetricScores.Types(ctx))
        {
            var score = Symbols.InheritanceDepth(ctx, node);
            if (score > Thresholds.InheritanceDepth)
            {
                ctx.Report(id, node, Detail(score));
            }
        }
    }

    private static void Coupling(WalkContext ctx, string id)
    {
        foreach (var node in MetricScores.Types(ctx))
        {
            var score = MetricScores.Coupling(node);
            if (score > Thresholds.ClassCoupling)
            {
                ctx.Report(id, node, Detail(score));
            }
        }
    }

    private static void Arity(WalkContext ctx, string id)
    {
        ReportTypeArity(ctx, id);
        ReportMethodArity(ctx, id);
    }

    private static void CaseLength(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.SwitchSection))
        {
            var score = Shapes.LineSpan(node);
            if (score > Thresholds.CaseLength)
            {
                ctx.Report(id, node, Detail(score));
            }
        }
    }

    private static void DuplicateString(WalkContext ctx, string id)
    {
        var counts = CountStrings(ctx);
        foreach (var node in ctx.Nodes(SyntaxKind.StringLiteralExpression))
        {
            if (node is not LiteralExpressionSyntax literal)
            {
                continue;
            }

            if (counts.TryGetValue(literal.Token.ValueText, out var count) && count >= Thresholds.DuplicateString)
            {
                ctx.Report(id, node, Detail(count));
            }
        }
    }

    private static void Expression(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.IfStatement))
        {
            ReportExpression(ctx, id, node);
        }
    }

    private static void SwitchCases(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.SwitchStatement))
        {
            if (node is not SwitchStatementSyntax statement)
            {
                continue;
            }

            var score = statement.Sections.Count;
            if (score > Thresholds.SwitchCases)
            {
                ctx.Report(id, node, Detail(score));
            }
        }
    }

    private static void Logs(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.MethodDeclaration))
        {
            var score = MetricScores.LogCalls(node);
            if (score > Thresholds.TooManyLogs)
            {
                ctx.Report(id, node, Detail(score));
            }
        }
    }

    private static void ReportTypeArity(WalkContext ctx, string id)
    {
        foreach (var node in MetricScores.Types(ctx))
        {
            if (node is not TypeDeclarationSyntax type || type.TypeParameterList is null)
            {
                continue;
            }

            var score = type.TypeParameterList.Parameters.Count;
            if (score > Thresholds.TypeArity)
            {
                ctx.Report(id, node, Detail(score));
            }
        }
    }

    private static void ReportMethodArity(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.MethodDeclaration))
        {
            if (node is not MethodDeclarationSyntax method || method.TypeParameterList is null)
            {
                continue;
            }

            var score = method.TypeParameterList.Parameters.Count;
            if (score > Thresholds.MethodArity)
            {
                ctx.Report(id, node, Detail(score));
            }
        }
    }

    private static void ReportExpression(WalkContext ctx, string id, SyntaxNode node)
    {
        if (node is not IfStatementSyntax statement)
        {
            return;
        }

        var score = MetricScores.BoolOps(statement.Condition);
        if (score > Thresholds.ExpressionComplexity)
        {
            ctx.Report(id, statement.Condition, Detail(score));
        }
    }

    private static Dictionary<string, int> CountStrings(WalkContext ctx)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in ctx.Nodes(SyntaxKind.StringLiteralExpression))
        {
            if (node is not LiteralExpressionSyntax literal || literal.Token.ValueText.Length == 0)
            {
                continue;
            }

            var value = literal.Token.ValueText;
            counts[value] = counts.TryGetValue(value, out var count) ? count + 1 : 1;
        }

        return counts;
    }

    private static string Detail(int score) => $"{OpenDetail}{score}{CloseDetail}";
}
