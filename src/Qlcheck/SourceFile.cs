using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck;

public sealed record SourceFile(string Path, string Text)
{
    private SyntaxTree? _tree;

    public SyntaxTree Tree => _tree ??= CSharpSyntaxTree.ParseText(Text, path: Path);
}
