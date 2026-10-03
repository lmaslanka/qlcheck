# Fix: `sql-injection` matches any `.Execute(...)` call regardless of receiver type

## Reported issue

From `requisitionops/api/QLCHECK_KNOWN_ISSUES.md` (36 findings, confirmed false positives):

> every instance is a controller or action calling `someCommand.Execute(...)` on a
> strongly-typed, injected command/query object (e.g.
> `updateRequisitionCommand.Execute(recordId, model)`). None involve string-built SQL — actual
> SQL text lives only in the Persistence layer, which uses Dapper `@param` parameters
> throughout. The checker appears to pattern-match on the token `.Execute(` regardless of what
> is being executed.

## Root cause

- Row: `TaintEngine.cs:28` — `Row.Taint("sql-injection", CheckClass.Taint, "Execute")`.
- All `Row.Taint(...)` ids funnel through `TaintMatch.Report` → `ReportInvocations`
  (`Match/TaintMatch.cs:21-29`), which matches on `Names.Invocation(invocation)` — the
  invoked method's simple name only, purely syntactic, with **no receiver-type check at all**
  (`Names.Invocation` never looks at `ctx.Model`). Any `x.Execute(arg)` fires as long as one
  argument is "tainted" (see below) — `x`'s actual type (a DI'd `ICommand<T>`, not
  `SqlCommand`/`DbCommand`) is never inspected.
- "Tainted" (`TaintMatch.IsTainted`, `Match/TaintMatch.cs:85-108`, via `IsParameter`,
  lines 110-132) means: the argument is literally a parameter of the enclosing method, and
  that method is not `private`. `updateRequisitionCommand.Execute(recordId, model)` fires
  because `Execute` is the name and `recordId`/`model` are public-method parameters — nothing
  about SQL, string-building, or the receiver's type is ever checked.

## Proposed fix

Add a semantic receiver-type filter specific to this row. `Queries/Symbols.cs:12-16`
(`Symbols.TypeOf(ctx, node)`, wrapping `ctx.Model.GetTypeInfo`) already exists and is used
elsewhere in the catalog (`Patterns.PrivateUnused`, `Symbols.IsDisposable`/`Implements`), so
this reuses existing capability rather than adding new infrastructure.

The known raw-SQL-executing type set already exists in this codebase —
`InlineSqlPattern.CommandTypes` (`Catalog/InlineSqlPattern.cs`): `SqlCommand`, `NpgsqlCommand`,
`SqliteCommand`, `MySqlCommand`, `DbCommand`. Reuse it (promote to `internal` if needed, or
duplicate as a small shared constant) rather than redefining it.

Since `TaintMatch.Report`'s signature (`string[] sinks`) is shared by ~19 other ids
(`TaintEngine.cs:8-36`), don't change it globally — add a new, narrower entry point used only
by `sql-injection`:

```csharp
// Row.cs — new DSL entry, mirrors Row.Taint but adds a receiver-type allowlist
public static WalkRow TaintOn(string id, CheckClass group, string[] allowedTypes, params string[] sinks) =>
    new()
    {
        Id = id,
        Class = group,
        Apply = ctx => TaintMatch.ReportOnTypes(ctx, id, allowedTypes, sinks),
    };
```

```csharp
// TaintMatch.cs — new method, only used by the receiver-restricted rows
public static void ReportOnTypes(WalkContext ctx, string id, string[] allowedTypes, string[] sinks)
{
    var types = new HashSet<string>(allowedTypes, StringComparer.Ordinal);
    var sinkSet = new HashSet<string>(sinks, StringComparer.Ordinal);
    foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
    {
        if (node is not InvocationExpressionSyntax invocation
            || !sinkSet.Contains(Names.Invocation(invocation))
            || invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            continue;
        }

        var receiverType = Symbols.TypeOf(ctx, member.Expression);
        if (receiverType is null || !types.Contains(receiverType.Name))
        {
            continue;
        }

        ReportIfTainted(ctx, id, invocation, invocation.ArgumentList);
    }
}
```

(`ReportIfTainted` is already `private` in `TaintMatch.cs` — this reuses it directly since it's
defined in the same class.)

```csharp
// TaintEngine.cs
Row.TaintOn(
    "sql-injection",
    CheckClass.Taint,
    ["SqlCommand", "NpgsqlCommand", "SqliteCommand", "MySqlCommand", "DbCommand"],
    "Execute", "ExecuteAsync", "ExecuteReader", "ExecuteScalar"),
```

(`InlineSqlPattern.CommandTypes` is currently a `private` field on that class — either widen it
to `internal` and reference it directly here, or keep the two lists independently maintained;
either is fine, but note the duplication if kept independent.)

(Widening the sink method list to the other `Execute*` members Dapper/ADO actually expose is a
reasonable companion improvement, since the current row only wires plain `"Execute"` — worth
confirming against `InlineSqlPattern.DapperMethods` for the full set already known to this
codebase.)

## Test plan

- Update `tests/Qlcheck.Tests/Catalog/fixtures.json["sql-injection"]` to use a real
  `SqlCommand`/`DbCommand` receiver for `bad`, and a non-command receiver (e.g. a
  hand-rolled `ICommand`-shaped interface) for `good`.
- Add a regression test reproducing the exact reported shape:
  ```csharp
  interface IUpdateRequisitionCommand { void Execute(int recordId, object model); }
  class C
  {
      public void M(IUpdateRequisitionCommand updateRequisitionCommand, int recordId, object model) =>
          updateRequisitionCommand.Execute(recordId, model);
  }
  ```
  must produce `Empty` findings.
- Keep a true-positive case: `SqlCommand cmd; cmd.CommandText = sql; cmd.Execute(userInput);`
  (or similar, receiver typed as `SqlCommand`) must still fire.

## Confidence

Clean, low-risk — this is a **sink-precision** fix (restrict which receivers count), not a
source-precision or full-dataflow change, so it doesn't touch `TaintMatch.IsTainted`/
`IsParameter`, which are shared by all ~19 taint ids. Only `sql-injection`'s own row changes
behavior.
