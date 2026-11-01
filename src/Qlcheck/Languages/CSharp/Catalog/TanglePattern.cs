// Copyright (c) qlcheck contributors.
using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp.Catalog;

// architecture-tangle (plan 06, D9): under `no-cycles P.*`, each direct child namespace of P is a
// component (types directly in P belong to none), and dependencies between components must not
// form a cycle. Each cycle is broken greedily at its weakest edge, the one with the fewest
// references (ordinal tie-break), until none is left, and every reference on a removed edge is
// reported: the smallest change that breaks the cycle.
internal static class TanglePattern
{
    private const string Arrow = " -> ";

    public static IEnumerable<Finding> Find(IReadOnlyList<GovernedDependency> dependencies, string id)
    {
        foreach (var governed in dependencies.GroupBy(dependency => dependency.File))
        {
            foreach (var constraint in governed.Key.Constraints.Where(constraint => constraint.Kind == ConstraintKind.NoCycles))
            {
                foreach (var finding in Cycles(governed.Select(dependency => dependency.Dependency), constraint, id))
                {
                    yield return finding;
                }
            }
        }
    }

    private static IEnumerable<Finding> Cycles(IEnumerable<Dependency> dependencies, ArchitectureConstraint constraint, string id)
    {
        var prefix = constraint.Sources[0];
        var graph = new Graph(dependencies
            .Select(dependency => (From: Component(prefix, dependency.SourceNamespace), To: Component(prefix, dependency.TargetNamespace), Dependency: dependency))
            .Where(edge => edge.From is not null && edge.To is not null && edge.From != edge.To)
            .Select(edge => new Edge(edge.From!, edge.To!, edge.Dependency)));
        while (graph.Weakest() is { } weakest)
        {
            var cycle = string.Join(Arrow, [weakest.From, .. graph.PathBetween(weakest.To, weakest.From)]);
            var references = graph.Remove(weakest);
            var count = references.Count == 1 ? "1 reference. Remove it" : $"{references.Count} references. Remove them";
            var message = $"`{weakest.From}`{Arrow}`{weakest.To}` closes the cycle {cycle} ({ArchitectureFiles.FileName}:{constraint.Line}); it is the weakest link, {count}, or move what both need into a component neither depends back on.";
            foreach (var reference in references)
            {
                yield return Finding.At(id, reference.Path, reference.Location, message);
            }
        }
    }

    // `Q.Scan` for a namespace under `Q.Scan` when the prefix is `Q`; null for `Q` itself or a
    // namespace outside it.
    private static string? Component(string prefix, string ns) =>
        ns.Length > prefix.Length && DottedNames.Matches(prefix, ns) ? $"{prefix}.{ns[(prefix.Length + 1)..].Split('.')[0]}" : null;

    private readonly record struct Edge(string From, string To, Dependency Dependency);

    // The components and the references behind each dependency between two of them.
    private sealed class Graph
    {
        private readonly Dictionary<(string From, string To), List<Dependency>> _edges;

        public Graph(IEnumerable<Edge> edges)
        {
            _edges = edges.GroupBy(edge => (edge.From, edge.To)).ToDictionary(group => group.Key, group => group.Select(edge => edge.Dependency).ToList());
        }

        public IReadOnlyList<Dependency> Remove((string From, string To) edge)
        {
            var references = _edges[edge];
            _edges.Remove(edge);
            return references;
        }

        // The weakest edge inside the first strongly connected component that has a cycle, or null
        // when the graph is acyclic.
        public (string From, string To)? Weakest()
        {
            var component = StronglyConnected().Components.Where(nodes => nodes.Count > 1).MinBy(nodes => nodes.Min(StringComparer.Ordinal), StringComparer.Ordinal);
            if (component is null)
            {
                return null;
            }

            return _edges.Keys
                .Where(edge => component.Contains(edge.From) && component.Contains(edge.To))
                .OrderBy(edge => _edges[edge].Count)
                .ThenBy(edge => edge.From, StringComparer.Ordinal)
                .ThenBy(edge => edge.To, StringComparer.Ordinal)
                .First();
        }

        // The shortest path from start to goal, both included, by breadth-first search.
        public IReadOnlyList<string> PathBetween(string start, string goal)
        {
            var previous = new Dictionary<string, string> { [start] = start };
            var queue = new Queue<string>([start]);
            while (queue.Count > 0 && !previous.ContainsKey(goal))
            {
                var node = queue.Dequeue();
                foreach (var next in Successors(node).Where(next => !previous.ContainsKey(next)))
                {
                    previous[next] = node;
                    queue.Enqueue(next);
                }
            }

            var path = new List<string> { goal };
            while (path[^1] != start)
            {
                path.Add(previous[path[^1]]);
            }

            path.Reverse();
            return path;
        }

        private IEnumerable<string> Successors(string node) =>
            _edges.Keys.Where(edge => edge.From == node).Select(edge => edge.To).Order(StringComparer.Ordinal);

        // Tarjan's strongly connected components, walked from every node.
        private Tarjan StronglyConnected()
        {
            var nodes = _edges.Keys.SelectMany(edge => new[] { edge.From, edge.To }).Distinct().Order(StringComparer.Ordinal).ToList();
            var walk = new Tarjan(this);
            foreach (var node in nodes.Where(node => !walk.Visited(node)))
            {
                walk.Visit(node);
            }

            return walk;
        }

        private sealed class Tarjan(Graph graph)
        {
            private readonly Dictionary<string, int> _index = [];

            private readonly Dictionary<string, int> _low = [];

            private readonly Stack<string> _stack = new();

            private readonly List<IReadOnlySet<string>> _components = [];

            public IReadOnlyList<IReadOnlySet<string>> Components => _components;

            public bool Visited(string node) => _index.ContainsKey(node);

            public void Visit(string node)
            {
                _index[node] = _index.Count;
                _low[node] = _index[node];
                _stack.Push(node);
                foreach (var next in graph.Successors(node))
                {
                    if (!Visited(next))
                    {
                        Visit(next);
                        _low[node] = Math.Min(_low[node], _low[next]);
                        continue;
                    }

                    if (_stack.Contains(next))
                    {
                        _low[node] = Math.Min(_low[node], _index[next]);
                    }
                }

                if (_low[node] == _index[node])
                {
                    _components.Add(PopComponent(node));
                }
            }

            // The nodes on the stack down to the component's root.
            private HashSet<string> PopComponent(string root)
            {
                var component = new HashSet<string>();
                string member;
                do
                {
                    member = _stack.Pop();
                    component.Add(member);
                }
                while (member != root);
                return component;
            }
        }
    }
}
