// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Qlcheck.Languages.CSharp.Catalog;

// One place a duplicated statement sequence appears: from its first statement to its last.
internal sealed record CloneCopy(string Path, SyntaxTree Tree, TextSpan Span, StatementSyntax First, StatementSyntax Last)
{
    public int StartLine => Tree.GetLineSpan(Span).StartLinePosition.Line + 1;

    public int EndLine => Tree.GetLineSpan(Span).EndLinePosition.Line + 1;

    public Location Location => Location.Create(Tree, Span);

    // The method, accessor or local function whose whole body this copy is, or null when it is
    // only part of a body.
    public SyntaxNode? WholeBodyOf =>
        First.Parent is BlockSyntax block && block.Statements[0] == First && block.Statements[^1] == Last
            && block.Parent is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax
            ? block.Parent
            : null;

    public override string ToString() => $"{Path}:{StartLine}-{EndLine}";
}
