using Microsoft.CodeAnalysis;

namespace Qlcheck;

public interface ICompilationCheck : ICheck
{
    IReadOnlyList<Finding> AnalyzeCompilation(
        Compilation compilation,
        IReadOnlySet<string> includedFiles);
}
