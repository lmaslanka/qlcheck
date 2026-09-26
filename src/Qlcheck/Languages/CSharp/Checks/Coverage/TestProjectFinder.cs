using IOFile = System.IO.File;
using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp.Checks.Coverage;

internal static class TestProjectFinder
{
    private const string GitDirectory = ".git";

    private const string CsprojPattern = "*.csproj";

    public static IReadOnlyList<string> Find(string productCsproj)
    {
        var product = Path.GetFullPath(productCsproj);
        var root = GitRoot(product) ?? Path.GetDirectoryName(product)!;
        var ignore = IgnorePatterns.Load(root);
        var matches = new List<string>();
        var unresolved = false;
        foreach (var csproj in Enumerate(root, ignore))
        {
            if (string.Equals(Path.GetFullPath(csproj), product, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var info = CsprojReader.Read(csproj);
            if (!info.IsTestProject)
            {
                continue;
            }

            if (info.UnresolvedReference)
            {
                unresolved = true;
                continue;
            }

            if (info.ProjectReferences.Any(reference =>
                    string.Equals(reference, product, StringComparison.OrdinalIgnoreCase)))
            {
                matches.Add(Path.GetFullPath(csproj));
            }
        }

        if (matches.Count > 0)
        {
            return matches;
        }

        if (unresolved)
        {
            throw new InvalidOperationException($"Cannot resolve ProjectReference for {product}.");
        }

        throw new InvalidOperationException($"No test project references {product}.");
    }

    private static string? GitRoot(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        while (!string.IsNullOrEmpty(dir))
        {
            var git = Path.Combine(dir, GitDirectory);
            if (Directory.Exists(git) || IOFile.Exists(git))
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

    private static IEnumerable<string> Enumerate(string root, IgnorePatterns ignore)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(dir, CsprojPattern))
            {
                var relative = Path.GetRelativePath(root, file);
                if (!ignore.IsIgnored(relative, isDirectory: false))
                {
                    yield return file;
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
}
