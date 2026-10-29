using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp.Checks.Coverage;

internal static class TestProjectFinder
{
    private const string CsprojPattern = "*.csproj";

    public static IReadOnlyList<string> Find(string productCsproj)
    {
        var product = Path.GetFullPath(productCsproj);
        var root = RepoRoot.Boundary(Path.GetDirectoryName(product)!);
        var matches = new List<string>();
        var unresolved = false;
        foreach (var csproj in Enumerate(root))
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

    private static IEnumerable<string> Enumerate(string root)
    {
        var pending = new Stack<(string Dir, IgnoreTree Ignore)>();
        pending.Push((root, IgnoreTree.ForRoot(root)));
        while (pending.Count > 0)
        {
            var (dir, ignore) = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(dir, CsprojPattern))
            {
                if (!ignore.IsIgnored(file, isDirectory: false))
                {
                    yield return file;
                }
            }

            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                if (!ignore.IsIgnored(sub, isDirectory: true))
                {
                    pending.Push((sub, ignore.Enter(sub)));
                }
            }
        }
    }
}
