// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis.CSharp;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class CreateMatch
{
    public static void Report(WalkContext ctx, string id, string typeName)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ObjectCreationExpression))
        {
            if (Same(Names.Creation(node), typeName))
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void Any(WalkContext ctx, string id, string[] typeNames)
    {
        foreach (var typeName in typeNames)
        {
            Report(ctx, id, typeName);
        }
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}
