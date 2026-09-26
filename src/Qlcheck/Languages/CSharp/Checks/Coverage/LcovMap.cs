using System.Globalization;

namespace Qlcheck.Languages.CSharp.Checks.Coverage;

internal static class LcovMap
{
    private const string SourcePrefix = "SF:";

    private const string FunctionPrefix = "FN:";

    private const string LinePrefix = "DA:";

    private const string RecordEnd = "end_of_record";

    private const char Comma = ',';

    public static Document Parse(string text)
    {
        var document = new Document();
        RecordBuilder? current = null;
        foreach (var raw in text.ReplaceLineEndings("\n").Split('\n'))
        {
            if (raw.Length == 0)
            {
                continue;
            }

            if (raw.StartsWith(SourcePrefix, StringComparison.Ordinal))
            {
                Commit(document, current);
                current = new RecordBuilder(raw[SourcePrefix.Length..]);
                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (raw == RecordEnd)
            {
                Commit(document, current);
                current = null;
                continue;
            }

            if (raw.StartsWith(FunctionPrefix, StringComparison.Ordinal))
            {
                current.AddFunction(raw[FunctionPrefix.Length..]);
                continue;
            }

            if (raw.StartsWith(LinePrefix, StringComparison.Ordinal))
            {
                current.AddLine(raw[LinePrefix.Length..]);
            }
        }

        Commit(document, current);
        return document;
    }

    private static void Commit(Document document, RecordBuilder? current)
    {
        if (current is null || current.Path.Length == 0)
        {
            return;
        }

        document.Add(current.Path, current.ToHits());
    }

    internal sealed class Document
    {
        private readonly List<FileHits> _files = [];

        public bool TryGet(string fullPath, out FileHits hits)
        {
            foreach (var file in _files)
            {
                if (Same(file.Path, fullPath))
                {
                    hits = file;
                    return true;
                }
            }

            hits = null!;
            return false;
        }

        public void Add(string path, FileHits hits)
        {
            if (TryGet(path, out var existing))
            {
                existing.Merge(hits);
                return;
            }

            _files.Add(hits);
        }

        public void Merge(Document other)
        {
            foreach (var file in other._files)
            {
                Add(file.Path, file);
            }
        }

        private static bool Same(string left, string right)
        {
            var lcov = Slash(left);
            var full = Slash(Path.GetFullPath(right));
            if (lcov.Equals(full, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (Path.IsPathRooted(left))
            {
                var rooted = Slash(Path.GetFullPath(left));
                if (rooted.Equals(full, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var relative = lcov.TrimStart('/');
            return full.EndsWith($"/{relative}", StringComparison.OrdinalIgnoreCase);
        }

        private static string Slash(string path) => path.Replace('\\', '/');
    }

    internal sealed class FileHits
    {
        public FileHits(string path, IReadOnlyList<LineHit> lines)
        {
            Path = path;
            Lines.AddRange(lines);
        }

        public string Path { get; }

        public List<LineHit> Lines { get; } = [];

        public void Merge(FileHits other)
        {
            var byLine = new Dictionary<int, LineHit>();
            foreach (var line in Lines)
            {
                byLine[line.Line] = line;
            }

            foreach (var line in other.Lines)
            {
                if (!byLine.TryGetValue(line.Line, out var current))
                {
                    byLine[line.Line] = line;
                    continue;
                }

                var hits = current.Hits > line.Hits ? current.Hits : line.Hits;
                byLine[line.Line] = new LineHit(line.Line, hits, current.Method ?? line.Method);
            }

            Lines.Clear();
            Lines.AddRange(byLine.Values);
        }
    }

    internal sealed record LineHit(int Line, int Hits, string? Method);

    private sealed class RecordBuilder
    {
        private readonly List<FunctionHit> _functions = [];

        private readonly List<PendingLine> _lines = [];

        public RecordBuilder(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public void AddFunction(string value)
        {
            var comma = value.IndexOf(Comma);
            if (comma < 1 || !TryLine(value[..comma], out var line))
            {
                return;
            }

            _functions.Add(new FunctionHit(line, value[(comma + 1)..]));
        }

        public void AddLine(string value)
        {
            var comma = value.IndexOf(Comma);
            if (comma < 1 || !TryLine(value[..comma], out var line))
            {
                return;
            }

            var rest = value[(comma + 1)..];
            var next = rest.IndexOf(Comma);
            var hitsText = next < 0 ? rest : rest[..next];
            if (!int.TryParse(hitsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hits))
            {
                return;
            }

            _lines.Add(new PendingLine(line, hits));
        }

        public FileHits ToHits()
        {
            var lines = new List<LineHit>(_lines.Count);
            foreach (var line in _lines)
            {
                lines.Add(new LineHit(line.Line, line.Hits, MethodAt(line.Line)));
            }

            return new FileHits(Path, lines);
        }

        private string? MethodAt(int line)
        {
            string? name = null;
            var best = 0;
            foreach (var function in _functions)
            {
                if (function.Line > line || function.Line < best)
                {
                    continue;
                }

                best = function.Line;
                name = function.Name;
            }

            return name;
        }

        private static bool TryLine(string text, out int line)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out line))
            {
                return false;
            }

            return line > 0;
        }

        private sealed record FunctionHit(int Line, string Name);

        private sealed record PendingLine(int Line, int Hits);
    }
}
