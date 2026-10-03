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

        // A "flat" switch expression -- one whose arms are plain dispatch, with no further
        // branching inside any arm -- is a lookup table, not a nest of decisions: its whole
        // complexity is the one branch on `action`, not one branch per case. Count it once
        // instead of once per arm, same as a dictionary lookup would be.
        var flatArms = new HashSet<SyntaxNode>();
        foreach (var switchExpression in node.DescendantNodes().OfType<SwitchExpressionSyntax>())
        {
            if (!IsFlatSwitchExpression(switchExpression))
            {
                continue;
            }

            score++;
            foreach (var arm in switchExpression.Arms)
            {
                flatArms.Add(arm);
            }
        }

        // Likewise, an object/anonymous-object initializer that is nothing but a flat list of
        // `Member = value ?? fallback` projections is a mapping table laid out as fields instead
        // of switch arms: each `??` is an independent default fill, not a branch that combines
        // with the others. Count the whole initializer once instead of once per member.
        foreach (var initializer in FlatCoalesceInitializers(node))
        {
            score++;
            foreach (var coalesce in initializer)
            {
                flatArms.Add(coalesce);
            }
        }

        foreach (var child in node.DescendantNodes())
        {
            if (flatArms.Contains(child))
            {
                continue;
            }

            if (Decisions.Contains(child.Kind()))
            {
                score++;
            }
        }

        return score + BoolOps(node, flatArms);
    }

    private const int MinFlatCoalesceCount = 2;

    private static IEnumerable<List<SyntaxNode>> FlatCoalesceInitializers(SyntaxNode node)
    {
        foreach (var creation in node.DescendantNodes())
        {
            var values = InitializerValues(creation);
            if (values is null)
            {
                continue;
            }

            var coalesces = values
                .SelectMany(value => value.DescendantNodesAndSelf().Where(d => d.IsKind(SyntaxKind.CoalesceExpression)))
                .ToList();
            if (coalesces.Count < MinFlatCoalesceCount)
            {
                continue;
            }

            if (values.Any(value => value.DescendantNodesAndSelf().Any(d => Structures.Contains(d.Kind()))))
            {
                continue;
            }

            yield return coalesces;
        }
    }

    private static List<ExpressionSyntax>? InitializerValues(SyntaxNode node) =>
        node switch
        {
            AnonymousObjectCreationExpressionSyntax anonymous =>
                anonymous.Initializers.Select(declarator => declarator.Expression).ToList(),
            InitializerExpressionSyntax { RawKind: (int)SyntaxKind.ObjectInitializerExpression } initializer =>
                initializer.Expressions.OfType<AssignmentExpressionSyntax>().Select(assignment => assignment.Right).ToList(),
            _ => null,
        };

    // Only genuine control flow inside an arm disqualifies the switch from being a flat dispatch
    // table. A `??` or a simple `cond ? a : b` picking between two values is still one-line value
    // selection, not nested branching layered on top of the table -- and it still adds its own
    // point to the score below via the ordinary per-node count, so it is not left uncounted.
    private static readonly HashSet<SyntaxKind> ArmDisqualifyingNesting =
    [
        SyntaxKind.IfStatement,
        SyntaxKind.WhileStatement,
        SyntaxKind.ForStatement,
        SyntaxKind.ForEachStatement,
        SyntaxKind.DoStatement,
        SyntaxKind.SwitchStatement,
        SyntaxKind.CatchClause,
        SyntaxKind.SwitchExpressionArm,
    ];

    private static bool IsFlatSwitchExpression(SwitchExpressionSyntax switchExpression) =>
        switchExpression.Arms.All(arm => !arm.DescendantNodes().Any(descendant =>
            ArmDisqualifyingNesting.Contains(descendant.Kind())));

    // Boolean operators inside a flat switch-expression arm are part of that one routing rule
    // (e.g. `EventCode.X => a != null && b == c`), not separate decisions layered on top of it.
    private static int BoolOps(SyntaxNode node, HashSet<SyntaxNode> excludedArms)
    {
        if (excludedArms.Count == 0)
        {
            return BoolOps(node);
        }

        var count = 0;
        foreach (var token in node.DescendantTokens())
        {
            if (IsBoolOp(token) && !IsWithinAny(token.Parent, excludedArms))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsWithinAny(SyntaxNode? node, HashSet<SyntaxNode> ancestors)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (ancestors.Contains(current))
            {
                return true;
            }
        }

        return false;
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
