// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class CatalogWalker
{
    private static readonly IReadOnlyList<WalkRow> Rows =
    [
        ..SyntaxEngine.Rows,
        ..SymbolEngine.Rows,
        ..FlowEngine.Rows,
        ..TaintEngine.Rows,
        ..MetricEngine.Rows,
    ];

    private static readonly HashSet<string> Implemented =
        Rows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);

    private static readonly Dictionary<string, CheckClass> Classes =
        Rows.ToDictionary(row => row.Id, row => row.Class, StringComparer.Ordinal);

    public static bool IsImplemented(string id) => Implemented.Contains(id);

    public static bool TryClass(string id, out CheckClass group) => Classes.TryGetValue(id, out group);

    public static IReadOnlyList<string> ImplementedIds => Implemented.OrderBy(id => id, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<Finding> Walk(
        SourceFile file,
        SyntaxTree tree,
        SemanticModel model,
        IReadOnlySet<string> selected,
        IReadOnlyDictionary<string, string> messages)
    {
        var ctx = WalkContext.Create(file, tree, model, messages);
        SyntaxEngine.Apply(ctx, selected);
        SymbolEngine.Apply(ctx, selected);
        FlowEngine.Apply(ctx, selected);
        TaintEngine.Apply(ctx, selected);
        MetricEngine.Apply(ctx, selected);
        return ctx.Findings;
    }
}
