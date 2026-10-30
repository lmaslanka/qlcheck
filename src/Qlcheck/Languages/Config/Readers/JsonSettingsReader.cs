// Copyright (c) qlcheck contributors.
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Qlcheck.Languages.Config.Readers;

// `appsettings*.json`, read the way .NET configuration reads it: comments and trailing commas are
// allowed, sections join with `:`, and array elements are numbered sections. Only string values
// are entries. A file that does not parse yields no entries.
internal static class JsonSettingsReader
{
    private const string Separator = ":";

    private static readonly JsonReaderOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static IReadOnlyList<ConfigEntry> Read(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text.TrimStart('\uFEFF'));
        var walk = new Walk(bytes, LineStarts(bytes));
        try
        {
            var reader = new Utf8JsonReader(bytes, Options);
            while (reader.Read())
            {
                var token = reader.TokenType;
                var value = token is JsonTokenType.PropertyName or JsonTokenType.String ? reader.GetString() : null;
                walk.Step(token, value, (int)reader.TokenStartIndex);
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return walk.Entries;
    }

    private static List<int> LineStarts(byte[] bytes)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts;
    }

    // The section path while walking the tokens, and the entries found so far.
    private sealed class Walk(byte[] bytes, List<int> lines)
    {
        private readonly List<string> _path = [];

        private readonly Stack<Frame> _frames = new();

        private readonly List<ConfigEntry> _entries = [];

        public IReadOnlyList<ConfigEntry> Entries => _entries;

        public void Step(JsonTokenType token, string? value, int offset)
        {
            switch (token)
            {
                case JsonTokenType.StartObject or JsonTokenType.StartArray:
                    Open(token == JsonTokenType.StartArray);
                    break;
                case JsonTokenType.EndObject or JsonTokenType.EndArray:
                    Close();
                    break;
                case JsonTokenType.PropertyName:
                    _frames.Peek().Property = value;
                    break;
                case JsonTokenType.String:
                    Add(value ?? string.Empty, offset);
                    break;
                default:
                    Segment();
                    break;
            }
        }

        private void Open(bool isArray)
        {
            var segment = Segment();
            if (segment is not null)
            {
                _path.Add(segment);
            }

            _frames.Push(new Frame(isArray, segment is not null));
        }

        private void Close()
        {
            if (_frames.Pop().AddedSegment)
            {
                _path.RemoveAt(_path.Count - 1);
            }
        }

        // The string token starts at its opening quote; the value starts one byte later.
        private void Add(string value, int offset)
        {
            var key = string.Join(Separator, [.. _path, Segment() ?? string.Empty]);
            var (line, column) = Position(offset + 1);
            _entries.Add(ConfigEntry.At(key, value, line, column));
        }

        // 1-based line and character column of a byte offset.
        private (int Line, int Column) Position(int offset)
        {
            var index = lines.BinarySearch(offset);
            var line = index >= 0 ? index : ~index - 1;
            var column = Encoding.UTF8.GetCharCount(bytes, lines[line], offset - lines[line]) + 1;
            return (line + 1, column);
        }

        // The section name of the next value: the pending property name, or the next array index.
        private string? Segment()
        {
            if (_frames.Count == 0)
            {
                return null;
            }

            var frame = _frames.Peek();
            return frame.IsArray ? frame.NextIndex().ToString(CultureInfo.InvariantCulture) : frame.Property;
        }
    }

    private sealed class Frame(bool isArray, bool addedSegment)
    {
        private int _index;

        public bool IsArray { get; } = isArray;

        public bool AddedSegment { get; } = addedSegment;

        public string? Property { get; set; }

        public int NextIndex()
        {
            var index = _index;
            _index = index + 1;
            return index;
        }
    }
}
