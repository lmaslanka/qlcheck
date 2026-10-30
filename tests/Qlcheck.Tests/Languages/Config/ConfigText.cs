using Qlcheck.Languages.Config.Readers;

namespace Qlcheck.Tests;

// Expected reader entries, positioned by finding the value's first occurrence in the text rather
// than by hand-counted line and column numbers.
internal static class ConfigText
{
    public static ConfigEntry Entry(string text, string keyPath, string value, string? locate = null)
    {
        var (line, column) = PositionOf(text, locate ?? value);
        return ConfigEntry.At(keyPath, value, line, column);
    }

    public static (int Line, int Column) PositionOf(string text, string needle)
    {
        var index = text.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(index >= 0, $"'{needle}' is not in the text.");
        var lineStart = index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
        var line = text[..index].Count(ch => ch == '\n') + 1;
        return (line, index - lineStart + 1);
    }
}
