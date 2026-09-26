using Qlcheck.Checks;
using Qlcheck.Languages.CSharp.Checks.Compilation;
using Qlcheck.Languages.CSharp.Checks.File;
using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp;

internal sealed class CSharpLanguage : ILanguage
{
    public const string LanguageId = "csharp";

    private const string Extension = ".cs";

    private const string AdhocCompilationName = "adhoc";

    public string Id => LanguageId;

    public bool Matches(string path) =>
        path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<Finding> Execute(
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        IReadOnlyList<ICheck> checks)
    {
        foreach (var check in checks)
        {
            if (check is not IFileCheck and not ICompilationCheck)
            {
                throw new InvalidOperationException($"Check '{check.Id}' is not a C# check.");
            }
        }

        var fileChecks = checks.OfType<IFileCheck>().ToList();
        var compilationChecks = checks.OfType<ICompilationCheck>().ToList();
        var parsed = loaded
            .Select(source => (source, Tree: CSharpTrees.Parse(source.File)))
            .ToList();
        var findings = parsed
            .AsParallel()
            .AsOrdered()
            .SelectMany(item => fileChecks.SelectMany(check => check.Analyze(item.source.File, item.Tree)))
            .ToList();
        if (compilationChecks.Count == 0)
        {
            return findings;
        }

        var included = parsed
            .Select(item => item.source.File.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trees = parsed.ToDictionary(item => item.source.FullPath, item => item.Tree);
        foreach (var group in loaded.GroupBy(source => CSharpCompilations.FindCsproj(source.FullPath) ?? string.Empty))
        {
            var name = string.IsNullOrEmpty(group.Key)
                ? AdhocCompilationName
                : Path.GetFileNameWithoutExtension(group.Key);
            var compilation = CSharpCompilations.Create(name, group.Select(source => trees[source.FullPath]));
            foreach (var check in compilationChecks)
            {
                findings.AddRange(check.AnalyzeCompilation(compilation, included));
            }
        }

        return findings;
    }
}
