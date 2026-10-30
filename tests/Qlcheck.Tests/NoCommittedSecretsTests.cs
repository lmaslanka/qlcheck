using Qlcheck.Secrets;

namespace Qlcheck.Tests;

// Push protection and secret scanners read every committed file, tests and docs included. Test
// tokens are built at run time (FakeSecrets), and this guard fails if a whole token ever lands in
// a file under src/, tests/ or docs/.
public class NoCommittedSecretsTests
{
    private static readonly string[] Roots = ["src", "tests", "docs"];

    private static readonly string[] BuildDirs = ["bin", "obj"];

    [Fact]
    public void No_committed_file_holds_a_known_token()
    {
        var root = RepoRoot();
        var offenders = Roots
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(root, dir), "*", SearchOption.AllDirectories))
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Any(BuildDirs.Contains))
            .Where(path => SecretFormats.ProviderOf(File.ReadAllText(path)) is not null)
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        Assert.Empty(offenders);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Qlcheck.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repo root was not found.");
    }
}
