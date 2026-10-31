using System.Text.Json;
using Microsoft.CodeAnalysis;
using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

// The taint and flow probe corpus of plan 03. Each probe is a small compiling program, the Checks it
// is scored on and the Findings it should give among them. A probe marked pending names the phase
// expected to fix it, and must still miss: a phase that fixes a probe has to clear its marker, so
// the file always shows what is done and what is not.
public class TaintProbeTests
{
    private const string ProbeFile = "tests/Qlcheck.Tests/Catalog/Dataflow/taint-probes.json";

    private const string Header = """
        using System;
        using System.Collections.Generic;
        using System.Data.Common;
        using System.Diagnostics;
        using System.IO;
        using System.IO.Compression;
        using System.Linq;
        using System.Text;
        using System.Text.RegularExpressions;
        using System.Threading;
        using System.Threading.Tasks;
        using System.Xml;

        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Lazy<Dictionary<string, Probe>> Probes = new(Load);

    public static TheoryData<string> Ready() => Names(probe => probe.Pending is null);

    public static TheoryData<string> Pending() => Names(probe => probe.Pending is not null);

    [Theory]
    [MemberData(nameof(Ready))]
    public void Probe_gives_exactly_its_expected_findings(string name)
    {
        var probe = Probes.Value[name];

        Assert.Equal(probe.Expect.Order(StringComparer.Ordinal), Found(name, probe));
    }

    [Theory]
    [MemberData(nameof(Pending))]
    public void Pending_probe_still_misses(string name)
    {
        var probe = Probes.Value[name];

        Assert.NotEqual(probe.Expect.Order(StringComparer.Ordinal), Found(name, probe));
    }

    [Fact]
    public void Probes_compile_without_errors()
    {
        var failing = Probes.Value
            .Where(pair => Compile(pair.Key, pair.Value).Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            .Select(pair => pair.Key);

        Assert.Empty(failing);
    }

    private static IReadOnlyList<string> Found(string name, Probe probe)
    {
        var (file, tree, compilation) = Compile(name, probe);
        var selected = probe.Checks.ToHashSet(StringComparer.Ordinal);
        var messages = probe.Checks.ToDictionary(id => id, id => Catalog.Messages[id], StringComparer.Ordinal);
        return CatalogWalker.Walk(file, tree, compilation.GetSemanticModel(tree), selected, messages)
            .Select(finding => finding.Check)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static (SourceFile File, SyntaxTree Tree, Compilation Compilation) Compile(string name, Probe probe)
    {
        var file = new SourceFile($"{name}.cs", $"{Header}namespace P_{name}\n{{\n{probe.Source}\n}}\n");
        var tree = CSharpTrees.Parse(file);
        return (file, tree, CSharpCompilations.Create(name, [tree]));
    }

    private static TheoryData<string> Names(Func<Probe, bool> filter) =>
        new(Probes.Value.Where(pair => filter(pair.Value)).Select(pair => pair.Key));

    private static Dictionary<string, Probe> Load()
    {
        var path = Path.Combine(RepoPaths.Root(), ProbeFile);
        return JsonSerializer.Deserialize<Dictionary<string, Probe>>(File.ReadAllText(path), JsonOptions) ?? [];
    }

    private sealed record Probe(string Tag, string[] Expect, string[] Checks, string Source, string? Pending);
}
