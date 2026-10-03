// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog;

internal sealed class CompilationContext
{
    private readonly IReadOnlyDictionary<string, string> _messages;

    public CompilationContext(
        Compilation compilation,
        IReadOnlySet<string> includedFiles,
        IReadOnlyDictionary<string, string> messages)
    {
        Compilation = compilation;
        IncludedFiles = includedFiles;
        _messages = messages;
    }

    public Compilation Compilation { get; }

    public IReadOnlySet<string> IncludedFiles { get; }

    public List<Finding> Findings { get; } = [];

    public void Report(string id, string path, Location location, string? replacement = null) =>
        Findings.Add(Finding.At(id, path, location, _messages[id], replacement));
}
