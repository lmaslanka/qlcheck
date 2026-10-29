namespace Qlcheck.Scan;

// The .qlcheck_ignore files that apply inside one folder: the built-in defaults, then every file from
// the git root down to that folder (ADR 0010). Each file's patterns are relative to its own folder,
// and a deeper file overrides a shallower one, as with .gitignore.
internal sealed class IgnoreTree
{
    private readonly IReadOnlyList<Layer> _layers;

    private IgnoreTree(IReadOnlyList<Layer> layers)
    {
        _layers = layers;
    }

    public static IgnoreTree ForRoot(string scanRoot)
    {
        var root = RepoRoot.Full(scanRoot);
        var files = RepoRoot.Chain(RepoRoot.Boundary(root), root).Select(Read).OfType<Layer>();
        return new IgnoreTree([new Layer(root, IgnorePatterns.Defaults), .. files]);
    }

    // The tree for a subfolder: its own .qlcheck_ignore, if any, applies below it.
    public IgnoreTree Enter(string directory) =>
        Read(RepoRoot.Full(directory)) is { } layer ? new IgnoreTree([.. _layers, layer]) : this;

    public bool IsIgnored(string fullPath, bool isDirectory)
    {
        var ignored = false;
        foreach (var layer in _layers)
        {
            if (layer.Patterns.Match(Path.GetRelativePath(layer.Directory, fullPath), isDirectory) is { } match)
            {
                ignored = match;
            }
        }

        return ignored;
    }

    private static Layer? Read(string directory)
    {
        var file = Path.Combine(directory, IgnorePatterns.FileName);
        return File.Exists(file) ? new Layer(directory, IgnorePatterns.Parse(File.ReadAllLines(file))) : null;
    }

    private sealed record Layer(string Directory, IgnorePatterns Patterns);
}
