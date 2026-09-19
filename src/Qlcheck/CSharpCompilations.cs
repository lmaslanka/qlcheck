using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck;

internal static class CSharpCompilations
{
    private static readonly Lazy<MetadataReference[]> PlatformReferences = new(LoadPlatform);

    public static Compilation Create(string assemblyName, IEnumerable<SyntaxTree> trees)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic>
                {
                    ["CS8019"] = ReportDiagnostic.Warn,
                });
        return CSharpCompilation.Create(assemblyName, trees, PlatformReferences.Value, options);
    }

    public static string? FindCsproj(string filePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        while (!string.IsNullOrEmpty(dir))
        {
            var projects = Directory.GetFiles(dir, "*.csproj");
            if (projects.Length > 0)
            {
                return projects.OrderBy(p => p, StringComparer.Ordinal).First();
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    private static MetadataReference[] LoadPlatform()
    {
        var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
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
