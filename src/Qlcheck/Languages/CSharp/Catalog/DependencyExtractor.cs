// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog;

// The type references of one file, for architecture Constraints (plan 06, D6). Every simple name
// outside using and namespace declarations is bound, and a member reduces to the type declaring it
// (an extension method to its static class), so fully qualified names, parent-namespace types,
// global, alias and static usings, attributes, generic arguments, base types and nameof all count.
// Unresolved names give nothing (ADR 0003). A using directive also counts, syntactically, as a
// reference from each namespace the file declares to the namespace or type it names, so a project
// that is not built still reports its usings. One dependency per line and target.
internal static class DependencyExtractor
{
    private const string GlobalPrefix = "global::";

    private static readonly SymbolDisplayFormat DottedName = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None);

    public static IEnumerable<Dependency> Extract(SemanticModel model, string path)
    {
        var root = model.SyntaxTree.GetRoot();
        var sources = new Dictionary<SyntaxNode, DependencySide?>();
        var references = root.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Where(name => !IsDeclarationName(name))
            .Select(name => (Name: name, Target: TargetOf(model.GetSymbolInfo(name).Symbol), Source: SourceOf(model, name, sources)))
            .Where(reference => reference.Target is not null && reference.Source is not null)
            .Select(reference => new Dependency(
                reference.Source!.Name, reference.Target!.Name, path, reference.Name.GetLocation(), reference.Source.Namespace, reference.Target.Namespace));
        return references.Concat(Usings(root, path))
            .GroupBy(dependency => (dependency.Location.GetLineSpan().StartLinePosition.Line, dependency.Target))
            .Select(group => group.First());
    }

    // Part of a using directive or of a namespace declaration's own name.
    private static bool IsDeclarationName(SimpleNameSyntax name) =>
        name.Ancestors().Any(ancestor => ancestor is UsingDirectiveSyntax
            || ancestor is BaseNamespaceDeclarationSyntax ns && ns.Name.Span.Contains(name.Span));

    // The type a referenced symbol stands for, as a dotted name; null for a namespace, a local or a
    // name that did not bind.
    private static DependencySide? TargetOf(ISymbol? symbol)
    {
        var type = symbol switch
        {
            IAliasSymbol alias => alias.Target as INamedTypeSymbol,
            INamedTypeSymbol named => named.OriginalDefinition,
            IMethodSymbol method => (method.ReducedFrom ?? method).ContainingType?.OriginalDefinition,
            IPropertySymbol or IFieldSymbol or IEventSymbol => symbol.ContainingType?.OriginalDefinition,
            _ => null,
        };
        return type is null or { TypeKind: TypeKind.Error } ? null : new DependencySide(type.ToDisplayString(DottedName), NamespaceOf(type));
    }

    private static string NamespaceOf(ISymbol symbol) =>
        symbol.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : string.Empty;

    // The innermost type declaring the code, or else its namespace; null in the global namespace.
    private static DependencySide? SourceOf(SemanticModel model, SyntaxNode node, Dictionary<SyntaxNode, DependencySide?> cache)
    {
        var owner = node.Ancestors().FirstOrDefault(ancestor => ancestor is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or BaseNamespaceDeclarationSyntax);
        if (owner is null)
        {
            return null;
        }

        if (!cache.TryGetValue(owner, out var source))
        {
            source = owner switch
            {
                BaseNamespaceDeclarationSyntax ns => new DependencySide(ns.Name.ToString(), ns.Name.ToString()),
                _ => model.GetDeclaredSymbol(owner) is { } type ? new DependencySide(type.ToDisplayString(DottedName), NamespaceOf(type)) : null,
            };
            cache[owner] = source;
        }

        return source;
    }

    private static IEnumerable<Dependency> Usings(SyntaxNode root, string path)
    {
        var namespaces = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().Select(ns => ns.Name.ToString()).Distinct(StringComparer.Ordinal).ToList();
        return root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Where(directive => directive.Name is not null)
            .SelectMany(directive => namespaces.Select(source => new Dependency(source, Named(directive.Name!), path, directive.Name!.GetLocation(), source, Named(directive.Name!))));
    }

    private static string Named(NameSyntax name)
    {
        var text = name.ToString();
        return text.StartsWith(GlobalPrefix, StringComparison.Ordinal) ? text[GlobalPrefix.Length..] : text;
    }

    // One side of a dependency: its dotted name and the namespace it lives in.
    private sealed record DependencySide(string Name, string Namespace);
}
