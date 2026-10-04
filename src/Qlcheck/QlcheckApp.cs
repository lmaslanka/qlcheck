using System.Diagnostics;
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
        try
        {
            languages = LanguageDiscovery.All();
            loaded = SourceScan.Load(options.Paths, path => languages.Any(language => language.Matches(path)));
            reported = options.Unstaged ? OnlyUnstaged(loaded, options.Paths) : loaded;
        }
        catch (CaughtException ex)
        {
            stderr.WriteLine(ex.Message);
            return ExitCode.Error;
        }

        var timer = Stopwatch.StartNew();
        RunResult run;
        try
        {
            run = reported.Count == 0 ? new RunResult([], []) : CheckRun.Execute(loaded, checks, languages);
        }
        catch (CaughtException ex)
        {
            stderr.WriteLine(ex.Message);
            return ExitCode.Error;
        }

        timer.Stop();

        if (options.Unstaged)
        {
            run = InFiles(run, reported);
        }

        var output = stdout;
        Report.Report.Write(options, reported, checks, run.Findings, run.Coverage, timer.Elapsed, output);
        return run.Findings.Count == 0 ? ExitCode.Clean : ExitCode.Findings;
    }

    // Checks still run on every loaded file so cross-file Checks see the whole tree; only what is
    // reported is narrowed to files with unstaged changes.
    private static IReadOnlyList<SourceScan.LoadedSource> OnlyUnstaged(
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        IReadOnlyList<string> paths)
    {
        var changed = GitChanges.Unstaged(paths);
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
