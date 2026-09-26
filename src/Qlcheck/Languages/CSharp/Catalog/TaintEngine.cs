// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class TaintEngine
{
    public static readonly WalkRow[] Rows =
    [
        Row.Handle("allocation-dos", CheckClass.Taint, Patterns.AllocDos),
        Row.Taint("argument-injection", CheckClass.Taint, "ArgumentList"),
        Row.Taint("command-argument-injection", CheckClass.Taint, "Arguments"),
        Row.Taint("command-injection", CheckClass.Taint, "Start"),
        Row.Taint("connection-string-injection", CheckClass.Taint, "ConnectionString"),
        Row.Taint("deserialization-injection", CheckClass.Taint, "Deserialize"),
        Row.Taint("dynamic-code-injection", CheckClass.Taint, "Evaluate"),
        Row.Taint("filesystem-oracle", CheckClass.Taint, "Exists"),
        Row.Taint("ldap-injection", CheckClass.Taint, "Search"),
        Row.Taint("log-injection", CheckClass.Taint, "Information"),
        Row.Handle("loop-bound-injection", CheckClass.Taint, Patterns.LoopBound),
        Row.Taint("no-stack-trace-disclosure", CheckClass.Taint, "Write"),
        Row.Taint("nosql-injection", CheckClass.Taint, "Find"),
        Row.Taint("open-redirect", CheckClass.Taint, "Redirect"),
        Row.Taint("path-injection", CheckClass.Taint, "Combine"),
        Row.Taint("reflected-xss", CheckClass.Taint, "Write"),
        Row.Taint("reflection-injection", CheckClass.Taint, "GetType"),
        Row.Taint("regex-dos", CheckClass.Taint, "Regex"),
        Row.Taint("sql-injection", CheckClass.Taint, "Execute"),
        Row.Taint("ssrf", CheckClass.Taint, "GetAsync"),
        Row.Taint("ssrf-traversal", CheckClass.Taint, "SendAsync"),
        Row.Taint("untrusted-environment-variable", CheckClass.Taint, "SetEnvironmentVariable"),
        Row.Taint("untrusted-session-cookie", CheckClass.Taint, "Append"),
        Row.Taint("xml-injection", CheckClass.Taint, "LoadXml"),
        Row.Taint("xpath-injection", CheckClass.Taint, "SelectNodes"),
        Row.Taint("xslt-injection", CheckClass.Taint, "Transform"),
        Row.Taint("zip-slip", CheckClass.Taint, "ExtractToFile"),
    ];

    public static void Apply(WalkContext ctx, IReadOnlySet<string> selected)
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
