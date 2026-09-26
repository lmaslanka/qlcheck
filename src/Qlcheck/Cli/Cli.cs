namespace Qlcheck.Cli;

internal static class Cli
{
    private const string Usage = "Usage: qlcheck [--human] [--stats] [--check <id>] [--inline-sql] <path>...";

    private const string HelpOption = "--help";

    private const string HelpShortOption = "-h";

    private const string HumanOption = "--human";

    private const string StatsOption = "--stats";

    private const string InlineSqlOption = "--inline-sql";

    private const string InlineSqlCheckId = "inline-sql";

    private const string CheckOption = "--check";

    private const string OptionPrefix = "--";

    internal sealed record Options(
        bool Human,
        bool Stats,
        IReadOnlyList<string> EnableIds,
        IReadOnlyList<string> CheckIds,
        IReadOnlyList<string> Paths);

    public static Options? TryParse(IReadOnlyList<string> args, TextWriter stderr)
    {
        var human = false;
        var stats = false;
        var enableIds = new List<string>();
        var checkIds = new List<string>();
        var paths = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is HelpOption or HelpShortOption)
            {
                stderr.WriteLine(Usage);
                return null;
            }

            if (arg == HumanOption)
            {
                human = true;
                continue;
            }

            if (arg == StatsOption)
            {
                stats = true;
                continue;
            }

            if (arg == InlineSqlOption)
            {
                enableIds.Add(InlineSqlCheckId);
                continue;
            }

            if (arg == CheckOption)
            {
                if (i + 1 >= args.Count)
                {
                    stderr.WriteLine("Missing value for --check.");
                    stderr.WriteLine(Usage);
                    return null;
                }

                checkIds.Add(args[++i]);
                continue;
            }

            if (arg.StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                stderr.WriteLine($"Unknown option: {arg}");
                stderr.WriteLine(Usage);
                return null;
            }

            paths.Add(arg);
        }

        if (paths.Count == 0)
        {
            stderr.WriteLine(Usage);
            return null;
        }

        return new Options(human, stats, enableIds, checkIds, paths);
    }
}
