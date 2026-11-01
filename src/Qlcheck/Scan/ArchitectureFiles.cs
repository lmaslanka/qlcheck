namespace Qlcheck.Scan;

// The intended architecture of the scanned repositories (plan 06, D1): one .qlcheck_architecture at
// each git root, or at the scan root outside git. Unlike the ignore and disable files it does not
// combine per folder, because Constraints name each other's components and a tangle needs the whole
// graph, so a file below the root is an error. Lines are `S… must-not-use T…`, `T… only-used-by S…`
// or `no-cycles P.*`, with `#` comments.
internal sealed class ArchitectureFiles
{
    private const string FileNameValue = ".qlcheck_architecture";

    private const char CommentStart = '#';

    private const string MustNotUse = "must-not-use";

    private const string OnlyUsedBy = "only-used-by";

    private const string NoCycles = "no-cycles";

    private const string ChildrenSuffix = ".*";

    private ArchitectureFiles(IReadOnlyList<ArchitectureFile> files)
    {
        Files = files;
    }

    public static string FileName => FileNameValue;

    public static ArchitectureFiles None { get; } = new([]);

    public IReadOnlyList<ArchitectureFile> Files { get; }

    // Throws InvalidOperationException naming the file and line for a malformed or misplaced file.
    public static ArchitectureFiles Load(IReadOnlyList<string> scanPaths, IEnumerable<string> fullPaths)
    {
        var roots = scanPaths.Select(RepoRoot.ScanDirectory).Distinct(StringComparer.Ordinal).ToList();
        var files = new Dictionary<string, ArchitectureFile?>(StringComparer.Ordinal);
        foreach (var directory in fullPaths.Select(path => Path.GetDirectoryName(RepoRoot.Full(path))!).Distinct(StringComparer.Ordinal))
        {
            var boundary = BoundaryFor(roots, directory);
            RejectDeeperFiles(boundary, directory);
            if (!files.ContainsKey(boundary))
            {
                files[boundary] = Read(boundary);
            }
        }

        return new ArchitectureFiles([.. files.Values.OfType<ArchitectureFile>().OrderBy(file => file.Path, StringComparer.Ordinal)]);
    }

    // The file governing a source, or null when its repository has none.
    public ArchitectureFile? For(string fullPath)
    {
        var full = RepoRoot.Full(fullPath);
        return Files.Where(file => full.StartsWith(Path.TrimEndingDirectorySeparator(file.Boundary) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .MaxBy(file => file.Boundary.Length);
    }

    private static string BoundaryFor(IReadOnlyList<string> roots, string directory)
    {
        var root = roots.Where(root => directory == root || directory.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .MaxBy(root => root.Length);
        return RepoRoot.Boundary(root ?? directory);
    }

    private static void RejectDeeperFiles(string boundary, string directory)
    {
        var deeper = RepoRoot.Chain(boundary, directory).Skip(1).Select(folder => Path.Combine(folder, FileNameValue)).FirstOrDefault(File.Exists);
        if (deeper is not null)
        {
            throw new InvalidOperationException($"A {FileNameValue} must be at the repository root ({deeper})");
        }
    }

    private static ArchitectureFile? Read(string boundary)
    {
        var path = Path.Combine(boundary, FileNameValue);
        if (!File.Exists(path))
        {
            return null;
        }

        var lines = File.ReadAllLines(path);
        var constraints = new List<ArchitectureConstraint>();
        for (var i = 0; i < lines.Length; i++)
        {
            var comment = lines[i].IndexOf(CommentStart, StringComparison.Ordinal);
            var words = (comment < 0 ? lines[i] : lines[i][..comment]).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0)
            {
                constraints.Add(Parse(words, i + 1) ?? throw Error(Problem(words), path, i + 1));
            }
        }

        return new ArchitectureFile(path, boundary, constraints);
    }

    private static ArchitectureConstraint? Parse(string[] words, int line)
    {
        if (words[0] == NoCycles)
        {
            return words is [_, var children] && children.EndsWith(ChildrenSuffix, StringComparison.Ordinal) && DottedNames.IsValid(children[..^ChildrenSuffix.Length])
                ? new ArchitectureConstraint(ConstraintKind.NoCycles, [children[..^ChildrenSuffix.Length]], [], line)
                : null;
        }

        var keyword = Array.FindIndex(words, word => word is MustNotUse or OnlyUsedBy);
        if (keyword <= 0 || keyword == words.Length - 1 || words.Any(word => word is not (MustNotUse or OnlyUsedBy) && !DottedNames.IsValid(word)))
        {
            return null;
        }

        var (left, right) = (words[..keyword], words[(keyword + 1)..]);
        return words[keyword] == MustNotUse
            ? new ArchitectureConstraint(ConstraintKind.MustNotUse, left, right, line)
            : new ArchitectureConstraint(ConstraintKind.OnlyUsedBy, right, left, line);
    }

    // Why a line did not parse.
    private static string Problem(string[] words)
    {
        if (words[0] == NoCycles)
        {
            return $"{NoCycles} needs one name ending in '{ChildrenSuffix}'";
        }

        var keyword = Array.FindIndex(words, word => word is MustNotUse or OnlyUsedBy);
        if (keyword < 0)
        {
            return $"Unknown constraint: expected '{MustNotUse}', '{OnlyUsedBy}' or '{NoCycles}'";
        }

        var invalid = words.FirstOrDefault(word => word is not (MustNotUse or OnlyUsedBy) && !DottedNames.IsValid(word));
        return invalid is not null ? $"Not a dotted name: '{invalid}'" : $"'{words[keyword]}' needs names on both sides";
    }

    private static InvalidOperationException Error(string problem, string path, int line) => new($"{problem} ({path}:{line})");
}
