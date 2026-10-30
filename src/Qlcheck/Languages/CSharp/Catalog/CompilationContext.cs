// Copyright (c) qlcheck contributors.
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog;

internal sealed class CompilationContext
{
    private readonly IReadOnlyDictionary<string, string> _messages;

    private readonly Lazy<ImmutableArray<Diagnostic>> _diagnostics;

    private readonly Lazy<IReadOnlySet<SyntaxTree>> _errorTrees;

    public CompilationContext(
        Compilation compilation,
        IReadOnlySet<SyntaxTree> reportableTrees,
        IReadOnlyDictionary<string, string> messages,
        bool hasNullableAnnotatedTarget = true)
    {
        Compilation = compilation;
        HasNullableAnnotatedTarget = hasNullableAnnotatedTarget;
        ReportableTrees = reportableTrees;
        _messages = messages;
        _diagnostics = new(() => compilation.GetDiagnostics());
        _errorTrees = new(() => Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Location.SourceTree)
            .OfType<SyntaxTree>()
            .ToHashSet());
    }

    public Compilation Compilation { get; }

    // The scanned sources. Other trees in the compilation (such as the project's global usings) are
    // context: they are bound against but never reported on.
    public IReadOnlySet<SyntaxTree> ReportableTrees { get; }

    // False for a project whose targets all lack framework nullable annotations (ProjectSettings).
    public bool HasNullableAnnotatedTarget { get; }

    // One GetDiagnostics() per compilation, shared by every row that reads compiler diagnostics.
    public ImmutableArray<Diagnostic> Diagnostics => _diagnostics.Value;

    // Trees with any compile error: their semantic model is incomplete, so diagnostic-based Findings
    // there could be wrong only because of what is missing (ADR 0003).
    public IReadOnlySet<SyntaxTree> ErrorTrees => _errorTrees.Value;

    public List<Finding> Findings { get; } = [];

    public string Message(string id) => _messages[id];

    public void Report(string id, string path, Location location, string? replacement = null) =>
        Findings.Add(Finding.At(id, path, location, _messages[id], replacement));

    public void ReportCustom(string id, string path, Location location, string message, string? replacement = null) =>
        Findings.Add(Finding.At(id, path, location, message, replacement));

    public bool Includes(SyntaxTree tree) => ReportableTrees.Contains(tree);

    public static string PathOf(SyntaxTree tree) => tree.FilePath.Replace('\\', '/');
}
