// Copyright (c) qlcheck contributors.
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class CatalogTests
{
    private const int CatalogCount = 509;

    private const int EnabledCatalogCount = 501;

    private const int UnimplementedCatalogCount = 7;

    private const int HouseEnabledCount = 0;

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
        Assert.Equal(UnimplementedCatalogCount, architecture.Count);
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
            if (CatalogWalker.TryClass(id, out var group) && group == CheckClass.Process)
            {
                continue;
            }

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
    public void Record_Exception_call_does_not_trigger_exception_checks()
    {
        const string Source = """
            namespace X;

            using Xunit;

            public sealed class ATests
            {
                [Fact]
                public void Run_Valid_DoesNotThrow()
                {
                    var exception = Record.Exception(() => Work());

                    Assert.Null(exception);
                }

                private static void Work()
                {
                }
            }

            """;

        string[] ids =
        [
            "exception-name-extends",
            "exception-public",
            "exception-standard-constructors",
            "throw-created-exception",
        ];

        foreach (var id in ids)
        {
            Assert.Empty(Run(id, Source, "Repro/ATests.cs"));
        }
    }

    [Fact]
    public void Theory_only_test_class_satisfies_test_has_case()
    {
        const string Source = """
            namespace X;

            using Xunit;

            public sealed class T1Tests
            {
                [Theory]
                [InlineData(1)]
                public void Run_Value_Works(int value)
                {
                    Assert.Equal(1, value);
                }
            }

            """;

        var findings = Run("test-has-case", Source, "MeebliApi.Tests/X/T1Tests.cs");

        Assert.Empty(findings);
    }

    [Fact]
    public void Test_class_with_no_test_methods_still_reports_test_has_case()
    {
        const string Source = """
            namespace X;

            public sealed class EmptyTests
            {
            }

            """;

        var findings = Run("test-has-case", Source, "MeebliApi.Tests/X/EmptyTests.cs");

        Assert.Contains(findings, finding => finding.Check == "test-has-case");
    }

    [Fact]
    public void CatchOnlyRethrow_fires_only_on_the_bare_rethrow_case()
    {
        const string Source = """
            namespace X;

            using System;
            using System.Threading.Tasks;

            public sealed class StoreReadException : Exception
            {
                public StoreReadException()
                {
                }

                public StoreReadException(string message) : base(message)
                {
                }

                public StoreReadException(string message, Exception innerException) : base(message, innerException)
                {
                }
            }

            public static class Wrap
            {
                public static async Task<int> ReadAsync(Func<Task<int>> query, int page)
                {
                    try
                    {
                        return await query();
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new StoreReadException($"Read(page: {page}) failed - {ex.Message}", ex);
                    }
                }

                public static void Validate(Action check)
                {
                    try
                    {
                        check();
                    }
                    catch (Exception exception) when (exception is ArgumentException or FormatException)
                    {
                        throw new StoreReadException("Token rejected.");
                    }
                }

                public static void Rethrow(Action work)
                {
                    try
                    {
                        work();
                    }
                    catch (InvalidOperationException)
                    {
                        throw;
                    }
                }
            }

            """;

        var findings = Run("catch-only-rethrow", Source, "Wrap.cs");

        Assert.Single(findings);
        var lines = Source.Replace("\r\n", "\n").Split('\n');
        Assert.Equal("catch (InvalidOperationException)", lines[findings[0].Line - 1].Trim());
    }

    [Fact]
    public void Ssrf_checks_match_the_regression_report_for_Repo_and_Client()
    {
        const string RepoSource = """
            namespace X;

            using System.Threading.Tasks;

            public interface IDocumentRepository
            {
                Task<string> GetAsync(long requisitionId, long documentId);
            }

            public sealed class GetDocumentQuery
            {
                private readonly IDocumentRepository repository;

                public GetDocumentQuery(IDocumentRepository repository)
                {
                    this.repository = repository;
                }

                public async Task<string> QueryAsync(long requisitionId, long documentId)
                {
                    return await repository.GetAsync(requisitionId, documentId);
                }
            }

            """;

        const string ClientSource = """
            namespace X;

            using System;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class ClientOptions
            {
                public string BaseUrl { get; init; } = "https://control-plane.example";
                public string ApplicationCode { get; init; } = "app";
            }

            public sealed class ControlClient
            {
                private readonly HttpClient httpClient;
                private readonly ClientOptions options;

                public ControlClient(HttpClient httpClient, ClientOptions options)
                {
                    this.httpClient = httpClient;
                    this.options = options;
                    this.httpClient.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/", UriKind.Absolute);
                }

                public async Task<string> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken)
                {
                    return await httpClient.GetStringAsync(
                        $"api/applications/{Uri.EscapeDataString(options.ApplicationCode)}/tenants/{tenantId:D}",
                        cancellationToken);
                }

                public async Task<int> SendAsync(Guid tenantId, CancellationToken cancellationToken)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"api/tenants/{tenantId:D}/invitations");
                    using var response = await httpClient.SendAsync(request, cancellationToken);
                    return (int)response.StatusCode;
                }

                public async Task<string> UnsafeAsync(string userSuppliedUrl, CancellationToken cancellationToken)
                {
                    return await httpClient.GetStringAsync(userSuppliedUrl, cancellationToken);
                }
            }

            """;

        Assert.Empty(Run("ssrf", RepoSource, "Repo.cs"));

        var ssrfFindings = Run("ssrf", ClientSource, "Client.cs");
        Assert.Single(ssrfFindings);

        var clientLines = ClientSource.Replace("\r\n", "\n").Split('\n');
        Assert.Equal(
            "return await httpClient.GetStringAsync(userSuppliedUrl, cancellationToken);",
            clientLines[ssrfFindings[0].Line - 1].Trim());

        Assert.Empty(Run("ssrf-traversal", ClientSource, "Client.cs"));
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
        var selected = new HashSet<string>(StringComparer.Ordinal) { id };
        var messages = new Dictionary<string, string>(StringComparer.Ordinal) { [id] = Catalog.Messages[id] };

        if (CatalogWalker.TryClass(id, out var group) && group == CheckClass.Compilation)
        {
            var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
            var compilationCtx = new CompilationContext(compilation, included, messages);
            CompilationEngine.Apply(compilationCtx, selected);
            return compilationCtx.Findings;
        }

        var model = compilation.GetSemanticModel(tree);
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
