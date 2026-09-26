namespace Qlcheck.Tests;

public class CsprojReaderTests : IDisposable
{
    private const string TempPrefix = "qlcheck_csproj_";

    private const string Assembly = "Sample";

    private const string Framework = "net10.0";

    private const string Include = "[Sample]*";

    private readonly string _root = Directory.CreateTempSubdirectory(TempPrefix).FullName;

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Reads_assembly_name_and_include_filter()
    {
        var path = Write(
            "Sample.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Sample</AssemblyName>
              </PropertyGroup>
            </Project>
            """);

        var info = CsprojReader.Read(path);

        Assert.Equal(Assembly, info.AssemblyName);
        Assert.Equal(Framework, info.TargetFramework);
        Assert.False(info.MultipleFrameworks);
        Assert.False(info.IsTestProject);
        Assert.Equal(Include, CsprojReader.IncludeFilter(path));
    }

    [Fact]
    public void Rejects_multiple_target_frameworks()
    {
        var path = Write(
            "Sample.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """);

        var error = Assert.Throws<InvalidOperationException>(() => CsprojReader.RequireSingleFramework(path));

        Assert.Contains("multiple target frameworks", error.Message);
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, text);
        return path;
    }
}
