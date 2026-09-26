using Qlcheck.Checks;
using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp.Checks.Coverage;

internal interface ICoverageCheck : ICheck
{
    CoverageAnalysis Analyze(IReadOnlyList<SourceScan.LoadedSource> files);
}
