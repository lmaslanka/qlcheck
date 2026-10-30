// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

internal static class CompilationEngine
{
    public static CompilationRow[] Rows { get; } =
    [
        new CompilationRow
        {
            Id = "magic-literal-shared",
            Class = CheckClass.Compilation,
            Apply = ctx => MagicLiteralSharedPattern.Apply(ctx, "magic-literal-shared"),
        },
        new CompilationRow
        {
            Id = "non-nullable-uninitialized",
            Class = CheckClass.Compilation,
            Apply = ctx => NullablePattern.Apply(ctx, "non-nullable-uninitialized", "CS8618"),
        },
        new CompilationRow
        {
            Id = "nullable-argument",
            Class = CheckClass.Compilation,
            Apply = ctx => NullablePattern.Apply(ctx, "nullable-argument", "CS8604"),
        },
        new CompilationRow
        {
            Id = "nullable-assignment",
            Class = CheckClass.Compilation,
            Apply = ctx => NullablePattern.Apply(ctx, "nullable-assignment", "CS8600", "CS8601", "CS8625"),
        },
        new CompilationRow
        {
            Id = "nullable-return",
            Class = CheckClass.Compilation,
            Apply = ctx => NullablePattern.Apply(ctx, "nullable-return", "CS8603"),
        },
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
