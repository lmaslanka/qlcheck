namespace Qlcheck.Scan;

// One line of a .qlcheck_architecture file. Sources are the code a Constraint governs (for
// no-cycles, the prefix P); Targets are the types it is about. All are dotted names.
internal sealed record ArchitectureConstraint(ConstraintKind Kind, IReadOnlyList<string> Sources, IReadOnlyList<string> Targets, int Line);
