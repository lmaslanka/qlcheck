// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

internal static class CatchFacts
{
    public static string CatchType(SyntaxNode node)
    {
        if (node is not CatchClauseSyntax clause || clause.Declaration is null)
        {
            return string.Empty;
        }

        return Names.TypeText(clause.Declaration.Type);
    }

    public static string ThrownType(SyntaxNode node)
    {
        if (node is not ThrowStatementSyntax statement || statement.Expression is null)
        {
            return string.Empty;
        }

        if (statement.Expression is ObjectCreationExpressionSyntax creation)
        {
            return Names.Creation(creation);
        }

        return Names.Simple(statement.Expression);
    }

    public static bool Rethrows(SyntaxNode node)
    {
        if (node is not CatchClauseSyntax clause || clause.Filter is not null)
        {
            return false;
        }

        if (clause.Block.Statements.Count != 1
            || clause.Block.Statements[0] is not ThrowStatementSyntax thrown)
        {
            return false;
        }

        if (thrown.Expression is null)
        {
            return true;
        }

        if (thrown.Expression is not IdentifierNameSyntax identifier)
        {
            return false;
        }

        return clause.Declaration is { Identifier.ValueText: var caught }
            && caught == identifier.Identifier.ValueText;
    }

    public static bool BareRethrow(SyntaxNode node)
    {
        if (node is not ThrowStatementSyntax statement)
        {
            return false;
        }

        return statement.Expression is IdentifierNameSyntax;
    }
}
