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

    public static Compilation Create(string assemblyName, IEnumerable<SyntaxTree> trees) =>
        CreateWith(assemblyName, trees, PlatformReferences.Value);

    public static Compilation CreateForProject(string assemblyName, string csproj, IEnumerable<SyntaxTree> trees) =>
        CreateWith(assemblyName, trees, Merge(PlatformReferences.Value, ProjectReferences.Load(csproj)));

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
        IEnumerable<MetadataReference> references)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic>
                {
                    [UnusedUsingDiagnostic] = ReportDiagnostic.Warn,
                });
        return CSharpCompilation.Create(assemblyName, trees, references, options);
    }

    private static MetadataReference[] Merge(
        IEnumerable<MetadataReference> platform,
        IEnumerable<MetadataReference> extra)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var refs = new List<MetadataReference>();
        foreach (var reference in platform.Concat(extra))
        {
            var path = reference.Display ?? string.Empty;
            if (seen.Add(path))
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

        return tpa.Split(Path.PathSeparator)
            .Where(File.Exists)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();
    }
}
