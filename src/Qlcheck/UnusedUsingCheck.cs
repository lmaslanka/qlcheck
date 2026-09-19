using Microsoft.CodeAnalysis;

namespace Qlcheck;

public sealed class UnusedUsingCheck : ICheck
{
    public const string CheckId = "unused-using";

    public const string Message = "Remove unused using directive.";

    public string Id => CheckId;

    public IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree) => [];

    IReadOnlyList<Finding> ICheck.AnalyzeCompilation(
        Compilation compilation,
        IReadOnlySet<string> includedFiles) =>
        AnalyzeCompilation(compilation, includedFiles);

    public IReadOnlyList<Finding> AnalyzeCompilation(
        Compilation compilation,
        IReadOnlySet<string> includedFiles)
    {
        var findings = new List<Finding>();
        foreach (var diagnostic in compilation.GetDiagnostics())
        {
            if (diagnostic.Id != "CS8019" || diagnostic.Location.SourceTree is null)
            {
                continue;
            }

            var path = diagnostic.Location.SourceTree.FilePath.Replace('\\', '/');
            if (includedFiles.Count > 0 &&
                !includedFiles.Contains(path) &&
                !includedFiles.Contains(diagnostic.Location.SourceTree.FilePath))
            {
                continue;
            }

            var span = diagnostic.Location.GetLineSpan().StartLinePosition;
            var line = span.Line + 1;
            var column = span.Character + 1;
            findings.Add(new Finding(
                Id: $"{CheckId}:{path}:{line}:{column}",
                Check: CheckId,
                File: path,
                Line: line,
                Column: column,
                Message: Message,
                Replacement: string.Empty));
        }

        return findings;
    }
}
