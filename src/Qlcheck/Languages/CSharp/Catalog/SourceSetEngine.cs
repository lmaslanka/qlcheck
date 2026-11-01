// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

// Checks that need every scanned file of the run at once, across csproj boundaries (ADR 0007: a
// Check that needs a different scope gets an engine at that scope).
internal static class SourceSetEngine
{
    public static SourceSetRow[] Rows { get; } =
    [
        new SourceSetRow
        {
            Id = "duplicate-code",
            Class = CheckClass.SourceSet,
            Apply = ctx => DuplicateCodePattern.Apply(ctx, "duplicate-code"),
        },
        new SourceSetRow
        {
            Id = "identical-method",
            Class = CheckClass.SourceSet,
            Apply = ctx => IdenticalMethodPattern.Apply(ctx, "identical-method"),
        },
    ];

    public static void Apply(SourceSetContext ctx, IReadOnlySet<string> selected)
    {
        foreach (var row in Rows)
        {
            if (selected.Contains(row.Id))
            {
                row.Apply(ctx);
            }
        }
    }
}
