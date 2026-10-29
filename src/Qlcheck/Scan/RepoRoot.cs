namespace Qlcheck.Scan;

// Where repository files (.qlcheck_ignore, .qlcheck_disable) are looked for: from the git root down
// to a scanned folder, or only the scanned folder itself outside a git repository (ADR 0010).
internal static class RepoRoot
{
    private const string GitEntry = ".git";

    public static string? GitRoot(string directory)
    {
        var dir = Full(directory);
        while (!string.IsNullOrEmpty(dir))
        {
            var git = Path.Combine(dir, GitEntry);
            if (Directory.Exists(git) || File.Exists(git))
            {
                return dir;
            }

            var parent = Path.GetDirectoryName(dir);
            if (parent == dir)
            {
                break;
            }

            dir = parent;
        }

        return null;
    }

    public static string Boundary(string scanDirectory) =>
        GitRoot(scanDirectory) ?? Full(scanDirectory);

    // The folders from boundary down to directory, outermost first. Just directory when it is not
    // under boundary.
    public static IReadOnlyList<string> Chain(string boundary, string directory)
    {
        var chain = new List<string>();
        var dir = Full(directory);
        var top = Full(boundary);
        while (!string.IsNullOrEmpty(dir))
        {
            chain.Add(dir);
            if (string.Equals(dir, top, StringComparison.Ordinal))
            {
                chain.Reverse();
                return chain;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return [Full(directory)];
    }

    // The folder a scan path stands for: itself for a directory, its parent for a file.
    public static string ScanDirectory(string path)
    {
        var full = Full(path);
        return Directory.Exists(full) ? full : Path.GetDirectoryName(full) ?? full;
    }

    public static string Full(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
