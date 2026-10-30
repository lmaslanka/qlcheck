// Copyright (c) qlcheck contributors.
using System.Text;

namespace Qlcheck.Secrets;

// Decides from a name (a field, property, parameter or configuration key) whether the value it
// holds is meant to be a password or another secret. Names whose last word says the value is
// about a secret rather than the secret itself (PasswordPolicy, TokenTypeName, ApiKeyHeader)
// do not count.
internal static class SecretNames
{
    private const string KeyWord = "key";

    private static readonly HashSet<string> PasswordWords = new(StringComparer.Ordinal)
    {
        "password", "passwd", "pwd", "passphrase", "credential", "credentials",
    };

    private static readonly HashSet<string> SecretWords = new(StringComparer.Ordinal)
    {
        "secret", "token", "apikey", "accesskey", "privatekey", "clientsecret", "signingkey", "sas", "bearer",
    };

    // Words that, written before `Key`, make one secret word: ApiKey, AccessKey, PrivateKey, SigningKey.
    private static readonly HashSet<string> KeyPrefixes = new(StringComparer.Ordinal)
    {
        "api", "access", "private", "signing",
    };

    // A last word that says what the value is *about*, not that it is the secret.
    private static readonly HashSet<string> NonSecretLastWords = new(StringComparer.Ordinal)
    {
        "length", "policy", "hash", "salt", "regex", "pattern", "field", "label", "prompt", "hint", "name",
        "header", "path", "file", "url", "uri", "endpoint", "type", "id", "expiry", "expiration", "lifetime",
        "ttl", "count", "min", "max", "required", "reset", "validator", "error", "message", "placeholder",
        "format", "section", "variable", "var", "column", "claim", "scheme", "provider", "handler", "options",
    };

    // Words before a trailing `Key` that make the name a lookup key rather than a key: CacheKey,
    // ConfigKey, PasswordSettingKey.
    private static readonly HashSet<string> LookupWords = new(StringComparer.Ordinal)
    {
        "config", "configuration", "setting", "settings", "resource", "cache", "section", "env", "environment",
        "storage", "lookup", "registry", "map", "dictionary", "claim", "header", "query", "route", "form",
        "cookie", "session", "item", "property", "column", "json", "xml",
    };

    private static readonly HashSet<string> JwtSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "Jwt", "JwtSettings", "JwtBearer", "Bearer",
    };

    private static readonly HashSet<string> JwtKeyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Key", "Secret", "SigningKey", "IssuerSigningKey",
    };

    private static readonly char[] KeySeparators = [':', '.'];

    public static SecretNameKind Classify(string? name)
    {
        var words = Words(name);
        if (words.Count == 0 || NonSecretLastWords.Contains(words[^1]) || IsLookupKey(words))
        {
            return SecretNameKind.None;
        }

        if (words.Any(PasswordWords.Contains))
        {
            return SecretNameKind.Password;
        }

        return Compounds(words).Any(SecretWords.Contains) ? SecretNameKind.Secret : SecretNameKind.None;
    }

    // A configuration key path such as `Jwt:Key` or `JwtSettings:IssuerSigningKey` that holds a
    // JWT signing key.
    public static bool IsJwtKeyPath(string? keyPath)
    {
        var segments = Segments(keyPath);
        return segments.Count > 1
            && JwtKeyNames.Contains(segments[^1])
            && segments.Take(segments.Count - 1).Any(JwtSections.Contains);
    }

    // The last segment of a configuration key (`ConnectionStrings:Default`, `Jwt__Key`), which is
    // the name the value is stored under.
    public static string? LastSegment(string? keyPath)
    {
        var segments = Segments(keyPath);
        return segments.Count == 0 ? null : segments[^1];
    }

    // Splits camelCase, PascalCase, ACRONYMWords, snake_case, kebab-case and dotted or `:` keys
    // into lower-case words.
    public static IReadOnlyList<string> Words(string? name)
    {
        var words = new List<string>();
        if (string.IsNullOrEmpty(name))
        {
            return words;
        }

        var current = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (!char.IsLetterOrDigit(ch))
            {
                Flush(words, current);
                continue;
            }

            if (current.Length > 0 && StartsWord(name, i))
            {
                Flush(words, current);
            }

            current.Append(char.ToLowerInvariant(ch));
        }

        Flush(words, current);
        return words;
    }

    private static bool IsLookupKey(IReadOnlyList<string> words) =>
        words.Count > 1
        && words[^1] == KeyWord
        && (LookupWords.Contains(words[^2]) || words.Take(words.Count - 1).Any(PasswordWords.Contains));

    // Each word, plus each adjacent pair joined (`api` + `key` -> `apikey`, `client` + `secret`).
    private static IEnumerable<string> Compounds(IReadOnlyList<string> words)
    {
        for (var i = 0; i < words.Count; i++)
        {
            yield return words[i];
            if (i + 1 < words.Count && (words[i + 1] != KeyWord || KeyPrefixes.Contains(words[i])))
            {
                yield return words[i] + words[i + 1];
            }
        }
    }

    private static IReadOnlyList<string> Segments(string? keyPath)
    {
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            return [];
        }

        return keyPath.Replace("__", ":", StringComparison.Ordinal)
            .Split(KeySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // A new word starts at lower->Upper, letter<->digit, and at the last capital of an acronym
    // followed by lower case (`APIKey` -> `API`, `Key`).
    private static bool StartsWord(string name, int i)
    {
        var previous = name[i - 1];
        var ch = name[i];
        if (char.IsDigit(previous) != char.IsDigit(ch))
        {
            return true;
        }

        if (char.IsLower(previous) && char.IsUpper(ch))
        {
            return true;
        }

        return char.IsUpper(previous) && char.IsUpper(ch) && i + 1 < name.Length && char.IsLower(name[i + 1]);
    }

    private static void Flush(List<string> words, StringBuilder current)
    {
        if (current.Length > 0)
        {
            words.Add(current.ToString());
            current.Clear();
        }
    }
}
