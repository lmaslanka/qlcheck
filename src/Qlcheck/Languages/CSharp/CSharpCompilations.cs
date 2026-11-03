// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp;

internal static class CSharpCompilations
{
    private const string UnusedUsingDiagnostic = "CS8019";

    private const string CsprojSearchPattern = "*.csproj";

    private const string TrustedPlatformAssemblies = "TRUSTED_PLATFORM_ASSEMBLIES";

    private static readonly Lazy<MetadataReference[]> PlatformReferences = new(LoadPlatform);

    // Files outside any csproj compile as a nullable-enabled library (Plan 02 D2).
    public static Compilation Create(string assemblyName, IEnumerable<SyntaxTree> trees) =>
        CreateWith(
            assemblyName,
            trees,
            PlatformReferences.Value,
            NullableContextOptions.Enable,
            OutputKind.DynamicallyLinkedLibrary);

    // The project's global usings join the scanned trees as a context tree: bound against, never reported.
    public static Compilation CreateForProject(string assemblyName, string csproj, IEnumerable<SyntaxTree> trees)
    {
        var settings = ProjectSettings.Load(csproj);
        var globalUsings = ImplicitUsingsTree.For(csproj, settings);
        var all = globalUsings is null ? trees : trees.Append(globalUsings);
        var references = Merge(PlatformReferences.Value, ProjectReferences.Load(csproj), SharedFrameworks.For(settings));
        return CreateWith(assemblyName, all, references, settings.Nullable, settings.OutputKind);
    }

    public static string? FindCsproj(string filePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        while (!string.IsNullOrEmpty(dir))
        {
            var projects = Directory.GetFiles(dir, CsprojSearchPattern);
            if (projects.Length > 0)
            {
                return projects.OrderBy(p => p, StringComparer.Ordinal).First();
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    private static Compilation CreateWith(
        string assemblyName,
        IEnumerable<SyntaxTree> trees,
        IEnumerable<MetadataReference> references,
        NullableContextOptions nullable,
        OutputKind outputKind)
    {
        // Unsafe code is always allowed: a project that has it sets AllowUnsafeBlocks for its own
        // build, and CS0227 says nothing about the code a Check inspects.
        var options = new CSharpCompilationOptions(outputKind)
            .WithNullableContextOptions(nullable)
            .WithAllowUnsafe(true)
            .WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic>
                {
                    [UnusedUsingDiagnostic] = ReportDiagnostic.Warn,
                });
        return CSharpCompilation.Create(assemblyName, trees, references, options);
    }

    // Deduped by assembly file name, earlier sources first: a package's Microsoft.Extensions.* must
    // not sit beside the shared framework's copy, or every type in it is ambiguous (CS0433).
    private static MetadataReference[] Merge(params IEnumerable<MetadataReference>[] sources)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var refs = new List<MetadataReference>();
        foreach (var reference in sources.SelectMany(source => source))
        {
            var name = Path.GetFileName(reference.Display ?? string.Empty);
            if (seen.Add(name))
            {
                refs.Add(reference);
            }
        }

        return refs.ToArray();
    }

    private static MetadataReference[] LoadPlatform()
    {
        var tpa = AppContext.GetData(TrustedPlatformAssemblies) as string;
        if (string.IsNullOrEmpty(tpa))
        {
            return [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)];
        }

        // Only the runtime's own assemblies: the process also trusts qlcheck, Roslyn and their
        // dependencies, which a scanned project never referenced (plan 09).
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);
        return tpa.Split(Path.PathSeparator)
            .Where(path => File.Exists(path) && string.Equals(Path.GetDirectoryName(path), runtime, StringComparison.Ordinal))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();
    }
}
