using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck;

public sealed record Finding(
    string Id,
    string Check,
    string File,
    int Line,
    int Column,
    string Message,
    string? Replacement = null);

public sealed record SourceFile(string Path, string Text)
{
    private SyntaxTree? _tree;

    public SyntaxTree Tree => _tree ??= CSharpSyntaxTree.ParseText(Text, path: Path);
}

public sealed record CheckContext(IReadOnlyList<SourceFile> Files);

public interface ICheck
{
    string Id { get; }

    IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree);

    IReadOnlyList<Finding> AnalyzeCompilation(
        Compilation compilation,
        IReadOnlySet<string> includedFiles) => [];

    IReadOnlyList<Finding> Run(CheckContext context)
    {
        var findings = new List<Finding>();
        foreach (var file in context.Files)
        {
            findings.AddRange(Analyze(file, file.Tree));
        }

        var compilation = CSharpCompilations.Create(
            "qlcheck",
            context.Files.Select(f => f.Tree));
        var included = context.Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        findings.AddRange(AnalyzeCompilation(compilation, included));
        return findings;
    }
}
