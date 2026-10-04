namespace Qlcheck.ArchitectureTests.Support;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Roslyn syntax access to the main project's own source tree, for rules that check what a file's
/// `using` directives reach for. Purely syntactic — no compilation, no symbol resolution, matching qlcheck's
/// own "syntax trees, not compilation" approach (see docs/adr/0001).</summary>
public static class Sources
{
    private const string MainProjectFolder = "src/Qlcheck";

    private static readonly Lazy<IReadOnlyList<CSharpFile>> MainProject = new(() => Load(MainProjectFolder));

    /// <summary>Every .cs file under src/Qlcheck, excluding build output.</summary>
    public static IReadOnlyList<CSharpFile> All() => MainProject.Value;

    /// <summary>Files whose path (relative to the repo root, forward-slashed) starts with one of the given
    /// prefixes, e.g. "src/Qlcheck/Cli/".</summary>
    public static IEnumerable<CSharpFile> Under(params string[] relativeFolderPrefixes) =>
        All().Where(file => relativeFolderPrefixes.Any(prefix =>
            file.RelativePath.StartsWith(prefix, StringComparison.Ordinal)));

    /// <summary>The namespace names a file's `using` directives name, e.g. "Qlcheck.Run" or "System.Diagnostics".
    /// Alias and static usings are included by the name they introduce; a global using is still per-file here
    /// since qlcheck's main project does not declare any.</summary>
    public static IEnumerable<string> UsingNames(CSharpFile file) =>
        file.Root.Usings.Select(u => u.Name?.ToString() ?? string.Empty).Where(name => name.Length > 0);

    private static IReadOnlyList<CSharpFile> Load(string relativeFolder)
    {
        var root = System.IO.Path.Combine(RepoRoot.Path, relativeFolder);
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new CSharpFile(
                RepoRoot.RelativePath(path),
                (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot()))
            .ToList();
    }

    private static bool IsBuildOutput(string path)
    {
        var segments = path.Replace('\\', '/').Split('/');
        return segments.Contains("bin") || segments.Contains("obj");
    }
}

/// <summary>A parsed C# source file, identified by its path relative to the repo root.</summary>
public sealed record CSharpFile(string RelativePath, CompilationUnitSyntax Root);
