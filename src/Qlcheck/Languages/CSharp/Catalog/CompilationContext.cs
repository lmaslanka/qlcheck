// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog;

internal sealed class CompilationContext
{
    private readonly IReadOnlyDictionary<string, string> _messages;

    public CompilationContext(
        Compilation compilation,
        IReadOnlySet<SyntaxTree> reportableTrees,
        IReadOnlyDictionary<string, string> messages)
    {
        Compilation = compilation;
        ReportableTrees = reportableTrees;
        _messages = messages;
    }

    public Compilation Compilation { get; }

    // The scanned sources. Other trees in the compilation (such as the project's global usings) are
    // context: they are bound against but never reported on.
    public IReadOnlySet<SyntaxTree> ReportableTrees { get; }

    public List<Finding> Findings { get; } = [];

    public void Report(string id, string path, Location location, string? replacement = null) =>
        Findings.Add(Finding.At(id, path, location, _messages[id], replacement));

    public void ReportCustom(string id, string path, Location location, string message, string? replacement = null) =>
        Findings.Add(Finding.At(id, path, location, message, replacement));

    public bool Includes(SyntaxTree tree) => ReportableTrees.Contains(tree);

    public static string PathOf(SyntaxTree tree) => tree.FilePath.Replace('\\', '/');
}
