using Microsoft.CodeAnalysis;
using Qlcheck.Checks;

namespace Qlcheck.Languages.CSharp.Checks.Compilation;

public interface ICompilationCheck : ICheck
{
    IReadOnlyList<Finding> AnalyzeCompilation(
        Microsoft.CodeAnalysis.Compilation compilation,
        IReadOnlySet<string> includedFiles);
}
