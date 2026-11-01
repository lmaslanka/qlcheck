// Copyright (c) qlcheck contributors.
using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp.Catalog;

// architecture-relationship: a reference that breaks a must-not-use or only-used-by Constraint. A
// source may always use itself: under `Q.Scan must-not-use Q`, Scan code may use Scan code.
internal static class RelationshipPattern
{
    public static IEnumerable<Finding> Find(IReadOnlyList<GovernedDependency> dependencies, string id)
    {
        foreach (var (file, dependency) in dependencies)
        {
            if (file.Constraints.Select(constraint => Breach(constraint, dependency)).FirstOrDefault(message => message is not null) is { } message)
            {
                yield return Finding.At(id, dependency.Path, dependency.Location, message);
            }
        }
    }

    // The message for a dependency that breaks the Constraint, or null when it does not.
    private static string? Breach(ArchitectureConstraint constraint, Dependency dependency)
    {
        var place = $"({ArchitectureFiles.FileName}:{constraint.Line})";
        switch (constraint.Kind)
        {
            case ConstraintKind.MustNotUse:
                var source = constraint.Sources.FirstOrDefault(name => DottedNames.Matches(name, dependency.Source) && !DottedNames.Matches(name, dependency.Target));
                return source is not null && constraint.Targets.Any(name => DottedNames.Matches(name, dependency.Target))
                    ? $"`{source}` must not use `{dependency.Target}` {place}. Move this code to a component that may use it, or move `{SimpleName(dependency.Target)}` into one `{source}` may use."
                    : null;
            case ConstraintKind.OnlyUsedBy:
                var restricted = constraint.Targets.Any(name => DottedNames.Matches(name, dependency.Target) && !DottedNames.Matches(name, dependency.Source));
                return restricted && !constraint.Sources.Any(name => DottedNames.Matches(name, dependency.Source))
                    ? $"Only {Names(constraint.Sources)} may use `{dependency.Target}` {place}. Call one of them instead."
                    : null;
            default:
                return null;
        }
    }

    private static string SimpleName(string dotted) => dotted[(dotted.LastIndexOf('.') + 1)..];

    private static string Names(IReadOnlyList<string> names)
    {
        var quoted = names.Select(name => $"`{name}`").ToList();
        return quoted.Count == 1 ? quoted[0] : $"{string.Join(", ", quoted[..^1])} and {quoted[^1]}";
    }
}
