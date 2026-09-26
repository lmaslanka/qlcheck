namespace Qlcheck;

public sealed record CoverageFile(
    string File,
    bool Missing,
    IReadOnlyList<CoverageMethod> Methods);
