namespace Qlcheck.Tests;

public class IgnoreTreeTests : IDisposable
{
    private const string IgnoreFile = ".qlcheck_ignore";

    private const string Source = "class C { }";

    private readonly string _repo = Directory.CreateTempSubdirectory("qlcheck_ignoretree_").FullName;

    public void Dispose()
    {
        Directory.Delete(_repo, recursive: true);
    }

    [Fact]
    public void Root_file_applies_when_scanning_a_subfolder_of_a_git_repository()
    {
        MakeGitRepository();
        Write(IgnoreFile, "/src/gen/\n");
        Write("src/gen/Parser.cs", Source);
        Write("src/App.cs", Source);

        Assert.Equal(["App.cs"], Scan("src"));
    }

    [Fact]
    public void Patterns_are_relative_to_the_folder_of_their_file()
    {
        MakeGitRepository();
        Write("src/" + IgnoreFile, "/gen/\n");
        Write("src/gen/Parser.cs", Source);
        Write("gen/Tool.cs", Source);

        Assert.Equal(["Tool.cs"], Scan("."));
    }

    [Fact]
    public void A_deeper_file_re_includes_a_path_ignored_above()
    {
        MakeGitRepository();
        Write(IgnoreFile, "*.g.cs\n");
        Write("src/" + IgnoreFile, "!Keep.g.cs\n");
        Write("src/Keep.g.cs", Source);
        Write("src/Drop.g.cs", Source);
        Write("Other.g.cs", Source);

        Assert.Equal(["Keep.g.cs"], Scan("."));
    }

    [Fact]
    public void Files_above_the_scan_root_are_not_used_outside_a_git_repository()
    {
        Write(IgnoreFile, "/src/gen/\n");
        Write("src/gen/Parser.cs", Source);

        Assert.Equal(["Parser.cs"], Scan("src"));
    }

    private void MakeGitRepository() => Directory.CreateDirectory(Path.Combine(_repo, ".git"));

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_repo, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private IReadOnlyList<string> Scan(string relative) =>
        SourceScan.Load([Path.Combine(_repo, relative)], path => path.EndsWith(".cs", StringComparison.Ordinal))
            .Select(source => Path.GetFileName(source.FullPath))
            .Order(StringComparer.Ordinal)
            .ToList();
}
