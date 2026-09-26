using System.Xml.Linq;

namespace Qlcheck.Languages.CSharp.Checks.Coverage;

internal static class CsprojReader
{
    private const string ProjectReferenceName = "ProjectReference";

    private const string PackageReferenceName = "PackageReference";

    private const string TargetFrameworkName = "TargetFramework";

    private const string TargetFrameworksName = "TargetFrameworks";

    private const string AssemblyNameName = "AssemblyName";

    private const string IsTestProjectName = "IsTestProject";

    private const string IncludeName = "Include";

    private const string TrueValue = "true";

    private const char PropertyMarker = '$';

    private static readonly HashSet<string> TestPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "xunit",
        "xunit.v3",
        "xunit.v3.mtp-v2",
        "xunit.runner.visualstudio",
        "nunit",
        "NUnit3TestAdapter",
        "MSTest",
        "MSTest.TestFramework",
        "MSTest.TestAdapter",
        "Microsoft.NET.Test.Sdk",
        "Microsoft.Testing.Platform",
    };

    public static Info Read(string path)
    {
        var document = XDocument.Load(path);
        var assembly = Value(document, AssemblyNameName);
        if (string.IsNullOrEmpty(assembly))
        {
            assembly = Path.GetFileNameWithoutExtension(path);
        }

        var frameworks = Value(document, TargetFrameworksName);
        return new Info(
            assembly,
            Value(document, TargetFrameworkName),
            !string.IsNullOrEmpty(frameworks),
            IsTest(document),
            References(path, document, out var unresolved),
            unresolved);
    }

    public static string IncludeFilter(string path)
    {
        var info = Read(path);
        return $"[{info.AssemblyName}]*";
    }

    public static void RequireSingleFramework(string path)
    {
        if (!Read(path).MultipleFrameworks)
        {
            return;
        }

        throw new InvalidOperationException($"{path} has multiple target frameworks.");
    }

    private static bool IsTest(XDocument document)
    {
        if (string.Equals(Value(document, IsTestProjectName), TrueValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var element in document.Descendants())
        {
            if (element.Name.LocalName != PackageReferenceName)
            {
                continue;
            }

            var include = element.Attribute(IncludeName)?.Value;
            if (include is not null && TestPackages.Contains(include))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> References(string path, XDocument document, out bool unresolved)
    {
        unresolved = false;
        var references = new List<string>();
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        foreach (var element in document.Descendants())
        {
            if (element.Name.LocalName != ProjectReferenceName)
            {
                continue;
            }

            var include = element.Attribute(IncludeName)?.Value;
            if (string.IsNullOrEmpty(include))
            {
                continue;
            }

            if (include.Contains(PropertyMarker))
            {
                unresolved = true;
                continue;
            }

            var normalized = include.Replace('\\', '/');
            references.Add(Path.GetFullPath(Path.Combine(dir, normalized)));
        }

        return references;
    }

    private static string? Value(XDocument document, string name)
    {
        foreach (var element in document.Descendants())
        {
            if (element.Name.LocalName != name)
            {
                continue;
            }

            var value = element.Value.Trim();
            if (value.Length > 0)
            {
                return value;
            }
        }

        return null;
    }

    internal sealed record Info(
        string AssemblyName,
        string? TargetFramework,
        bool MultipleFrameworks,
        bool IsTestProject,
        IReadOnlyList<string> ProjectReferences,
        bool UnresolvedReference);
}
