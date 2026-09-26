// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis.CSharp;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class InvokeMatch
{
    public static void Report(WalkContext ctx, string id, string method, string? type)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (!Same(Names.Invocation(node), method))
            {
                continue;
            }

            if (type is not null && !Same(Names.InvocationType(node), type))
            {
                continue;
            }

            ctx.Report(id, node);
        }
    }

    public static void Any(WalkContext ctx, string id, string[] methods)
    {
        foreach (var method in methods)
        {
            Report(ctx, id, method, null);
        }
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}
