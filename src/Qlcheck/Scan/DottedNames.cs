namespace Qlcheck.Scan;

// Namespace or type names written with dots. A name matches itself and everything below it on dot
// boundaries: `Qlcheck.Scan` matches `Qlcheck.Scan.GitChanges` but not `Qlcheck.Scanner`.
internal static class DottedNames
{
    public static bool Matches(string pattern, string name) =>
        name.Length == pattern.Length
            ? string.Equals(name, pattern, StringComparison.Ordinal)
            : name.Length > pattern.Length && name[pattern.Length] == '.' && name.StartsWith(pattern, StringComparison.Ordinal);

    public static bool IsValid(string name) =>
        name.Length > 0 && name.Split('.').All(part => part.Length > 0
            && (char.IsLetter(part[0]) || part[0] == '_')
            && part.All(character => char.IsLetterOrDigit(character) || character == '_'));
}
