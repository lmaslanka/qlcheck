using Qlcheck.Scan;

namespace Qlcheck;

// What a run produced. Architecture lists the .qlcheck_architecture files an architecture Check
// applied, so the report can show that a clean run was checked against them.
internal sealed record RunResult(
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<CoverageFile> Coverage,
    IReadOnlyList<ArchitectureFile>? Architecture = null);
