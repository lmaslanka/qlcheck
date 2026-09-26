// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis.CSharp;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class AttributeMatch
{
    public static void Report(WalkContext ctx, string id, string name)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.Attribute))
        {
            if (Same(Names.Attribute(node), name))
            {
                ctx.Report(id, node);
            }
        }
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}
