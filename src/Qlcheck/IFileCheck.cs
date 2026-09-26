using Microsoft.CodeAnalysis;

namespace Qlcheck;

public interface IFileCheck : ICheck
{
    IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree);
}
