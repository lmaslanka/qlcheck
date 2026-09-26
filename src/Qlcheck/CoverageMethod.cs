namespace Qlcheck;

public sealed record CoverageMethod(string? Name, IReadOnlyList<int> Lines);
