// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.Config.Readers;

// `.env` files: `KEY=VALUE` lines with an optional `export ` prefix. Blank lines and `#` lines are
// skipped, matching quotes are stripped, and an unquoted ` #` starts a trailing comment. A line
// without `=` is not an entry.
internal static class DotEnvReader
{
    private const string ExportPrefix = "export ";

    private const string TrailingComment = " #";

    private const char ByteOrderMark = '﻿';

    public static IReadOnlyList<ConfigEntry> Read(string text)
    {
        var entries = new List<ConfigEntry>();
        var lines = text.TrimStart(ByteOrderMark).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (ReadLine(lines[index].TrimEnd('\r'), index + 1) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private static ConfigEntry? ReadLine(string line, int number)
    {
        var start = line.Length - line.TrimStart().Length;
        if (start == line.Length || line[start] == '#')
        {
            return null;
        }

        if (line.AsSpan(start).StartsWith(ExportPrefix, StringComparison.Ordinal))
        {
            start += ExportPrefix.Length;
        }

        var equals = line.IndexOf('=', start);
        var key = equals < 0 ? string.Empty : line[start..equals].Trim();
        if (key.Length == 0)
        {
            return null;
        }

        var valueStart = equals + 1;
        while (valueStart < line.Length && line[valueStart] is ' ' or '\t')
        {
            valueStart++;
        }

        var (value, column) = Value(line, valueStart);
        return ConfigEntry.At(key, value, number, column);
    }

    private static (string Value, int Column) Value(string line, int start)
    {
        if (start < line.Length && line[start] is '"' or '\'')
        {
            var close = line.IndexOf(line[start], start + 1);
            if (close > start)
            {
                return (line[(start + 1)..close], start + 2);
            }
        }

        var rest = line[start..];
        var comment = rest.IndexOf(TrailingComment, StringComparison.Ordinal);
        return ((comment < 0 ? rest : rest[..comment]).TrimEnd(), start + 1);
    }
}
