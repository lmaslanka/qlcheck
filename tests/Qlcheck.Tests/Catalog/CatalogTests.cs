// Copyright (c) qlcheck contributors.
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class CatalogTests
{
    private const int CatalogCount = 503;

    private const int EnabledCatalogCount = 496;

    private const int HouseEnabledCount = 6;

    private const string UnknownId = "nope";

    private const string ArchitectureId = "architecture-relationship";

    private const string MethodComplexityId = "method-complexity";

    private const string RulesFile = "rules.txt";

    private const string SolutionName = "Qlcheck.slnx";

    private const string FixtureFile = "fixtures.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Catalog_has_only_id_and_message()
    {
        var rows = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(ReadCatalogJson());
        Assert.NotNull(rows);
        Assert.Equal(CatalogCount, rows.Count);
        Assert.Equal(CatalogCount, rows.Select(row => row["id"].GetString()).Distinct().Count());
        foreach (var row in rows)
        {
            Assert.Equal(2, row.Count);
            Assert.False(string.IsNullOrWhiteSpace(row["id"].GetString()));
            Assert.False(string.IsNullOrWhiteSpace(row["message"].GetString()));
        }
    }

    [Fact]
    public void Discovery_contains_catalog_ids_and_not_source_keys()
    {
        var ids = CheckDiscovery.All().Select(check => check.Id).ToList();
        foreach (var check in Catalog.Checks)
        {
            Assert.Contains(check.Id, ids);
        }

        var rules = FindRules();
        if (rules is null)
        {
            return;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(rules));
        foreach (var rule in doc.RootElement.GetProperty("rules").EnumerateArray())
        {
            var key = rule.GetProperty("key").GetString();
            Assert.DoesNotContain(ids, id => id == key);
        }
    }

    [Fact]
    public void Enabled_count_is_house_plus_implemented_catalog()
    {
        var enabled = CheckDiscovery.All().Count(check => check.EnabledByDefault);
        Assert.Equal(HouseEnabledCount + EnabledCatalogCount, enabled);
    }

    [Fact]
    public void Architecture_checks_are_discovered_and_unimplemented()
    {
        var architecture = Catalog.Checks.Where(check => !CatalogWalker.IsImplemented(check.Id)).ToList();
        Assert.Equal(CatalogCount - EnabledCatalogCount, architecture.Count);
        Assert.All(architecture, check => Assert.False(check.EnabledByDefault));
        Assert.Contains(architecture, check => check.Id == ArchitectureId);
    }

    [Fact]
    public void Selected_architecture_check_exits_two()
    {
        var file = WriteTemp("class C { }\n");
        var stderr = new StringWriter();
        var code = QlcheckApp.Run(["--check", ArchitectureId, file], new StringWriter(), stderr);

        Assert.Equal(ExitCode.Error, code);
        Assert.Contains($"Check '{ArchitectureId}' has no implementation.", stderr.ToString());
    }

    [Fact]
    public void Unknown_check_is_still_rejected()
    {
        var file = WriteTemp("class C { }\n");
        var stderr = new StringWriter();
        var code = QlcheckApp.Run(["--check", UnknownId, file], new StringWriter(), stderr);

        Assert.Equal(ExitCode.Error, code);
        Assert.Contains($"Unknown check: {UnknownId}", stderr.ToString());
    }

    [Fact]
    public void Method_complexity_reports_a_fixture()
    {
        var fixture = LoadFixtures()[MethodComplexityId];
        var file = WriteTemp(fixture.Bad);
        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--check", MethodComplexityId, file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        Assert.Contains(MethodComplexityId, stdout.ToString());
    }

    [Fact]
    public void Each_implemented_check_has_a_failing_and_clean_fixture()
    {
        var fixtures = LoadFixtures();
        var failures = new List<string>();
        foreach (var id in CatalogWalker.ImplementedIds)
        {
            var fixture = fixtures[id];
            if (!Run(id, fixture.Bad, fixture.Path).Any(finding => finding.Check == id))
            {
                failures.Add($"{id} missed");
            }

            if (Run(id, fixture.Good, fixture.Path).Any(finding => finding.Check == id))
            {
                failures.Add($"{id} clean");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void Project_reference_resolves_a_type_the_platform_compilation_misses()
    {
        var dir = Directory.CreateTempSubdirectory("qlcheck_ref_");
        try
        {
            var dll = Path.Combine(dir.FullName, "Extra.dll");
            EmitDisposable(dll);
            var csproj = Path.Combine(dir.FullName, "Extra.csproj");
            File.WriteAllText(csproj, HintProject(dll));
            var source = "class C { void M() { var item = new Marker(); } }\n";
            var file = Path.Combine(dir.FullName, "Repo.cs");
            File.WriteAllText(file, source);
            var tree = CSharpSyntaxTree.ParseText(source, path: file);
            var platform = CSharpCompilations.Create("adhoc", [tree]);
            var referenced = CSharpCompilations.CreateForProject("extra", csproj, [tree]);
            var creation = tree.GetRoot().DescendantNodes().First(node => node.IsKind(SyntaxKind.ObjectCreationExpression));
            var platformType = platform.GetSemanticModel(tree).GetTypeInfo(creation).Type;
            var referencedType = referenced.GetSemanticModel(tree).GetTypeInfo(creation).Type;
            Assert.True(platformType is null || platformType.TypeKind == TypeKind.Error);
            Assert.NotNull(referencedType);
            Assert.NotEqual(TypeKind.Error, referencedType.TypeKind);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static IReadOnlyList<Finding> Run(string id, string source, string path)
    {
        var file = new SourceFile(path, source);
        var tree = CSharpTrees.Parse(file);
        var compilation = CSharpCompilations.Create("fixture", [tree]);
        var model = compilation.GetSemanticModel(tree);
        var selected = new HashSet<string>(StringComparer.Ordinal) { id };
        var messages = new Dictionary<string, string>(StringComparer.Ordinal) { [id] = Catalog.Messages[id] };
        return CatalogWalker.Walk(file, tree, model, selected, messages);
    }

    private static Dictionary<string, Fixture> LoadFixtures()
    {
        var path = Path.Combine(RepoRoot(), "tests", "Qlcheck.Tests", "Catalog", FixtureFile);
        var rows = JsonSerializer.Deserialize<Dictionary<string, Fixture>>(File.ReadAllText(path), JsonOptions);
        return rows ?? [];
    }

    private static string ReadCatalogJson()
    {
        var path = Path.Combine(RepoRoot(), "src", "Qlcheck", "Languages", "CSharp", "Catalog", "checks.json");
        return File.ReadAllText(path);
    }

    private static string? FindRules()
    {
        var candidate = Path.Combine(Directory.GetParent(RepoRoot())!.FullName, RulesFile);
        return File.Exists(candidate) ? candidate : null;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, SolutionName)))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repo root was not found.");
    }

    private static string WriteTemp(string text)
    {
        var dir = Directory.CreateTempSubdirectory("qlcheck_cat_");
        var path = Path.Combine(dir.FullName, "Repo.cs");
        File.WriteAllText(path, text);
        return path;
    }

    private static void EmitDisposable(string path)
    {
        var tree = CSharpSyntaxTree.ParseText("public class Marker : System.IDisposable { public void Dispose() { } }\n");
        var compilation = CSharpCompilation.Create(
            "Extra",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var result = compilation.Emit(path);
        Assert.True(result.Success);
    }

    private static string HintProject(string dll) =>
        $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <Reference Include="Extra">
              <HintPath>{dll}</HintPath>
            </Reference>
          </ItemGroup>
        </Project>
        """;

    private sealed record Fixture(string Bad, string Good, string Path);
}
