namespace Qlcheck.Scan;

internal static class SourceScan
{
    internal sealed record LoadedSource(string FullPath, SourceFile File);

    public static IReadOnlyList<LoadedSource> Load(IReadOnlyList<string> paths, Func<string, bool> matches)
    {
        var files = CollectFiles(paths, matches);
        return files
            .AsParallel()
            .AsOrdered()
            .Select(path => new LoadedSource(path, new SourceFile(DisplayPath(path), File.ReadAllText(path))))
            .ToList();
    }

    private static List<string> CollectFiles(IReadOnlyList<string> paths, Func<string, bool> matches)
    {
        var files = new List<string>();
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                var full = Path.GetFullPath(path);
                if (matches(full))
                {
                    files.Add(full);
                }

                continue;
            }

            if (Directory.Exists(path))
            {
                var root = Path.GetFullPath(path);
                CollectFromDirectory(root, IgnorePatterns.Load(root), matches, files);
                continue;
            }

            throw new DirectoryNotFoundException($"Path not found: {path}");
        }

        return files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.Ordinal).ToList();
    }

    private static void CollectFromDirectory(
        string root,
        IgnorePatterns ignore,
        Func<string, bool> matches,
        List<string> files)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var relative = Path.GetRelativePath(root, file);
                var full = Path.GetFullPath(file);
                if (!ignore.IsIgnored(relative, isDirectory: false) && matches(full))
                {
                    files.Add(full);
                }
            }

            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var relative = Path.GetRelativePath(root, sub);
                if (!ignore.IsIgnored(relative, isDirectory: true))
                {
                    pending.Push(sub);
                }
            }
        }
    }

    private static string DisplayPath(string fullPath)
    {
        var cwd = Path.GetFullPath(Directory.GetCurrentDirectory());
        fullPath = Path.GetFullPath(fullPath);
        var prefix = cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(cwd, fullPath).Replace('\\', '/');
        }

        return fullPath.Replace('\\', '/');
    }
}
