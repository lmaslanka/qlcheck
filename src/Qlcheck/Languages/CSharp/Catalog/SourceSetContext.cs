// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog;

// What a run-wide Check sees: every scanned C# file of the run, across Compilations, sorted by
// path so the result never depends on load order. Syntax only; there is no semantic model.
internal sealed class SourceSetContext
{
    private readonly IReadOnlyDictionary<string, string> _messages;

    private readonly List<Finding> _findings = [];

    private IReadOnlyList<CloneGroup>? _clones;

    public SourceSetContext(IEnumerable<SourceSetFile> files, IReadOnlyDictionary<string, string> messages)
    {
        Files = [.. files.OrderBy(file => file.Path, StringComparer.Ordinal)];
        _messages = messages;
    }

    public IReadOnlyList<SourceSetFile> Files { get; }

    // The run's duplicated statement sequences, found once and shared by duplicate-code and
    // identical-method.
    public IReadOnlyList<CloneGroup> Clones => _clones ??= CloneIndex.Find(Files);

    public IReadOnlyList<Finding> Findings => _findings;

    public string Message(string id) => _messages[id];

    public void ReportCustom(string id, string path, Location location, string message) =>
        _findings.Add(Finding.At(id, path, location, message));
}
