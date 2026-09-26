namespace Qlcheck;

internal sealed record RunResult(
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<CoverageFile> Coverage);
