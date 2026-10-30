// Copyright (c) qlcheck contributors.
using System.Text.RegularExpressions;

namespace Qlcheck.Secrets;

// What a value looks like, independent of its name: placeholders, identifiers, keys and URLs are
// never secrets, and a secret-named value must also look random.
internal static partial class SecretValues
{
    private const int TimeoutMilliseconds = 200;

    private const int RandomMinLength = 16;

    private const int RandomMinClasses = 2;

    private const double RandomMinEntropy = 3.0;

    private const string KeyVaultReference = "@Microsoft.KeyVault(";

    private static readonly HashSet<string> PlaceholderWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "changeme", "change-me", "change_me", "password", "secret", "example", "dummy", "sample", "test",
        "none", "null", "todo", "replace_me", "replace-me", "replaceme",
    };

    private static readonly string[] ExampleMarkers = ["EXAMPLE", "FAKE", "DUMMY"];

    public static bool IsPlaceholder(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || PlaceholderWords.Contains(trimmed) || trimmed.All(ch => ch == trimmed[0]))
        {
            return true;
        }

        if (trimmed.StartsWith(KeyVaultReference, StringComparison.OrdinalIgnoreCase) || HasExampleMarker(trimmed))
        {
            return true;
        }

        return Matches(Template(), trimmed) || Matches(YourValueHere(), trimmed);
    }

    public static bool HasExampleMarker(string value) =>
        ExampleMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));

    // One Pascal, camel or snake word without digits, or the name itself
    // (`"JwtSecurityTokenHandler"`, `const string Password = "Password"`).
    public static bool LooksLikeIdentifier(string value, string? name) =>
        Matches(Identifier(), value) || string.Equals(value, name, StringComparison.OrdinalIgnoreCase);

    // Never a secret whatever its name: a placeholder, an identifier, an ALL_CAPS environment
    // variable name, a dotted or `:`-sectioned key, or a URL without user info.
    public static bool IsNonSecret(string value, string? name) =>
        IsPlaceholder(value)
        || LooksLikeIdentifier(value, name)
        || IsKeyName(value)
        || IsUrlWithoutUserInfo(value);

    // An environment variable name or a configuration key, which names a secret rather than being one.
    public static bool IsKeyName(string value) => Matches(EnvironmentVariable(), value) || Matches(SectionedKey(), value);

    public static bool LooksRandom(string value)
    {
        if (value.Length < RandomMinLength || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var classes = (value.Any(char.IsLower) ? 1 : 0)
            + (value.Any(char.IsUpper) ? 1 : 0)
            + (value.Any(char.IsDigit) ? 1 : 0)
            + (value.Any(ch => !char.IsLetterOrDigit(ch)) ? 1 : 0);
        return classes >= RandomMinClasses && Entropy(value) >= RandomMinEntropy;
    }

    // Shannon entropy in bits per character.
    public static double Entropy(string value)
    {
        if (value.Length == 0)
        {
            return 0;
        }

        return value.GroupBy(ch => ch)
            .Select(group => (double)group.Count() / value.Length)
            .Sum(p => -p * Math.Log2(p));
    }

    private static bool IsUrlWithoutUserInfo(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" or "ftp" or "ws" or "wss"
        && uri.UserInfo.Length == 0;

    private static bool Matches(Regex pattern, string value)
    {
        try
        {
            return pattern.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    // <name>, {name}, ${VAR}, ${{ secrets.X }}, $(Var), %VAR%, #{Token}#, __TOKEN__
    [GeneratedRegex(@"^(?:<[^<>]*>|\$?\{.*\}|\$\(.*\)|%[^%]+%|#\{.*\}#|__\w+__)$", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex Template();

    [GeneratedRegex(@"^your[-_ ].*[-_ ]here$", RegexOptions.IgnoreCase, TimeoutMilliseconds)]
    private static partial Regex YourValueHere();

    [GeneratedRegex(@"^[A-Za-z_]+$", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex Identifier();

    [GeneratedRegex(@"^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+$", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex EnvironmentVariable();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(?:(?:[.:]|__)[A-Za-z0-9_]+)+$", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex SectionedKey();
}
