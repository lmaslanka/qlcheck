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

    public override string ToString() => $"{Path}:{StartLine}-{EndLine}";
}
