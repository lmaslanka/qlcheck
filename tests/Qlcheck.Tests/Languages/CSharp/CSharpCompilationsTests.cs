namespace Qlcheck.Tests;

public class CSharpCompilationsTests
{
    [Fact]
    public void FindCsproj_locates_project_file_in_an_ancestor_directory()
    {
        var dir = Directory.CreateTempSubdirectory("qlcheck_csproj_");
        try
        {
            var csproj = Path.Combine(dir.FullName, "Sample.csproj");
            File.WriteAllText(csproj, "<Project />");
            var nested = Directory.CreateDirectory(Path.Combine(dir.FullName, "nested"));
            var file = Path.Combine(nested.FullName, "Repo.cs");
            File.WriteAllText(file, "class C { }");

            Assert.Equal(csproj, CSharpCompilations.FindCsproj(file));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
