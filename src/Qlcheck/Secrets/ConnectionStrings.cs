// Copyright (c) qlcheck contributors.
using System.Text.RegularExpressions;

namespace Qlcheck.Secrets;

// Finds a real password in a database connection string: `key=value;` pairs, or a database URI
// with `user:password@`. An empty or placeholder password is not one.
internal static partial class ConnectionStrings
{
    private const int TimeoutMilliseconds = 200;

    private const int MinPairs = 2;

    private const char PairSeparator = ';';

    private const char KeyValueSeparator = '=';

    private static readonly HashSet<string> TargetKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "server", "data source", "host", "database", "initial catalog", "user id", "uid", "username", "user",
    };

    private static readonly HashSet<string> PasswordKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "pwd",
    };

    public static bool HasPassword(string value) => HasPairPassword(value) || HasUriPassword(value);

    // Every non-empty segment must be a `key=value` pair, so text that merely contains
    // `Password=` (a log line, a whole source file) is not taken for a connection string.
    private static bool HasPairPassword(string value)
    {
        var pairs = new List<(string Key, string Value)>();
        foreach (var segment in value.Split(PairSeparator))
        {
            if (segment.Trim().Length == 0)
            {
                continue;
            }

            var equals = segment.IndexOf(KeyValueSeparator, StringComparison.Ordinal);
            var key = equals < 0 ? string.Empty : segment[..equals].Trim();
            if (!IsKey(key))
            {
                return false;
            }

            pairs.Add((key, Unquote(segment[(equals + 1)..].Trim())));
        }

        return pairs.Count >= MinPairs
            && pairs.Any(pair => TargetKeys.Contains(pair.Key))
            && pairs.Any(pair => PasswordKeys.Contains(pair.Key) && IsRealPassword(pair.Value));
    }

    private static bool HasUriPassword(string value)
    {
        try
        {
            var match = DatabaseUri().Match(value);
            return match.Success && IsRealPassword(Uri.UnescapeDataString(match.Groups["password"].Value));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool IsRealPassword(string password) =>
        !SecretValues.IsPlaceholder(password) && !SecretValues.IsKeyName(password);

    private static bool IsKey(string key) =>
        key.Length > 0 && char.IsLetter(key[0]) && key.All(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '_' or '.' or '-');

    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] is '\'' or '"') && value[^1] == value[0] ? value[1..^1] : value;

    [GeneratedRegex(
        @"\b(?:postgres|postgresql|mysql|mongodb|mongodb\+srv|redis|rediss|amqp|amqps|sqlserver)://[^:/@\s]+:(?<password>[^@/\s]+)@",
        RegexOptions.IgnoreCase,
        TimeoutMilliseconds)]
    private static partial Regex DatabaseUri();
}
