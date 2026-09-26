namespace Qlcheck.Languages.CSharp.Checks.Coverage;

internal sealed record CoverageAnalysis(
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<CoverageFile> Files);
