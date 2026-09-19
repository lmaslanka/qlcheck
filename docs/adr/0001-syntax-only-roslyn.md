# Inspect C# with Roslyn syntax trees, not compilation

Checks parse source text into syntax trees. They do not load projects, resolve symbols, or use a semantic model. That keeps the CLI a path-in, findings-out tool an agent can run without restore or build. The cost is name-based matching: any `QueryAsync` looks like Dapper.
