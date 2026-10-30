// Copyright (c) qlcheck contributors.
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp;

// The build settings a compilation needs, read in process from the csproj and the nearest
// Directory.Build.props (ADR 0011). Only unconditioned groups and elements count; the csproj wins
// over the props file, and within a file the last value wins.
internal sealed record ProjectSettings(
    string Sdk,
    NullableContextOptions Nullable,
    OutputKind OutputKind,
    IReadOnlyList<string> TargetFrameworks,
    bool ImplicitUsings,
    IReadOnlyList<ProjectUsing> Usings,
    IReadOnlyList<string> FrameworkReferences)
{
    private const string DirectoryBuildProps = "Directory.Build.props";

    private const string PropertyGroupName = "PropertyGroup";

    private const string ItemGroupName = "ItemGroup";

    private const string ConditionName = "Condition";

    private const string SdkName = "Sdk";

    private const string NameAttribute = "Name";

    private const string ImportName = "Import";

    private const string IncludeName = "Include";

    private const string RemoveName = "Remove";

    private const string AliasName = "Alias";

    private const string StaticName = "Static";

    private const string UsingName = "Using";

    private const string FrameworkReferenceName = "FrameworkReference";

    private const string NullableName = "Nullable";

    private const string OutputTypeName = "OutputType";

    private const string TargetFrameworkName = "TargetFramework";

    private const string TargetFrameworksName = "TargetFrameworks";

    private const string ImplicitUsingsName = "ImplicitUsings";

    private const string EnableValue = "enable";

    private const string TrueValue = "true";

    private const char ListSeparator = ';';

    private const char SdkVersionSeparator = '/';

    public static ProjectSettings Load(string csproj)
    {
        var project = XDocument.Load(csproj);
        var props = FindDirectoryBuildProps(csproj);
        var documents = props is null ? [project] : new[] { XDocument.Load(props), project };
        var properties = Properties(documents);
        return new ProjectSettings(
            SdkOf(project),
            NullableOf(properties.GetValueOrDefault(NullableName)),
            OutputKindOf(properties.GetValueOrDefault(OutputTypeName)),
            TargetFrameworksOf(properties),
            IsOn(properties.GetValueOrDefault(ImplicitUsingsName)),
            Items(documents, UsingName).SelectMany(UsingsOf).ToList(),
            FrameworkReferencesOf(Items(documents, FrameworkReferenceName)));
    }

    private static string? FindDirectoryBuildProps(string csproj)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(csproj));
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, DirectoryBuildProps);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    private static Dictionary<string, string> Properties(IEnumerable<XDocument> documents)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in Unconditioned(documents, PropertyGroupName))
        {
            properties[property.Name.LocalName] = property.Value.Trim();
        }

        return properties;
    }

    private static IEnumerable<XElement> Items(IEnumerable<XDocument> documents, string itemName) =>
        Unconditioned(documents, ItemGroupName).Where(item => item.Name.LocalName == itemName);

    private static IEnumerable<XElement> Unconditioned(IEnumerable<XDocument> documents, string groupName) =>
        from document in documents
        from grp in TopLevel(document).Where(element => element.Name.LocalName == groupName)
        where !HasCondition(grp)
        from element in grp.Elements()
        where !HasCondition(element)
        select element;

    private static IEnumerable<XElement> TopLevel(XDocument document) =>
        document.Root?.Elements() ?? [];

    private static bool HasCondition(XElement element) =>
        !string.IsNullOrWhiteSpace(element.Attribute(ConditionName)?.Value);

    private static string SdkOf(XDocument project)
    {
        var sdk = project.Root?.Attribute(SdkName)?.Value
            ?? TopLevel(project)
                .Where(element => element.Name.LocalName == SdkName)
                .Select(element => element.Attribute(NameAttribute)?.Value)
                .FirstOrDefault(name => name is not null)
            ?? TopLevel(project)
                .Where(element => element.Name.LocalName == ImportName)
                .Select(element => element.Attribute(SdkName)?.Value)
                .FirstOrDefault(name => name is not null)
            ?? string.Empty;
        var first = Split(sdk).FirstOrDefault() ?? string.Empty;
        return first.Split(SdkVersionSeparator)[0].Trim();
    }

    private static NullableContextOptions NullableOf(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "enable" => NullableContextOptions.Enable,
            "warnings" => NullableContextOptions.Warnings,
            "annotations" => NullableContextOptions.Annotations,
            _ => NullableContextOptions.Disable,
        };

    private static OutputKind OutputKindOf(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "exe" => OutputKind.ConsoleApplication,
            "winexe" => OutputKind.WindowsApplication,
            _ => OutputKind.DynamicallyLinkedLibrary,
        };

    private static IReadOnlyList<string> TargetFrameworksOf(Dictionary<string, string> properties)
    {
        if (properties.TryGetValue(TargetFrameworkName, out var single) && single.Length > 0)
        {
            return [single];
        }

        return Split(properties.GetValueOrDefault(TargetFrameworksName)).ToList();
    }

    private static bool IsOn(string? value) =>
        string.Equals(value, EnableValue, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, TrueValue, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<ProjectUsing> UsingsOf(XElement item)
    {
        var alias = Metadata(item, AliasName);
        var isStatic = IsOn(Metadata(item, StaticName));
        foreach (var name in Split(item.Attribute(IncludeName)?.Value))
        {
            yield return new ProjectUsing(name, Remove: false, string.IsNullOrEmpty(alias) ? null : alias, isStatic);
        }

        foreach (var name in Split(item.Attribute(RemoveName)?.Value))
        {
            yield return new ProjectUsing(name, Remove: true, Alias: null, Static: false);
        }
    }

    private static IReadOnlyList<string> FrameworkReferencesOf(IEnumerable<XElement> items)
    {
        var references = new List<string>();
        foreach (var item in items)
        {
            references.AddRange(Split(item.Attribute(IncludeName)?.Value));
            var removed = Split(item.Attribute(RemoveName)?.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            references.RemoveAll(removed.Contains);
        }

        return references;
    }

    // MSBuild item metadata may be written as an attribute or as a child element.
    private static string? Metadata(XElement item, string name) =>
        item.Attribute(name)?.Value.Trim()
        ?? item.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value.Trim();

    private static IEnumerable<string> Split(string? list) =>
        (list ?? string.Empty)
            .Split(ListSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
