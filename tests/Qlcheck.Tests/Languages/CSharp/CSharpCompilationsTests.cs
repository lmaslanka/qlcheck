using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class CSharpCompilationsTests
{
    private const string ImplicitUsingsOn = "<PropertyGroup><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>";

    private const string GeneratedStringBuilder = "global using global::System.Text;\n";

    private const string UsesStringBuilder = "class C { object M() => new StringBuilder(); }";

    private const string UsesList = "class C { object M() => new List<int>(); }";

    private const string Unresolved = "CS0246";

    private const string UsesHttpContext = "class C { Microsoft.AspNetCore.Http.HttpContext? Context; }";

    private const string LoggingAbstractions = "Microsoft.Extensions.Logging.Abstractions";

    private const string AssemblyName = "Sample";

    private const string TempPrefix = "qlcheck_globals_";

    private static readonly DateTime Earlier = new(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime Later = new(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime ProjectTime = new(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc);

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

    [Fact]
    public void Implicit_usings_resolve_sdk_namespaces_without_a_using()
    {
        const string source = """
            class C
            {
                List<int> Items() => new();
                string Name(string path) => Path.GetFileName(path);
            }
            """;

        Assert.Empty(Errors(Sdk(ImplicitUsingsOn), source));
    }

    [Fact]
    public void Without_implicit_usings_sdk_namespaces_stay_unresolved()
    {
        Assert.Contains(Unresolved, Errors(Sdk(string.Empty), UsesList));
    }

    [Fact]
    public void Generated_global_usings_win_when_newer_than_the_csproj()
    {
        var errors = Errors(Sdk(string.Empty), UsesStringBuilder, generated: GeneratedStringBuilder, generatedTime: Later);

        Assert.Empty(errors);
    }

    [Fact]
    public void A_csproj_newer_than_the_generated_global_usings_is_synthesized()
    {
        var project = Sdk(ImplicitUsingsOn);

        Assert.Contains(Unresolved, Errors(project, UsesStringBuilder, generated: GeneratedStringBuilder, generatedTime: Earlier));
        Assert.Empty(Errors(project, UsesList, generated: GeneratedStringBuilder, generatedTime: Earlier));
    }

    [Fact]
    public void Using_items_are_added_and_removed()
    {
        const string items = """
            <ItemGroup>
              <Using Include="Xunit" />
              <Using Remove="System.Linq" />
              <Using Include="System.Text.StringBuilder" Alias="Sb" />
              <Using Include="System.Math" Static="true" />
            </ItemGroup>
            """;
        var project = Sdk($"{ImplicitUsingsOn}{items}");
        const string source = """
            namespace Xunit { public sealed class StubAttribute : System.Attribute { } }

            [Stub]
            class C
            {
                object M() => new Sb();
                double N() => Abs(-1.0);
            }
            """;
        const string usesLinq = "class D { object M() => new int[0].Select(x => x); }";

        Assert.Empty(Errors(project, source));
        Assert.Contains("CS1061", Errors(project, usesLinq));
    }

    [Fact]
    public void An_unknown_sdk_gets_only_using_items()
    {
        const string project = """
            <Project Sdk="Custom.Sdk">
              <PropertyGroup><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
              <ItemGroup><Using Include="System.Text" /></ItemGroup>
            </Project>
            """;

        Assert.Empty(Errors(project, UsesStringBuilder));
        Assert.Contains(Unresolved, Errors(project, UsesList));
    }

    [Theory]
    [InlineData("enable", NullableContextOptions.Enable)]
    [InlineData("disable", NullableContextOptions.Disable)]
    [InlineData("warnings", NullableContextOptions.Warnings)]
    [InlineData("annotations", NullableContextOptions.Annotations)]
    public void A_project_compilation_uses_the_csproj_nullable_setting(string value, NullableContextOptions expected)
    {
        var compilation = CompileProject(Sdk($"<PropertyGroup><Nullable>{value}</Nullable></PropertyGroup>"), UsesList);

        Assert.Equal(expected, compilation.Options.NullableContextOptions);
    }

    [Fact]
    public void The_adhoc_compilation_enables_nullable()
    {
        var compilation = CSharpCompilations.Create(AssemblyName, [CSharpSyntaxTree.ParseText(UsesList)]);

        Assert.Equal(NullableContextOptions.Enable, compilation.Options.NullableContextOptions);
        Assert.Equal(OutputKind.DynamicallyLinkedLibrary, compilation.Options.OutputKind);
    }

    [Fact]
    public void An_exe_project_compiles_top_level_statements()
    {
        var project = Sdk("<PropertyGroup><OutputType>Exe</OutputType></PropertyGroup>");

        var compilation = CompileProject(project, "System.Console.WriteLine(args.Length);\n");

        Assert.Equal(OutputKind.ConsoleApplication, compilation.Options.OutputKind);
        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Id == "CS8805");
    }

    [AspNetPackFact]
    public void A_web_sdk_project_references_the_aspnetcore_pack()
    {
        const string project = "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>";

        Assert.Empty(Errors(project, UsesHttpContext));
    }

    [AspNetPackFact]
    public void A_framework_reference_to_aspnetcore_references_the_pack()
    {
        var project = Sdk("<ItemGroup><FrameworkReference Include=\"Microsoft.AspNetCore.App\" /></ItemGroup>");

        Assert.Empty(Errors(project, UsesHttpContext));
    }

    [Fact]
    public void A_library_project_does_not_reference_the_aspnetcore_pack()
    {
        Assert.Contains("CS0234", Errors(Sdk(string.Empty), UsesHttpContext));
    }

    [AspNetPackFact]
    public void A_package_assembly_wins_over_the_pack_copy_of_the_same_file()
    {
        var dir = Directory.CreateTempSubdirectory(TempPrefix);
        try
        {
            var dll = Path.Combine(dir.FullName, $"{LoggingAbstractions}.dll");
            EmitLoggingAbstractions(dll);
            var project = $"""
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <ItemGroup><Reference Include="{LoggingAbstractions}"><HintPath>{dll}</HintPath></Reference></ItemGroup>
                </Project>
                """;
            var csproj = WriteProject(dir, project, null, default);
            const string source = """
                class C
                {
                    Microsoft.Extensions.Logging.ILogger? Logger;
                    Microsoft.Extensions.Logging.PackageMarker? Marker;
                }
                """;
            var tree = CSharpSyntaxTree.ParseText(source, path: Path.Combine(dir.FullName, "Repo.cs"));

            var compilation = CSharpCompilations.CreateForProject(AssemblyName, csproj, [tree]);

            Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("Microsoft.NET.Sdk.Web", "Microsoft.AspNetCore.Http", true)]
    [InlineData("Microsoft.NET.Sdk.Web", "Microsoft.Extensions.Logging", true)]
    [InlineData("Microsoft.NET.Sdk.Worker", "Microsoft.Extensions.Hosting", true)]
    [InlineData("Microsoft.NET.Sdk.Worker", "Microsoft.AspNetCore.Http", false)]
    [InlineData("Microsoft.NET.Sdk", "Microsoft.Extensions.Logging", false)]
    public void Each_sdk_synthesizes_its_default_usings(string sdk, string ns, bool expected)
    {
        var settings = new ProjectSettings(
            sdk, NullableContextOptions.Disable, OutputKind.DynamicallyLinkedLibrary, [], ImplicitUsings: true, [], []);

        var text = ImplicitUsingsTree.Synthesize(settings);

        Assert.Contains("global using global::System.Linq;", text, StringComparison.Ordinal);
        Assert.Equal(expected, text.Contains($"global using global::{ns};", StringComparison.Ordinal));
    }

    [Fact]
    public void The_global_usings_tree_is_never_reported()
    {
        var dir = Directory.CreateTempSubdirectory(TempPrefix);
        try
        {
            var csproj = WriteProject(dir, Sdk(string.Empty), GeneratedStringBuilder, Later);
            var repoPath = Path.Combine(dir.FullName, "Repo.cs");
            var file = new SourceFile("Repo.cs", "using System.IO;\n\nclass C { }\n");
            var trees = new Dictionary<string, SyntaxTree> { [repoPath] = CSharpTrees.Parse(file) };
            var checks = new List<CatalogCheck> { Catalog.Checks.First(check => check.Id == "unused-using") };

            var findings = CatalogRun.Execute([new SourceScan.LoadedSource(repoPath, file)], csproj, trees, checks);

            var finding = Assert.Single(findings);
            Assert.Equal("Repo.cs", finding.File);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static string Sdk(string body) => $"<Project Sdk=\"Microsoft.NET.Sdk\">{body}</Project>";

    private static List<string> Errors(
        string csprojXml,
        string source,
        string? generated = null,
        DateTime generatedTime = default)
    {
        var compilation = CompileProject(csprojXml, source, generated, generatedTime);
        var tree = compilation.SyntaxTrees.First();
        return compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error && d.Location.SourceTree == tree)
            .Select(d => d.Id)
            .ToList();
    }

    private static Compilation CompileProject(
        string csprojXml,
        string source,
        string? generated = null,
        DateTime generatedTime = default)
    {
        var dir = Directory.CreateTempSubdirectory(TempPrefix);
        try
        {
            var csproj = WriteProject(dir, csprojXml, generated, generatedTime);
            var tree = CSharpSyntaxTree.ParseText(source, path: Path.Combine(dir.FullName, "Repo.cs"));
            return CSharpCompilations.CreateForProject(AssemblyName, csproj, [tree]);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static string WriteProject(DirectoryInfo dir, string csprojXml, string? generated, DateTime generatedTime)
    {
        var csproj = Path.Combine(dir.FullName, "Sample.csproj");
        File.WriteAllText(csproj, csprojXml);
        File.SetLastWriteTimeUtc(csproj, ProjectTime);
        if (generated is not null)
        {
            var obj = Directory.CreateDirectory(Path.Combine(dir.FullName, "obj", "Debug", "net10.0"));
            var path = Path.Combine(obj.FullName, "Sample.GlobalUsings.g.cs");
            File.WriteAllText(path, generated);
            File.SetLastWriteTimeUtc(path, generatedTime);
        }

        return csproj;
    }

    private static void EmitLoggingAbstractions(string path)
    {
        const string source = """
            namespace Microsoft.Extensions.Logging
            {
                public interface ILogger { }
                public sealed class PackageMarker { }
            }
            """;
        var compilation = CSharpCompilation.Create(
            LoggingAbstractions,
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.True(compilation.Emit(path).Success);
    }
}
