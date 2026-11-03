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

    private static readonly HashSet<SyntaxKind> LoopKinds =
    [
        SyntaxKind.ForStatement,
        SyntaxKind.ForEachStatement,
        SyntaxKind.WhileStatement,
        SyntaxKind.DoStatement,
    ];

    public static bool InsideLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (LoopKinds.Contains(current.Kind()))
            {
                return true;
            }
        }

        return false;
    }

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

        if (assignment.Parent is InitializerExpressionSyntax)
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

    // The statement after a braceless body of `if`, `else`, `for`, `foreach` or `while` when it starts
    // on a later line at the body's column, deeper than the keyword line's indent: it looks conditional but is not.
    // An `if` with an `else`, and an `else if`, leave the check to the last clause of the chain.
    public static StatementSyntax? MisleadinglyIndented(SyntaxNode node)
    {
        var (keyword, body) = node switch
        {
            IfStatementSyntax { Else: null } statement => (statement.IfKeyword, statement.Statement),
            ElseClauseSyntax { Statement: not IfStatementSyntax } clause => (clause.ElseKeyword, clause.Statement),
            ForStatementSyntax statement => (statement.ForKeyword, statement.Statement),
            ForEachStatementSyntax statement => (statement.ForEachKeyword, statement.Statement),
            WhileStatementSyntax statement => (statement.WhileKeyword, statement.Statement),
            _ => (default, null),
        };
        if (body is null or BlockSyntax || Line(body) == Line(keyword) || NextStatement(ChainHead(node)) is not { } next)
        {
            return null;
        }

        return Line(next) > Line(body) && Column(next) == Column(body) && Column(body) > Indent(keyword) ? next : null;
    }

    private static StatementSyntax ChainHead(SyntaxNode node)
    {
        var head = node as StatementSyntax ?? (StatementSyntax)node.Parent!;
        while (head.Parent is ElseClauseSyntax { Parent: IfStatementSyntax outer })
        {
            head = outer;
        }

        return head;
    }

    private static StatementSyntax? NextStatement(StatementSyntax statement)
    {
        var siblings = statement.Parent switch
        {
            BlockSyntax block => block.Statements,
            SwitchSectionSyntax section => section.Statements,
            _ => default,
        };
        var index = siblings.IndexOf(statement);
        return index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : null;
    }

    private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line;

    private static int Line(SyntaxToken token) => token.GetLocation().GetLineSpan().StartLinePosition.Line;

    private static int Column(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Character;

    // The column of the first non-blank character on the token's line.
    private static int Indent(SyntaxToken token)
    {
        var line = token.SyntaxTree!.GetText().Lines[Line(token)].ToString();
        return line.Length - line.TrimStart().Length;
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
}
