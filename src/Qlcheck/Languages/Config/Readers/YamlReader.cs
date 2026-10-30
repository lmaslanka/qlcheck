// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.Config.Readers;

// A minimal, line-based YAML reader with no dependency. It reads what configuration files use:
// nested `key: value` mappings (sections join with `:`), quoted and unquoted scalars with ` #`
// comments, list items `- key: value` and docker-compose style `- KEY=VALUE`, block scalars `|`
// and `>`, and `---` document breaks. Anchors and aliases, flow collections `{...}`/`[...]`, tags
// and multi-line keys are skipped, never misread; a line it cannot read is not an entry.
internal static class YamlReader
{
    private const string Separator = ":";

    private const string ListMarker = "- ";

    private const string DocumentStart = "---";

    private const string DocumentEnd = "...";

    private const string TrailingComment = " #";

    private const int ListMarkerWidth = 2;

    public static IReadOnlyList<ConfigEntry> Read(string text)
    {
        var lines = text.TrimStart('﻿').Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        var entries = new List<ConfigEntry>();
        var sections = new List<(int Indent, string Key)>();
        for (var index = 0; index < lines.Length; index++)
        {
            index = ReadLine(lines, index, sections, entries);
        }

        return entries;
    }

    // Reads the line at index and returns the index of the last line it consumed.
    private static int ReadLine(string[] lines, int index, List<(int Indent, string Key)> sections, List<ConfigEntry> entries)
    {
        var line = lines[index];
        var content = line.TrimStart(' ');
        var indent = line.Length - content.Length;
        if (content.Length == 0 || content[0] == '#')
        {
            return index;
        }

        if (indent == 0 && (content.StartsWith(DocumentStart, StringComparison.Ordinal) || content.StartsWith(DocumentEnd, StringComparison.Ordinal)))
        {
            sections.Clear();
            return index;
        }

        sections.RemoveAll(section => section.Indent >= indent);
        if (content.StartsWith(ListMarker, StringComparison.Ordinal))
        {
            content = content[ListMarkerWidth..];
            indent += ListMarkerWidth;
            if (EnvironmentItem(content, indent, index, sections) is { } item)
            {
                entries.Add(item);
                return index;
            }
        }

        return ReadMapping(lines, index, content, indent, sections, entries);
    }

    // `key: value`, `key:` opening a section, or `key: |` opening a block scalar.
    private static int ReadMapping(
        string[] lines,
        int index,
        string content,
        int indent,
        List<(int Indent, string Key)> sections,
        List<ConfigEntry> entries)
    {
        var colon = KeyEnd(content);
        if (colon <= 0)
        {
            return index;
        }

        var key = Unquote(content[..colon].Trim());
        var rest = content[(colon + 1)..];
        var valueStart = colon + 1 + (rest.Length - rest.TrimStart().Length);
        var raw = rest.Trim();
        if (raw.Length == 0 || raw[0] == '#')
        {
            sections.Add((indent, key));
            return index;
        }

        var path = PathOf(sections, key);
        if (raw[0] is '|' or '>')
        {
            return BlockScalar(lines, index, indent, path, raw[0] == '|', entries);
        }

        if (raw[0] is not ('&' or '*' or '{' or '[' or '!'))
        {
            var (value, offset) = Scalar(raw);
            entries.Add(ConfigEntry.At(path, value, index + 1, indent + valueStart + offset + 1));
        }

        return index;
    }

    // `- KEY=VALUE`, as in a docker-compose `environment:` list.
    private static ConfigEntry? EnvironmentItem(string item, int indent, int index, List<(int Indent, string Key)> sections)
    {
        if (IsQuoted(item))
        {
            item = item[1..^1];
            indent++;
        }

        var equals = item.IndexOf('=', StringComparison.Ordinal);
        if (equals <= 0 || KeyEnd(item) >= 0)
        {
            return null;
        }

        var (value, offset) = Scalar(item[(equals + 1)..]);
        return ConfigEntry.At(PathOf(sections, item[..equals].Trim()), value, index + 1, indent + equals + 1 + offset + 1);
    }

    private static int BlockScalar(string[] lines, int index, int indent, string path, bool literal, List<ConfigEntry> entries)
    {
        var last = index;
        while (last + 1 < lines.Length && (lines[last + 1].Trim().Length == 0 || Indent(lines[last + 1]) > indent))
        {
            last++;
        }

        var body = lines[(index + 1)..(last + 1)];
        var first = Array.FindIndex(body, line => line.Trim().Length > 0);
        if (first < 0)
        {
            return last;
        }

        var contentIndent = Indent(body[first]);
        var parts = body[first..].Select(line => line.Length > contentIndent ? line[contentIndent..] : string.Empty);
        var value = string.Join(literal ? "\n" : " ", parts).TrimEnd('\n', ' ');
        var firstLine = index + 1 + first;
        entries.Add(ConfigEntry.At(path, value, firstLine + 1, contentIndent + 1));
        return last;
    }

    // The index of the `:` that ends a key (followed by a space or the end of the line), outside quotes.
    private static int KeyEnd(string content)
    {
        var start = 0;
        if (content.Length > 0 && content[0] is '"' or '\'')
        {
            start = content.IndexOf(content[0], 1);
            if (start < 0)
            {
                return -1;
            }
        }

        for (var i = start; i < content.Length; i++)
        {
            if (content[i] == ':' && (i + 1 == content.Length || content[i + 1] == ' '))
            {
                return i;
            }
        }

        return -1;
    }

    // The value of a scalar and where it starts within the raw text (after an opening quote).
    private static (string Value, int Offset) Scalar(string raw)
    {
        if (raw.Length > 1 && raw[0] is '"' or '\'')
        {
            var close = raw.IndexOf(raw[0], 1);
            if (close > 0)
            {
                return (raw[1..close], 1);
            }
        }

        var comment = raw.IndexOf(TrailingComment, StringComparison.Ordinal);
        return ((comment < 0 ? raw : raw[..comment]).TrimEnd(), 0);
    }

    private static string Unquote(string key) => IsQuoted(key) ? key[1..^1] : key;

    private static bool IsQuoted(string text) => text.Length > 1 && text[0] is '"' or '\'' && text[^1] == text[0];

    private static string PathOf(List<(int Indent, string Key)> sections, string key) =>
        string.Join(Separator, [.. sections.Select(section => section.Key), key]);

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;
}
