// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp.Catalog;

internal sealed class WalkContext
{
    private const string TestMarker = "Test";

    private readonly IReadOnlyDictionary<string, string> _messages;

    private readonly ILookup<SyntaxKind, SyntaxNode> _byKind;

    internal WalkContext(
        string path,
        string text,
        SyntaxTree tree,
        SemanticModel model,
        IReadOnlyDictionary<string, string> messages,
        ILookup<SyntaxKind, SyntaxNode> byKind)
    {
        Path = path;
        Text = text;
        Tree = tree;
        Model = model;
        _messages = messages;
        _byKind = byKind;
        IsTestFile = path.Contains(TestMarker, StringComparison.OrdinalIgnoreCase);
    }

    public string Path { get; }

    public string Text { get; }

    public SyntaxTree Tree { get; }

    public SemanticModel Model { get; }

    public bool IsTestFile { get; }

    public List<Finding> Findings { get; } = [];

    public static WalkContext Create(
        SourceFile file,
        SyntaxTree tree,
        SemanticModel model,
        IReadOnlyDictionary<string, string> messages)
    {
        var byKind = tree.GetRoot().DescendantNodes().ToLookup(node => node.Kind());
        return new WalkContext(file.Path, file.Text, tree, model, messages, byKind);
    }

    public IEnumerable<SyntaxNode> Nodes(SyntaxKind kind) => _byKind[kind];

    public string Message(string id) => _messages[id];

    public void Report(string id, SyntaxNode node) =>
        Findings.Add(Finding.At(id, Path, node, _messages[id]));

    public void Report(string id, SyntaxToken token) =>
        Findings.Add(Finding.At(id, Path, token, _messages[id]));

    public void Report(string id, Location location) =>
        Findings.Add(Finding.At(id, Path, location, _messages[id]));

    public void Report(string id, SyntaxNode node, string detail)
    {
        var message = $"{_messages[id]} {detail}";
        Findings.Add(Finding.At(id, Path, node, message));
    }
}
