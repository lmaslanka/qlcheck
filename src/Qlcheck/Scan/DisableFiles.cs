namespace Qlcheck.Scan;

// Disabled checks per folder, from every .qlcheck_disable between the git root and that folder
// (ADR 0010). A file lists one check id per line; `!id` turns a check back on below that folder.
internal sealed class DisableFiles
{
    private const string FileNameValue = ".qlcheck_disable";

    private const char CommentStart = '#';

    private const char EnablePrefix = '!';

    private const string TopUnder = ".";

    private const string UnderSeparator = "/";

    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _byDirectory;

    private DisableFiles(IReadOnlyList<Entry> entries, IReadOnlyDictionary<string, IReadOnlySet<string>> byDirectory)
    {
        Entries = entries;
        _byDirectory = byDirectory;
    }

    public static string FileName => FileNameValue;

    public static DisableFiles None { get; } = new([], new Dictionary<string, IReadOnlySet<string>>());

    // Every line read from the files that apply to the loaded sources, `!id` lines included.
    public IReadOnlyList<Entry> Entries { get; }

    public IEnumerable<Entry> Disabled => Entries.Where(entry => !entry.Enable);

    public static DisableFiles Load(IReadOnlyList<string> scanPaths, IEnumerable<string> fullPaths)
    {
        var roots = scanPaths
            .Select(RepoRoot.ScanDirectory)
            .Distinct(StringComparer.Ordinal)
            .Select(dir => new Root(dir, RepoRoot.Boundary(dir)))
            .ToList();
        var files = new Dictionary<string, IReadOnlyList<Entry>>(StringComparer.Ordinal);
        var byDirectory = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (var fullPath in fullPaths)
        {
            var dir = DirectoryOf(fullPath);
            if (byDirectory.ContainsKey(dir))
            {
                continue;
            }

            var boundary = BoundaryFor(roots, dir);
            var disabled = new HashSet<string>(StringComparer.Ordinal);
            foreach (var folder in RepoRoot.Chain(boundary, dir))
            {
                if (!files.TryGetValue(folder, out var entries))
                {
                    entries = Read(boundary, folder);
                    files[folder] = entries;
                }

                Apply(disabled, entries);
            }

            byDirectory[dir] = disabled;
        }

        var all = files.Values
            .SelectMany(entries => entries)
            .OrderBy(entry => entry.Under, StringComparer.Ordinal)
            .ThenBy(entry => entry.Line)
            .ToList();
        return new DisableFiles(all, byDirectory);
    }

    public bool IsDisabled(string check, string fullPath) =>
        _byDirectory.TryGetValue(DirectoryOf(fullPath), out var disabled) && disabled.Contains(check);

    // Ids asked for by name override every file, so they are dropped here.
    public DisableFiles Without(IReadOnlyCollection<string> checks)
    {
        if (checks.Count == 0)
        {
            return this;
        }

        var entries = Entries.Where(entry => !checks.Contains(entry.Check)).ToList();
        var byDirectory = _byDirectory.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlySet<string>)pair.Value.Except(checks).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
        return new DisableFiles(entries, byDirectory);
    }

    private static string DirectoryOf(string fullPath)
    {
        var full = RepoRoot.Full(fullPath);
        return Path.GetDirectoryName(full) ?? full;
    }

    private static void Apply(HashSet<string> disabled, IReadOnlyList<Entry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Enable)
            {
                disabled.Remove(entry.Check);
            }
            else
            {
                disabled.Add(entry.Check);
            }
        }
    }

    private static string BoundaryFor(IReadOnlyList<Root> roots, string dir)
    {
        var root = roots
            .Where(root => IsWithin(dir, root.Directory))
            .MaxBy(root => root.Directory.Length);
        return root?.Boundary ?? dir;
    }

    private static bool IsWithin(string dir, string parent) =>
        string.Equals(dir, parent, StringComparison.Ordinal)
        || dir.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static IReadOnlyList<Entry> Read(string boundary, string folder)
    {
        var file = Path.Combine(folder, FileName);
        if (!File.Exists(file))
        {
            return [];
        }

        var under = Under(boundary, folder);
        var entries = new List<Entry>();
        var lines = File.ReadAllLines(file);
        for (var i = 0; i < lines.Length; i++)
        {
            if (Parse(lines[i]) is not { } parsed)
            {
                continue;
            }

            entries.Add(new Entry(parsed.Check, parsed.Enable, under, file, i + 1));
        }

        return entries;
    }

    private static (string Check, bool Enable)? Parse(string line)
    {
        var text = line.Split(CommentStart, 2)[0].Trim();
        var enable = text.StartsWith(EnablePrefix);
        var check = enable ? text[1..].Trim() : text;
        return check.Length == 0 ? null : (check, enable);
    }

    private static string Under(string boundary, string folder)
    {
        var relative = Path.GetRelativePath(boundary, folder).Replace('\\', '/');
        return relative == TopUnder ? TopUnder : $"{relative}{UnderSeparator}";
    }

    internal sealed record Entry(string Check, bool Enable, string Under, string File, int Line);

    private sealed record Root(string Directory, string Boundary);
}
