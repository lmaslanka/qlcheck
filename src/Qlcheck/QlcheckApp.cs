using System.Diagnostics;
using Qlcheck.Languages;
using Qlcheck.Run;
using Qlcheck.Scan;

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
        IReadOnlyList<ILanguage> languages;
        try
        {
            languages = LanguageDiscovery.All();
            loaded = SourceScan.Load(options.Paths, path => languages.Any(language => language.Matches(path)));
        }
        catch (Exception ex)
        {
            stderr.WriteLine(ex.Message);
            return ExitCode.Error;
        }

        var timer = Stopwatch.StartNew();
        RunResult run;
        try
        {
            run = CheckRun.Execute(loaded, checks, languages);
        }
        catch (Exception ex)
        {
            stderr.WriteLine(ex.Message);
            return ExitCode.Error;
        }

        timer.Stop();

        Report.Report.Write(options, loaded, checks, run.Findings, run.Coverage, timer.Elapsed, stdout);
        return run.Findings.Count == 0 ? ExitCode.Clean : ExitCode.Findings;
    }
}
