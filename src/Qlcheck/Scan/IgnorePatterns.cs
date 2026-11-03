using System.Text.RegularExpressions;

namespace Qlcheck.Scan;

public sealed class IgnorePatterns
{
    private const string FileNameValue = ".qlcheck_ignore";

    private const string CurrentDirectory = ".";

    private const string GlobstarPrefix = "**/";

    private const int GlobstarPrefixLength = 3;

    private const int SingleCharTokenLength = 1;

    private const int GlobstarTokenLength = 2;

    private const int GlobstarDirectoryTokenLength = 3;

    private const string AnchoredStart = "^";

    private const string UnanchoredStart = "(?:^|/)";

    private const string GlobstarDirectory = "(?:.*/)?";

    private const string Globstar = ".*";

    private const string StarPattern = "[^/]*";

    private const string QuestionPattern = "[^/]";

    private const string PathEnd = "(?:/|$)";

    private static readonly string[] DefaultLines =
    [
        "bin/",
        "obj/",
        ".git/",
        ".vs/",
        ".idea/",
        ".svn/",
        ".hg/",
        "node_modules/",
        "bower_components/",
        "packages/",
        "TestResults/",
        "coverage/",
        "dist/",
    ];

    private readonly IReadOnlyList<Rule> _rules;

    internal IgnorePatterns(IReadOnlyList<Rule> rules)
    {
        _rules = rules;
    }

    public static string FileName => FileNameValue;

    // Rules from an ignore file match any case.
    public static IgnorePatterns Parse(IEnumerable<string> lines) => Parse(lines, RegexOptions.IgnoreCase);

    // The built-in rules match case exactly, as the tools that write these folders name them, so a
    // `Coverage/` or `Packages/` source folder is still scanned (plan 09).
    public static IgnorePatterns Defaults { get; } = Parse(DefaultLines, RegexOptions.None);

    private static IgnorePatterns Parse(IEnumerable<string> lines, RegexOptions caseOption) =>
        new(lines.Select(line => TryParseRule(line, caseOption)).OfType<Rule>().ToList());

    public bool IsIgnored(string relativePath, bool isDirectory) => Match(relativePath, isDirectory) ?? false;

    // The last rule that matches decides: true ignored, false re-included by `!`, null no rule matched.
    public bool? Match(string relativePath, bool isDirectory)
    {
        var path = relativePath.Replace('\\', '/').Trim('/');
        if (path.Length == 0 || path == CurrentDirectory)
        {
            return null;
        }

        bool? ignored = null;
        foreach (var rule in _rules)
        {
            if (rule.DirectoryOnly && !isDirectory)
            {
                continue;
            }

            if (rule.Regex.IsMatch(path))
            {
                ignored = !rule.Negation;
            }
        }

        return ignored;
    }

    private static Rule? TryParseRule(string line, RegexOptions caseOption)
    {
        var raw = line.Trim();
        if (raw.Length == 0 || raw.StartsWith('#'))
        {
            return null;
        }

        var negation = raw.StartsWith('!');
        if (negation)
        {
            raw = raw[1..];
        }

        var directoryOnly = raw.EndsWith('/');
        if (directoryOnly)
        {
            raw = raw[..^1];
        }

        raw = raw.Replace('\\', '/');
        if (raw.Length == 0)
        {
            return null;
        }

        var anchored = raw.StartsWith('/') || raw.Contains('/');
        if (raw.StartsWith('/'))
        {
            raw = raw[1..];
        }

        if (raw.StartsWith(GlobstarPrefix))
        {
            anchored = false;
            raw = raw[GlobstarPrefixLength..];
        }

        var regex = new Regex(
            GlobToRegex(raw, anchored),
            RegexOptions.CultureInvariant | caseOption | RegexOptions.Compiled);
        return new Rule(negation, directoryOnly, regex);
    }

    private static string GlobToRegex(string glob, bool anchored)
    {
        var segments = new List<string> { anchored ? AnchoredStart : UnanchoredStart };
        var i = 0;
        while (i < glob.Length)
        {
            i += ConsumeToken(glob, i, segments);
        }

        segments.Add(PathEnd);
        return string.Concat(segments);
    }

    private static int ConsumeToken(string glob, int i, List<string> segments)
    {
        if (glob[i] == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
        {
            return ConsumeGlobstar(glob, i, segments);
        }

        segments.Add(glob[i] switch
        {
            '*' => StarPattern,
            '?' => QuestionPattern,
            _ => Regex.Escape(glob[i].ToString()),
        });
        return SingleCharTokenLength;
    }

    private static int ConsumeGlobstar(string glob, int i, List<string> segments)
    {
        if (i + GlobstarTokenLength < glob.Length && glob[i + GlobstarTokenLength] == '/')
        {
            segments.Add(GlobstarDirectory);
            return GlobstarDirectoryTokenLength;
        }

        segments.Add(Globstar);
        return GlobstarTokenLength;
    }

    internal sealed record Rule(bool Negation, bool DirectoryOnly, Regex Regex);
}
