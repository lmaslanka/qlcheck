namespace Qlcheck.Scan;

// A repository's intended architecture: its .qlcheck_architecture and the folder it governs.
internal sealed record ArchitectureFile(string Path, string Boundary, IReadOnlyList<ArchitectureConstraint> Constraints);
