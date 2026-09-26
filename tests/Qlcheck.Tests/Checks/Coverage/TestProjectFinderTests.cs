namespace Qlcheck.Tests;

public class TestProjectFinderTests : IDisposable
{
    private const string TempPrefix = "qlcheck_cov_";

    private const string GitDirectory = ".git";

    private const string ProductRelative = "src/Sample/Sample.csproj";

    private const string TestsRelative = "tests/Sample.Tests/Sample.Tests.csproj";

    private const string OtherRelative = "tests/Other.Tests/Other.Tests.csproj";

    private readonly string _root = Directory.CreateTempSubdirectory(TempPrefix).FullName;

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Finds_the_test_project_that_references_the_product()
    {
        Directory.CreateDirectory(Path.Combine(_root, GitDirectory));
        var product = Write(
            ProductRelative,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Sample</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        var tests = Write(
            TestsRelative,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.9.3" />
                <ProjectReference Include="..\..\src\Sample\Sample.csproj" />
              </ItemGroup>
            </Project>
            """);
        Write(
            OtherRelative,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="nunit" Version="4.0.0" />
                <ProjectReference Include="..\..\src\Missing\Missing.csproj" />
              </ItemGroup>
            </Project>
            """);

        var found = TestProjectFinder.Find(product);

        var match = Assert.Single(found);
        Assert.Equal(Path.GetFullPath(tests), match);
    }

    [Fact]
    public void Rejects_an_unresolved_project_reference_when_nothing_matches()
    {
        Directory.CreateDirectory(Path.Combine(_root, GitDirectory));
        var product = Write(
            ProductRelative,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        Write(
            TestsRelative,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
                <ProjectReference Include="$(RepoRoot)src/Sample/Sample.csproj" />
              </ItemGroup>
            </Project>
            """);

        var error = Assert.Throws<InvalidOperationException>(() => TestProjectFinder.Find(product));

        Assert.Contains("Cannot resolve ProjectReference", error.Message);
    }

    private string Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }
}
