using Microsoft.CodeAnalysis;
using Qlcheck.Checks;

namespace Qlcheck.Languages.CSharp.Checks.File;

public interface IFileCheck : ICheck
{
    IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree);
}
