using Qlcheck.Checks;
using Qlcheck.Languages;
using Qlcheck.Scan;

namespace Qlcheck.Run;

internal static class CheckRun
{
    public static RunResult Execute(
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        IReadOnlyList<ICheck> checks,
        IReadOnlyList<ILanguage> languages,
        DisableFiles? disabled = null)
    {
        EnsureOwned(checks, languages);
        EnsureOneLanguage(loaded, languages);
        var findings = new List<Finding>();
        var coverage = new List<CoverageFile>();
        foreach (var language in languages.OrderBy(language => language.Id, StringComparer.Ordinal))
        {
            var files = loaded.Where(source => language.Matches(source.FullPath)).ToList();
            if (files.Count == 0)
            {
                continue;
            }

            var mine = checks.Where(check => check.Language == language.Id).ToList();
            if (mine.Count == 0)
            {
                continue;
            }

            var result = language.Execute(files, mine);
            findings.AddRange(result.Findings);
            coverage.AddRange(result.Coverage);
        }

        var kept = Suppressions.Filter(findings, loaded);
        return new RunResult(disabled is null ? kept : WithoutDisabled(kept, loaded, disabled), coverage);
    }

    // Cross-file Checks still saw files in a disabled tree; only their Findings there are dropped.
    private static IReadOnlyList<Finding> WithoutDisabled(
        IReadOnlyList<Finding> findings,
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        DisableFiles disabled)
    {
        var fullPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in loaded)
        {
            fullPaths[source.File.Path] = source.FullPath;
        }

        return findings
            .Where(finding => !fullPaths.TryGetValue(finding.File, out var fullPath)
                              || !disabled.IsDisabled(finding.Check, fullPath))
            .ToList();
    }

    private static void EnsureOwned(IReadOnlyList<ICheck> checks, IReadOnlyList<ILanguage> languages)
    {
        foreach (var check in checks)
        {
            var owners = languages.Count(language => language.Id == check.Language);
            if (owners == 0)
            {
                throw new InvalidOperationException(
                    $"Check '{check.Id}' has no language '{check.Language}'.");
            }

            if (owners > 1)
            {
                throw new InvalidOperationException($"Check '{check.Id}' matches multiple languages.");
            }
        }
    }

    private static void EnsureOneLanguage(
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        IReadOnlyList<ILanguage> languages)
    {
        foreach (var source in loaded)
        {
            var matches = languages.Count(language => language.Matches(source.FullPath));
            if (matches > 1)
            {
                throw new InvalidOperationException($"Multiple languages match {source.FullPath}.");
            }
        }
    }
}
