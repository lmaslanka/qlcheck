// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

// Matches literals across the files of one compilation by value *and* target (the parameter,
// field, property or indexer they feed), so "open" for Order.Status never matches "open" for Door.State.
internal static class MagicLiteralSharedPattern
{
    private const string FileSeparator = ", ";

    private static readonly SymbolDisplayFormat ConstantFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
        .WithMemberOptions(SymbolDisplayMemberOptions.IncludeContainingType);

    public static void Apply(CompilationContext ctx, string id)
    {
        var constants = new Dictionary<(string Target, object Value), IFieldSymbol>();
        var uses = new List<Use>();
        foreach (var tree in ctx.Compilation.SyntaxTrees)
        {
            var model = ctx.Compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            IndexConstants(model, root, constants);
            CollectLiterals(tree, model, root, uses);
        }

        var files = uses
            .GroupBy(use => use.Key)
            .ToDictionary(group => group.Key, group => group.Select(use => use.Path).Distinct().ToList());

        foreach (var use in uses)
        {
            if (!ctx.Includes(use.Tree))
            {
                continue;
            }

            var label = LiteralContext.Label(use.Literal);
            var location = use.Value.GetLocation();
            if (constants.TryGetValue(use.Key, out var constant))
            {
                var position = use.Value.SpanStart;
                var replacement = use.Model.IsAccessible(position, constant)
                    ? constant.ToMinimalDisplayString(use.Model, position, ConstantFormat)
                    : null;
                ctx.ReportCustom(id, use.Path, location, ExistingMessage(label, Name(constant), use.Target.Display), replacement);
                continue;
            }

            var paths = files[use.Key];
            if (paths.Count >= Thresholds.SharedLiteralFiles)
            {
                var others = string.Join(
                    FileSeparator,
                    paths.Where(path => path != use.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal));
                ctx.ReportCustom(id, use.Path, location, SharedMessage(label, use.Target.Display, paths.Count, others));
            }
        }
    }

    public static string ExistingMessage(string label, string constant, string target) =>
        $"{label} is already const {constant} for {target}; use the const.";

    public static string SharedMessage(string label, string target, int files, string others) =>
        $"{label} is used for {target} in {files} files (also {others}); extract one shared const.";

    private static void IndexConstants(
        SemanticModel model,
        SyntaxNode root,
        Dictionary<(string Target, object Value), IFieldSymbol> constants)
    {
        foreach (var node in root.DescendantNodes())
        {
            if (node is not (IdentifierNameSyntax or MemberAccessExpressionSyntax) || IsMemberAccessName(node))
            {
                continue;
            }

            if (LiteralContext.SymbolOf(model, node) is not IFieldSymbol { IsConst: true, ConstantValue: { } value } constant)
            {
                continue;
            }

            if (LiteralContext.TargetOf(model, (ExpressionSyntax)node) is { } target)
            {
                constants.TryAdd((target.Key, value), constant);
            }
        }
    }

    private static bool IsMemberAccessName(SyntaxNode node) =>
        node.Parent is MemberAccessExpressionSyntax access && access.Name == node;

    private static void CollectLiterals(SyntaxTree tree, SemanticModel model, SyntaxNode root, List<Use> uses)
    {
        var path = CompilationContext.PathOf(tree);
        foreach (var literal in root.DescendantNodes().OfType<LiteralExpressionSyntax>())
        {
            if (!LiteralContext.IsCandidate(literal))
            {
                continue;
            }

            var value = LiteralContext.ValueExpression(literal);
            if (model.GetConstantValue(value) is not { HasValue: true, Value: { } constant })
            {
                continue;
            }

            if (LiteralContext.TargetOf(model, value) is { } target)
            {
                uses.Add(new Use(tree, model, path, literal, value, target, (target.Key, constant)));
            }
        }
    }

    private static string Name(IFieldSymbol constant) => $"{constant.ContainingType.Name}.{constant.Name}";

    private sealed record Use(
        SyntaxTree Tree,
        SemanticModel Model,
        string Path,
        LiteralExpressionSyntax Literal,
        ExpressionSyntax Value,
        LiteralContext.LiteralTarget Target,
        (string Target, object Value) Key);
}
