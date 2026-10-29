using System.Diagnostics;
using Qlcheck.Checks;
using Qlcheck.Languages;
using Qlcheck.Run;
using Qlcheck.Scan;
using CaughtException = System.Exception;

namespace Qlcheck;

public static class QlcheckApp
{
    public static ExitCode Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
    {
        var options = Cli.Cli.TryParse(args, stderr);
        if (options is null)
        {
            return ExitCode.Error;
        }

        if (!CheckDiscovery.TrySelect(options.CheckIds, options.EnableIds, out var checks, stderr))
        {
            return ExitCode.Error;
        }

        IReadOnlyList<SourceScan.LoadedSource> loaded;
        IReadOnlyList<SourceScan.LoadedSource> reported;
        IReadOnlyList<ILanguage> languages;
        DisableFiles disabled;
        try
        {
            languages = LanguageDiscovery.All();
            loaded = SourceScan.Load(options.Paths, path => languages.Any(language => language.Matches(path)));
            reported = options.Uncommitted ? OnlyUncommitted(loaded, options.Paths) : loaded;
            disabled = DisableFiles.Load(options.Paths, loaded.Select(source => source.FullPath));
        }
        catch (CaughtException ex)
        {
            stderr.WriteLine(ex.Message);
            return ExitCode.Error;
        }

        if (!CheckDiscovery.TryValidateDisabled(disabled.Entries, stderr))
        {
            return ExitCode.Error;
        }

        disabled = disabled.Without([.. options.CheckIds, .. options.EnableIds]);
        checks = Running(checks, loaded, disabled);

        var timer = Stopwatch.StartNew();
        RunResult run;
        try
        {
            run = reported.Count == 0 ? new RunResult([], []) : CheckRun.Execute(loaded, checks, languages, disabled);
        }
        catch (CaughtException ex)
        {
            stderr.WriteLine(ex.Message);
            return ExitCode.Error;
        }

        timer.Stop();

        if (options.Uncommitted)
        {
            run = InFiles(run, reported);
        }

        var output = stdout;
        Report.Report.Write(options, reported, checks, run, disabled.Disabled.ToList(), timer.Elapsed, output);
        return run.Findings.Count == 0 ? ExitCode.Clean : ExitCode.Findings;
    }

    // A Check disabled for every loaded file is not run at all; one disabled for only some files still
    // runs, and CheckRun drops its Findings in the disabled trees.
    private static IReadOnlyList<ICheck> Running(
        IReadOnlyList<ICheck> checks,
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        DisableFiles disabled)
    {
        var mentioned = disabled.Disabled.Select(entry => entry.Check).ToHashSet(StringComparer.Ordinal);
        return checks
            .Where(check => !mentioned.Contains(check.Id)
                            || loaded.Any(source => !disabled.IsDisabled(check.Id, source.FullPath)))
            .ToList();
    }

    // Checks still run on every loaded file so cross-file Checks see the whole tree; only what is
    // reported is narrowed to files that are not committed.
    private static IReadOnlyList<SourceScan.LoadedSource> OnlyUncommitted(
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        IReadOnlyList<string> paths)
    {
        var changed = GitChanges.Uncommitted(paths);
        return loaded.Where(source => changed.Contains(source.FullPath)).ToList();
    }

    private static RunResult InFiles(RunResult run, IReadOnlyList<SourceScan.LoadedSource> reported)
    {
        var files = reported.Select(source => source.File.Path).ToHashSet(StringComparer.Ordinal);
        return new RunResult(
            run.Findings.Where(finding => files.Contains(finding.File)).ToList(),
            run.Coverage.Where(file => files.Contains(file.File)).ToList());
    }
}
