// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

internal static class CompilationEngine
{
    public static CompilationRow[] Rows { get; } =
    [
        new CompilationRow
        {
            Id = "unused-using",
            Class = CheckClass.Compilation,
            Apply = ctx => UnusedUsingPattern.Apply(ctx, "unused-using"),
        },
    ];

    public static void Apply(CompilationContext ctx, IReadOnlySet<string> selected)
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
