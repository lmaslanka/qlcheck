// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class TaintMatch
{
    private const string ReadLine = "ReadLine";

    public static void Report(WalkContext ctx, string id, string[] sinks)
    {
        var sinkSet = new HashSet<string>(sinks, StringComparer.Ordinal);
        ReportInvocations(ctx, id, sinkSet);
        ReportCreations(ctx, id, sinkSet);
        ReportAssignments(ctx, id, sinkSet);
    }

    private static void ReportInvocations(WalkContext ctx, string id, HashSet<string> sinks)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is InvocationExpressionSyntax invocation && sinks.Contains(Names.Invocation(invocation)))
            {
                ReportIfTainted(ctx, id, invocation, invocation.ArgumentList);
            }
        }
    }

    private static void ReportCreations(WalkContext ctx, string id, HashSet<string> sinks)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ObjectCreationExpression))
        {
            if (node is ObjectCreationExpressionSyntax creation && sinks.Contains(Names.Creation(creation)))
            {
                ReportIfTainted(ctx, id, creation, creation.ArgumentList);
            }
        }
    }

    private static void ReportAssignments(WalkContext ctx, string id, HashSet<string> sinks)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.SimpleAssignmentExpression))
        {
            if (node is not AssignmentExpressionSyntax assignment)
            {
                continue;
            }

            if (!sinks.Contains(Names.Member(assignment.Left)) && !sinks.Contains(Names.Simple(assignment.Left)))
            {
                continue;
            }

            if (IsTainted(assignment.Right))
            {
                ctx.Report(id, assignment);
            }
        }
    }

    private static void ReportIfTainted(
        WalkContext ctx,
        string id,
        SyntaxNode node,
        ArgumentListSyntax? arguments)
    {
        if (arguments is null)
        {
            return;
        }

        foreach (var argument in arguments.Arguments)
        {
            if (IsTainted(argument.Expression))
            {
                ctx.Report(id, node);
                return;
            }
        }
    }

    private static bool IsTainted(SyntaxNode expression)
    {
        if (expression is IdentifierNameSyntax identifier && IsParameter(identifier))
        {
            return true;
        }

        if (expression is InvocationExpressionSyntax invocation && Names.Invocation(invocation) == ReadLine)
        {
            return true;
        }

        if (expression is InterpolatedStringExpressionSyntax)
        {
            return expression.DescendantNodes().OfType<IdentifierNameSyntax>().Any(IsParameter);
        }

        if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression))
        {
            return IsTainted(binary.Left) || IsTainted(binary.Right);
        }

        return false;
    }

    private static bool IsParameter(IdentifierNameSyntax identifier)
    {
        var method = Shapes.Enclosing(identifier, SyntaxKind.MethodDeclaration) as MethodDeclarationSyntax;
        if (method is null)
        {
            return false;
        }

        if (!Shapes.HasModifier(method, Microsoft.CodeAnalysis.CSharp.SyntaxKind.PublicKeyword))
        {
            return false;
        }

        foreach (var parameter in method.ParameterList.Parameters)
        {
            if (parameter.Identifier.Text == identifier.Identifier.Text)
            {
                return true;
            }
        }

        return false;
    }
}
