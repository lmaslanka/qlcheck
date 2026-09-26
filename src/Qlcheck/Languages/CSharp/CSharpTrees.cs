using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp;

internal static class CSharpTrees
{
    public static SyntaxTree Parse(SourceFile file) =>
        CSharpSyntaxTree.ParseText(file.Text, path: file.Path);
}
