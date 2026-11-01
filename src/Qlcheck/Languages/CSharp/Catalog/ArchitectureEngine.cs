// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

// Checks of the declared architecture (plan 06). Dependencies are extracted per Compilation, then
// judged once per run against each file's Constraints, since a cycle can span projects (ADR 0007:
// a Check that needs a different scope gets an engine at that scope). With no .qlcheck_architecture
// nothing is extracted and nothing is reported (D4).
internal static class ArchitectureEngine
{
    public static ArchitectureRow[] Rows { get; } =
    [
        new ArchitectureRow
        {
            Id = "architecture-relationship",
            Class = CheckClass.Architecture,
            Apply = (dependencies, _) => RelationshipPattern.Find(dependencies, "architecture-relationship"),
        },
    ];

    public static bool Selects(IReadOnlySet<string> selected) => Rows.Any(row => selected.Contains(row.Id));

    public static IReadOnlyList<Finding> Apply(
        IReadOnlyList<GovernedDependency> dependencies,
        IReadOnlySet<string> selected,
        IReadOnlyDictionary<string, string> messages) =>
        [.. Rows.Where(row => selected.Contains(row.Id)).SelectMany(row => row.Apply(dependencies, messages))];
}
