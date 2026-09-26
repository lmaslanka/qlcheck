// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

internal static class StmtFacts
{
    private static readonly HashSet<SyntaxKind> Control =
    [
        SyntaxKind.IfStatement,
        SyntaxKind.ElseClause,
        SyntaxKind.ForStatement,
        SyntaxKind.ForEachStatement,
        SyntaxKind.WhileStatement,
        SyntaxKind.DoStatement,
        SyntaxKind.LockStatement,
        SyntaxKind.UsingStatement,
        SyntaxKind.FixedStatement,
    ];

    public static bool NeedsBraces(SyntaxNode node)
    {
        if (!Control.Contains(node.Kind()))
        {
            return false;
        }

        var statement = BodyOf(node);
        return statement is not null && statement is not BlockSyntax;
    }

    public static bool RedundantParen(SyntaxNode node)
    {
        if (node is not ParenthesizedExpressionSyntax paren)
        {
            return false;
        }

        return paren.Expression is IdentifierNameSyntax or LiteralExpressionSyntax;
    }

    public static bool SelfAssignment(SyntaxNode node)
    {
        if (node is not AssignmentExpressionSyntax assignment)
        {
            return false;
        }

        return assignment.Left.ToString() == assignment.Right.ToString();
    }

    public static bool IdenticalOperands(SyntaxNode node)
    {
        if (node is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        return binary.Left.ToString() == binary.Right.ToString();
    }

    public static bool NestedSwitch(SyntaxNode node) =>
        node.IsKind(SyntaxKind.SwitchStatement) && Shapes.NestedIn(node, SyntaxKind.SwitchStatement);

    public static bool NestedTernary(SyntaxNode node) =>
        node.IsKind(SyntaxKind.ConditionalExpression) && Shapes.NestedIn(node, SyntaxKind.ConditionalExpression);

    public static bool LockOnLocal(SyntaxNode node)
    {
        if (node is not LockStatementSyntax statement)
        {
            return false;
        }

        if (statement.Expression is not IdentifierNameSyntax name)
        {
            return false;
        }

        var method = Shapes.Enclosing(statement, SyntaxKind.MethodDeclaration);
        if (method is null)
        {
            return false;
        }

        foreach (var local in method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (local.Identifier.Text == name.Identifier.Text)
            {
                return true;
            }
        }

        return false;
    }

    public static bool InfiniteWhile(SyntaxNode node)
    {
        if (node is not WhileStatementSyntax statement)
        {
            return false;
        }

        if (!IsTrue(statement.Condition))
        {
            return false;
        }

        return !HasExit(statement.Statement);
    }

    public static bool UnreachableIf(SyntaxNode node)
    {
        if (node is not IfStatementSyntax statement)
        {
            return false;
        }

        return IsFalse(statement.Condition);
    }

    public static bool MissingSwitchDefault(SyntaxNode node)
    {
        if (node is not SwitchStatementSyntax statement)
        {
            return false;
        }

        foreach (var section in statement.Sections)
        {
            foreach (var label in section.Labels)
            {
                if (label.IsKind(SyntaxKind.DefaultSwitchLabel))
                {
                    return false;
                }
            }
        }

        return statement.Sections.Count > 0;
    }

    public static bool FewSwitchCases(SyntaxNode node)
    {
        if (node is not SwitchStatementSyntax statement)
        {
            return false;
        }

        const int OneCase = 1;
        const int TwoCases = 2;
        var count = statement.Sections.Count;
        return count == OneCase || count == TwoCases;
    }

    private static StatementSyntax? BodyOf(SyntaxNode node) =>
        LoopBody(node) ?? OtherBody(node);

    private static StatementSyntax? LoopBody(SyntaxNode node) =>
        node switch
        {
            ForStatementSyntax statement => statement.Statement,
            ForEachStatementSyntax statement => statement.Statement,
            WhileStatementSyntax statement => statement.Statement,
            DoStatementSyntax statement => statement.Statement,
            _ => null,
        };

    private static StatementSyntax? OtherBody(SyntaxNode node) =>
        node switch
        {
            IfStatementSyntax statement => statement.Statement,
            ElseClauseSyntax clause => clause.Statement,
            LockStatementSyntax statement => statement.Statement,
            UsingStatementSyntax statement => statement.Statement,
            FixedStatementSyntax statement => statement.Statement,
            _ => null,
        };

    private static bool IsTrue(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.TrueLiteralExpression);

    private static bool IsFalse(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.FalseLiteralExpression);

    private static bool HasExit(StatementSyntax statement)
    {
        foreach (var node in statement.DescendantNodesAndSelf())
        {
            if (node is BreakStatementSyntax or ReturnStatementSyntax or ThrowStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }
}
