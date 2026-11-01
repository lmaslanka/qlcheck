// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// The control-flow graphs of one file's executable bodies, built lazily and cached. Binding a body
// is the expensive part, so a body is only built when one of its tokens is one the caller cares
// about (a name that binds to a sink, say).
internal sealed class BodyGraphs(SyntaxTree tree, SemanticModel model)
{
    private readonly Dictionary<SyntaxNode, BodyGraph?> _cache = [];

    private readonly Lazy<IReadOnlyList<SyntaxNode>> _bodies = new(() => Declarations(tree.GetRoot()));

    // Graphs of the bodies the caller finds relevant, judged from the declaration and the tokens of
    // its executable part.
    public IEnumerable<BodyGraph> Where(BodyFilter relevant)
    {
        foreach (var body in _bodies.Value)
        {
            if (relevant(body, [.. Executable(body)]) && Graph(body) is { } graph)
            {
                yield return graph;
            }
        }
    }

    // Methods, constructors, operators, destructors and accessors with a body or an expression
    // body, expression-bodied properties and indexers, and the compilation unit when it holds
    // top-level statements.
    private static IReadOnlyList<SyntaxNode> Declarations(SyntaxNode root)
    {
        var bodies = root.DescendantNodes().Where(IsBody).ToList();
        if (root is CompilationUnitSyntax unit && unit.Members.Any(member => member is GlobalStatementSyntax))
        {
            bodies.Insert(0, unit);
        }

        return bodies;
    }

    private static bool IsBody(SyntaxNode node) =>
        node switch
        {
            BaseMethodDeclarationSyntax method => method.Body is not null || method.ExpressionBody is not null,
            AccessorDeclarationSyntax accessor => accessor.Body is not null || accessor.ExpressionBody is not null,
            BasePropertyDeclarationSyntax property => property is PropertyDeclarationSyntax { ExpressionBody: not null }
                or IndexerDeclarationSyntax { ExpressionBody: not null },
            _ => false,
        };

    // Only the parts that run count: the block or expression body, a constructor's `: base(...)`,
    // or the top-level statements, not the declaration's own name, parameters or neighbouring
    // types. A local function or lambda is part of its container's body, so its identifiers count.
    private static IEnumerable<SyntaxToken> Executable(SyntaxNode body) =>
        body.ChildNodes()
            .Where(child => child is BlockSyntax or ArrowExpressionClauseSyntax or ConstructorInitializerSyntax or GlobalStatementSyntax)
            .SelectMany(child => child.DescendantTokens());

    private BodyGraph? Graph(SyntaxNode body)
    {
        if (!_cache.TryGetValue(body, out var graph))
        {
            graph = Build(body);
            _cache[body] = graph;
        }

        return graph;
    }

    private BodyGraph? Build(SyntaxNode body)
    {
        var root = body switch
        {
            PropertyDeclarationSyntax property => property.ExpressionBody,
            IndexerDeclarationSyntax indexer => indexer.ExpressionBody,
            _ => body,
        };

        try
        {
            return root is not null && ControlFlowGraph.Create(root, model) is { } graph ? new BodyGraph(body, Owner(body), graph) : null;
        }
        catch (ArgumentException)
        {
            // Not an executable block Roslyn can build a graph for (or invalid code): no Findings.
            return null;
        }
    }

    private IMethodSymbol? Owner(SyntaxNode body) =>
        model.GetDeclaredSymbol(body) switch
        {
            IMethodSymbol method => method,
            IPropertySymbol property => property.GetMethod,
            _ => null,
        };
}
