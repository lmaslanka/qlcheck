namespace Qlcheck.Tests;

public class SourceScanTests
{
    [Fact]
    public void Load_throws_when_path_does_not_exist()
    {
        Assert.Throws<DirectoryNotFoundException>(() => SourceScan.Load(["qlcheck-does-not-exist-xyz"], _ => true));
    }

    [Fact]
    public void Load_recurses_into_non_ignored_subdirectories()
    {
        var dir = Directory.CreateTempSubdirectory("qlcheck_scan_");
        try
        {
            var sub = Directory.CreateDirectory(Path.Combine(dir.FullName, "sub"));
            File.WriteAllText(Path.Combine(sub.FullName, "Repo.cs"), "class C { }");

            var loaded = SourceScan.Load([dir.FullName], path => path.EndsWith(".cs", StringComparison.Ordinal));

            Assert.Single(loaded);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Load_reports_relative_display_path_for_files_under_the_current_directory()
    {
        var name = $"qlcheck_scan_{Guid.NewGuid():N}.cs";
        var path = Path.Combine(Directory.GetCurrentDirectory(), name);
        File.WriteAllText(path, "class C { }");
        try
        {
            var loaded = SourceScan.Load([path], _ => true);

            var source = Assert.Single(loaded);
            Assert.Equal(name, source.File.Path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
