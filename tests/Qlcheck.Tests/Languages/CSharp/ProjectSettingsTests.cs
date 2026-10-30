using Microsoft.CodeAnalysis;

namespace Qlcheck.Tests;

public class ProjectSettingsTests
{
    private const string WebSdk = "Microsoft.NET.Sdk.Web";

    private const string TempPrefix = "qlcheck_settings_";

    [Theory]
    [InlineData("enable", NullableContextOptions.Enable)]
    [InlineData("Disable", NullableContextOptions.Disable)]
    [InlineData("WARNINGS", NullableContextOptions.Warnings)]
    [InlineData("annotations", NullableContextOptions.Annotations)]
    public void Load_maps_each_nullable_value(string value, NullableContextOptions expected)
    {
        var settings = Load(Project($"<PropertyGroup><Nullable>{value}</Nullable></PropertyGroup>"));

        Assert.Equal(expected, settings.Nullable);
    }

    [Fact]
    public void Load_defaults_nullable_to_disable_when_missing()
    {
        var settings = Load(Project(string.Empty));

        Assert.Equal(NullableContextOptions.Disable, settings.Nullable);
    }

    [Fact]
    public void Load_inherits_directory_build_props_and_lets_the_csproj_override_it()
    {
        const string props = """
            <Project>
              <PropertyGroup>
                <Nullable>enable</Nullable>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
            </Project>
            """;

        var settings = Load(Project("<PropertyGroup><Nullable>disable</Nullable></PropertyGroup>"), props);

        Assert.Equal(NullableContextOptions.Disable, settings.Nullable);
        Assert.Equal(OutputKind.ConsoleApplication, settings.OutputKind);
    }

    [Fact]
    public void Load_finds_directory_build_props_in_an_ancestor_directory()
    {
        var dir = Directory.CreateTempSubdirectory(TempPrefix);
        try
        {
            File.WriteAllText(
                Path.Combine(dir.FullName, "Directory.Build.props"),
                "<Project><PropertyGroup><Nullable>enable</Nullable></PropertyGroup></Project>");
            var nested = Directory.CreateDirectory(Path.Combine(dir.FullName, "src", "App"));
            var csproj = Path.Combine(nested.FullName, "App.csproj");
            File.WriteAllText(csproj, Project(string.Empty));

            Assert.Equal(NullableContextOptions.Enable, ProjectSettings.Load(csproj).Nullable);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Load_ignores_conditioned_properties_and_groups()
    {
        const string body = """
            <PropertyGroup>
              <Nullable>enable</Nullable>
              <Nullable Condition="'$(Configuration)' == 'Debug'">disable</Nullable>
            </PropertyGroup>
            <PropertyGroup Condition="'$(Configuration)' == 'Release'">
              <OutputType>Exe</OutputType>
            </PropertyGroup>
            """;

        var settings = Load(Project(body));

        Assert.Equal(NullableContextOptions.Enable, settings.Nullable);
        Assert.Equal(OutputKind.DynamicallyLinkedLibrary, settings.OutputKind);
    }

    [Theory]
    [InlineData("Exe", OutputKind.ConsoleApplication)]
    [InlineData("WinExe", OutputKind.WindowsApplication)]
    [InlineData("Library", OutputKind.DynamicallyLinkedLibrary)]
    public void Load_maps_output_type(string value, OutputKind expected)
    {
        var settings = Load(Project($"<PropertyGroup><OutputType>{value}</OutputType></PropertyGroup>"));

        Assert.Equal(expected, settings.OutputKind);
    }

    [Fact]
    public void Load_reads_a_single_target_framework()
    {
        var settings = Load(Project("<PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>"));

        Assert.Equal(["net10.0"], settings.TargetFrameworks);
    }

    [Fact]
    public void Load_splits_target_frameworks()
    {
        var settings = Load(Project(
            "<PropertyGroup><TargetFrameworks> net8.0;net10.0; </TargetFrameworks></PropertyGroup>"));

        Assert.Equal(["net8.0", "net10.0"], settings.TargetFrameworks);
    }

    [Fact]
    public void Load_reads_the_sdk_attribute_without_its_version()
    {
        var settings = Load($"<Project Sdk=\"{WebSdk}/1.0.0\"></Project>");

        Assert.Equal(WebSdk, settings.Sdk);
    }

    [Fact]
    public void Load_reads_the_sdk_element()
    {
        var settings = Load($"<Project><Sdk Name=\"{WebSdk}\" /></Project>");

        Assert.Equal(WebSdk, settings.Sdk);
    }

    [Fact]
    public void Load_reads_the_sdk_from_an_import()
    {
        var settings = Load($"<Project><Import Project=\"Sdk.props\" Sdk=\"{WebSdk}\" /></Project>");

        Assert.Equal(WebSdk, settings.Sdk);
    }

    [Theory]
    [InlineData("enable", true)]
    [InlineData("true", true)]
    [InlineData("disable", false)]
    public void Load_reads_implicit_usings(string value, bool expected)
    {
        var settings = Load(Project($"<PropertyGroup><ImplicitUsings>{value}</ImplicitUsings></PropertyGroup>"));

        Assert.Equal(expected, settings.ImplicitUsings);
    }

    [Fact]
    public void Load_reads_using_items_in_order()
    {
        const string body = """
            <ItemGroup>
              <Using Include="Xunit" />
              <Using Remove="System.Linq" />
              <Using Include="System.Text.StringBuilder" Alias="Sb" />
              <Using Include="System.Math" Static="true" />
              <Using Include="System.Console">
                <Static>true</Static>
              </Using>
            </ItemGroup>
            <ItemGroup Condition="'$(X)' == 'y'">
              <Using Include="Ignored" />
            </ItemGroup>
            """;

        var settings = Load(Project(body));

        Assert.Equal(
            [
                new ProjectUsing("Xunit", Remove: false, Alias: null, Static: false),
                new ProjectUsing("System.Linq", Remove: true, Alias: null, Static: false),
                new ProjectUsing("System.Text.StringBuilder", Remove: false, Alias: "Sb", Static: false),
                new ProjectUsing("System.Math", Remove: false, Alias: null, Static: true),
                new ProjectUsing("System.Console", Remove: false, Alias: null, Static: true),
            ],
            settings.Usings);
    }

    [Fact]
    public void Load_reads_using_items_from_directory_build_props_first()
    {
        const string props = "<Project><ItemGroup><Using Include=\"Shared\" /></ItemGroup></Project>";

        var settings = Load(Project("<ItemGroup><Using Include=\"Local\" /></ItemGroup>"), props);

        Assert.Equal(["Shared", "Local"], settings.Usings.Select(item => item.Name));
    }

    [Fact]
    public void Load_reads_framework_references()
    {
        var settings = Load(Project(
            "<ItemGroup><FrameworkReference Include=\"Microsoft.AspNetCore.App\" /></ItemGroup>"));

        Assert.Equal(["Microsoft.AspNetCore.App"], settings.FrameworkReferences);
    }

    [Fact]
    public void Load_reads_properties_from_an_old_style_namespaced_project()
    {
        const string csproj = """
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup><OutputType>Exe</OutputType></PropertyGroup>
            </Project>
            """;

        Assert.Equal(OutputKind.ConsoleApplication, Load(csproj).OutputKind);
    }

    private static string Project(string body) => $"<Project Sdk=\"Microsoft.NET.Sdk\">{body}</Project>";

    private static ProjectSettings Load(string csprojXml, string? directoryBuildProps = null)
    {
        var dir = Directory.CreateTempSubdirectory(TempPrefix);
        try
        {
            var csproj = Path.Combine(dir.FullName, "Sample.csproj");
            File.WriteAllText(csproj, csprojXml);
            if (directoryBuildProps is not null)
            {
                File.WriteAllText(Path.Combine(dir.FullName, "Directory.Build.props"), directoryBuildProps);
            }

            return ProjectSettings.Load(csproj);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
