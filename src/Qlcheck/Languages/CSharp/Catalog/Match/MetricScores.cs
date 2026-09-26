// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class MetricScores
{
    private const int BaseComplexity = 1;

    private static readonly HashSet<SyntaxKind> Decisions =
    [
        SyntaxKind.IfStatement,
        SyntaxKind.WhileStatement,
        SyntaxKind.ForStatement,
        SyntaxKind.ForEachStatement,
        SyntaxKind.CaseSwitchLabel,
        SyntaxKind.CatchClause,
        SyntaxKind.ConditionalExpression,
        SyntaxKind.CoalesceExpression,
        SyntaxKind.SwitchExpressionArm,
    ];

    private static readonly HashSet<SyntaxKind> Structures =
    [
        SyntaxKind.IfStatement,
        SyntaxKind.WhileStatement,
        SyntaxKind.ForStatement,
        SyntaxKind.ForEachStatement,
        SyntaxKind.DoStatement,
        SyntaxKind.SwitchStatement,
        SyntaxKind.CatchClause,
        SyntaxKind.ConditionalExpression,
    ];

    private static readonly HashSet<SyntaxKind> TypeKinds =
    [
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.InterfaceDeclaration,
    ];

    private const string LogName = "Log";

    private const string InfoName = "Info";

    private const string DebugName = "Debug";

    private const string WarningName = "Warning";

    private const string ErrorName = "Error";

    private const string TraceName = "Trace";

    private const string FatalName = "Fatal";

    private const string InformationName = "Information";

    private const string LogInformationName = "LogInformation";

    private const string LogWarningName = "LogWarning";

    private const string LogErrorName = "LogError";

    private const string LogDebugName = "LogDebug";

    private static readonly HashSet<string> LogNames =
    [
        LogName,
        InfoName,
        DebugName,
        WarningName,
        ErrorName,
        TraceName,
        FatalName,
        InformationName,
        LogInformationName,
        LogWarningName,
        LogErrorName,
        LogDebugName,
    ];

    public static int Cyclomatic(SyntaxNode node)
    {
        var score = BaseComplexity;
        foreach (var child in node.DescendantNodes())
        {
            if (Decisions.Contains(child.Kind()))
            {
                score++;
            }
        }

        return score + BoolOps(node);
    }

    public static int Cognitive(SyntaxNode node) => WalkCognitive(node, 0);

    public static int Nesting(SyntaxNode node) => WalkNesting(node, 0);

    public static int BoolOps(SyntaxNode node)
    {
        var count = 0;
        foreach (var token in node.DescendantTokens())
        {
            if (IsBoolOp(token))
            {
                count++;
            }
        }

        return count;
    }

    public static int Coupling(SyntaxNode type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in type.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            AddType(names, member.Declaration.Type);
        }

        foreach (var property in type.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            AddType(names, property.Type);
        }

        return names.Count;
    }

    public static int LogCalls(SyntaxNode node)
    {
        var count = 0;
        foreach (var child in node.DescendantNodes())
        {
            if (child.IsKind(SyntaxKind.InvocationExpression) && LogNames.Contains(Names.Invocation(child)))
            {
                count++;
            }
        }

        return count;
    }

    public static IEnumerable<SyntaxNode> Methods(WalkContext ctx)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.MethodDeclaration))
        {
            yield return node;
        }

        foreach (var node in ctx.Nodes(SyntaxKind.PropertyDeclaration))
        {
            yield return node;
        }
    }

    public static IEnumerable<SyntaxNode> Types(WalkContext ctx)
    {
        foreach (var kind in TypeKinds)
        {
            foreach (var node in ctx.Nodes(kind))
            {
                yield return node;
            }
        }
    }

    private static int WalkCognitive(SyntaxNode node, int nesting)
    {
        var score = 0;
        var next = nesting;
        if (Structures.Contains(node.Kind()))
        {
            score += BaseComplexity + nesting;
            next++;
        }

        foreach (var child in node.ChildNodes())
        {
            score += WalkCognitive(child, next);
        }

        return score;
    }

    private static int WalkNesting(SyntaxNode node, int depth)
    {
        var here = Structures.Contains(node.Kind()) ? depth + 1 : depth;
        var max = here;
        foreach (var child in node.ChildNodes())
        {
            var childMax = WalkNesting(child, here);
            if (childMax > max)
            {
                max = childMax;
            }
        }

        return max;
    }

    private static bool IsBoolOp(SyntaxToken token) =>
        token.IsKind(SyntaxKind.AmpersandAmpersandToken) || token.IsKind(SyntaxKind.BarBarToken);

    private static void AddType(HashSet<string> names, TypeSyntax type)
    {
        var name = Names.TypeText(type);
        if (name.Length > 0)
        {
            names.Add(name);
        }
    }
}
