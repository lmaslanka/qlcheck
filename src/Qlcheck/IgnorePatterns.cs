using System.Text;
using System.Text.RegularExpressions;

namespace Qlcheck;

public sealed class IgnorePatterns
{
    public const string FileName = ".qlcheck_ignore";

    private static readonly string[] Defaults = ["bin/", "obj/", ".git/"];

    private readonly IReadOnlyList<Rule> _rules;

    private IgnorePatterns(IReadOnlyList<Rule> rules)
    {
        _rules = rules;
    }

    public static IgnorePatterns Parse(IEnumerable<string> lines)
    {
        var rules = new List<Rule>();
        foreach (var line in lines)
        {
            if (TryParseRule(line, out var rule))
            {
                rules.Add(rule);
            }
        }

        return new IgnorePatterns(rules);
    }

    public static IgnorePatterns Load(string scanRoot)
    {
        var lines = new List<string>(Defaults);
        var file = Path.Combine(scanRoot, FileName);
        if (File.Exists(file))
        {
            lines.AddRange(File.ReadAllLines(file));
        }

        return Parse(lines);
    }

    public bool IsIgnored(string relativePath, bool isDirectory)
    {
        var path = relativePath.Replace('\\', '/').Trim('/');
        if (path.Length == 0 || path == ".")
        {
            return false;
        }

        var ignored = false;
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

    private static bool TryParseRule(string line, out Rule rule)
    {
        rule = null!;
        var raw = line.Trim();
        if (raw.Length == 0 || raw.StartsWith('#'))
        {
            return false;
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
            return false;
        }

        var anchored = raw.StartsWith('/') || raw.Contains('/');
        if (raw.StartsWith('/'))
        {
            raw = raw[1..];
        }

        if (raw.StartsWith("**/"))
        {
            anchored = false;
            raw = raw[3..];
        }

        var regex = new Regex(
            GlobToRegex(raw, anchored),
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
        rule = new Rule(negation, directoryOnly, regex);
        return true;
    }

    private static string GlobToRegex(string glob, bool anchored)
    {
        var sb = new StringBuilder();
        sb.Append(anchored ? "^" : "(?:^|/)");
        for (var i = 0; i < glob.Length; i++)
        {
            if (glob[i] == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                i++;
                if (i + 1 < glob.Length && glob[i + 1] == '/')
                {
                    i++;
                    sb.Append("(?:.*/)?");
                }
                else
                {
                    sb.Append(".*");
                }

                continue;
            }

            sb.Append(glob[i] switch
            {
                '*' => "[^/]*",
                '?' => "[^/]",
                _ => Regex.Escape(glob[i].ToString()),
            });
        }

        sb.Append("(?:/|$)");
        return sb.ToString();
    }

    private sealed record Rule(bool Negation, bool DirectoryOnly, Regex Regex);
}
