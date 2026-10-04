namespace Qlcheck.Cli;

internal static class Cli
{
    private const string Usage = "Usage: qlcheck [--human] [--stats] [--check <id>] [--coverage] [--unstaged] <path>...";

    private const string HelpOption = "--help";

    private const string HelpShortOption = "-h";

    private const string HumanOption = "--human";

    private const string StatsOption = "--stats";

    private const string CoverageOption = "--coverage";

    private const string CoverageCheckId = "coverage";

    private const string UnstagedOption = "--unstaged";

    private const string CurrentDirectory = ".";

    private const string CheckOption = "--check";

    private const string OptionPrefix = "--";

    internal sealed record Options(
        bool Human,
        bool Stats,
        bool Unstaged,
        IReadOnlyList<string> EnableIds,
        IReadOnlyList<string> CheckIds,
        IReadOnlyList<string> Paths);

    public static Options? TryParse(IReadOnlyList<string> args, TextWriter stderr)
    {
        var state = new ParserState();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is HelpOption or HelpShortOption)
            {
                stderr.WriteLine(Usage);
                return null;
            }

            if (TryHandleSimpleFlag(arg, state))
            {
                continue;
            }

            if (arg == CheckOption)
            {
                var nextIndex = TryParseCheckValue(args, i, state.CheckIds, stderr);
                if (nextIndex is null)
                {
                    return null;
                }

                i = nextIndex.Value;
                continue;
            }

            if (arg.StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                stderr.WriteLine($"Unknown option: {arg}");
                stderr.WriteLine(Usage);
                return null;
            }

            state.Paths.Add(arg);
        }

        if (state.Paths.Count == 0)
        {
            if (!state.Unstaged)
            {
                stderr.WriteLine(Usage);
                return null;
            }

            state.Paths.Add(CurrentDirectory);
        }

        return new Options(state.Human, state.Stats, state.Unstaged, state.EnableIds, state.CheckIds, state.Paths);
    }

    private static bool TryHandleSimpleFlag(string arg, ParserState state)
    {
        switch (arg)
        {
            case HumanOption:
                state.Human = true;
                return true;
            case StatsOption:
                state.Stats = true;
                return true;
            case UnstagedOption:
                state.Unstaged = true;
                return true;
            case CoverageOption:
                state.EnableIds.Add(CoverageCheckId);
                return true;
            default:
                return false;
        }
    }

    private static int? TryParseCheckValue(IReadOnlyList<string> args, int i, ICollection<string> checkIds, TextWriter stderr)
    {
        if (i + 1 >= args.Count)
        {
            stderr.WriteLine("Missing value for --check.");
            stderr.WriteLine(Usage);
            return null;
        }

        checkIds.Add(args[i + 1]);
        return i + 1;
    }

    private sealed class ParserState
    {
        public bool Human { get; set; }

        public bool Stats { get; set; }

        public bool Unstaged { get; set; }

        public List<string> EnableIds { get; } = [];

        public List<string> CheckIds { get; } = [];

        public List<string> Paths { get; } = [];
    }
}
