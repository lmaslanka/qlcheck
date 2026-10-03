# Fix: `datetime-kind` flags the fix itself instead of the problem

## Reported issue

From `requisitionops/api/QLCHECK_KNOWN_ISSUES.md` (3 findings, confirmed false positives):

> all three point directly at `DateTime.SpecifyKind(value, DateTimeKind.Utc)` calls — i.e.
> the code doing exactly what the rule asks for. Confirmed via column offsets.

## Root cause

- Row: `SyntaxEngine.cs:52` — `Row.Invoke("datetime-kind", CheckClass.Syntax, "SpecifyKind")`.
- `InvokeMatch.Report` (`Match/InvokeMatch.cs:9-23`) flags every `InvocationExpressionSyntax`
  whose invoked method's simple name (`Names.Invocation`) equals `"SpecifyKind"` — no receiver
  type filter (the row passes no `type` argument).
- Message (`checks.json`): "Always set the `DateTimeKind` when creating new `DateTime`
  instances."
- **The row is inverted**: it matches calls to the fix (`DateTime.SpecifyKind(...)`) instead of
  the problem (`DateTime` construction with no kind specified). This is confirmed by the
  fixture itself — `fixtures.json["datetime-kind"].bad` is literally `SpecifyKind();`, a bare
  call unrelated to any `DateTime` at all, meaning the fixture was written to match the
  row's (wrong) implementation rather than the check's stated intent.

## Proposed fix

Retarget the row to flag `DateTime` construction that has no `DateTimeKind` argument, matching
the message's literal wording ("when *creating* new `DateTime` instances"). This needs a new
predicate — the generic `Row.Invoke` DSL only matches invocations by name, not
`ObjectCreationExpressionSyntax` shape with an argument-list condition:

```csharp
// SyntaxEngine.cs
Row.Handle("datetime-kind", CheckClass.Syntax, Patterns.DateTimeMissingKind),

// Patterns.cs
private const string DateTimeTypeName = "DateTime";

public static void DateTimeMissingKind(WalkContext ctx, string id)
{
    foreach (var node in ctx.Nodes(SyntaxKind.ObjectCreationExpression))
    {
        if (node is ObjectCreationExpressionSyntax creation
            && Names.Creation(creation) == DateTimeTypeName
            && !HasDateTimeKindArgument(creation))
        {
            ctx.Report(id, creation);
        }
    }
}

private static bool HasDateTimeKindArgument(ObjectCreationExpressionSyntax creation) =>
    creation.ArgumentList?.Arguments.Any(argument =>
        argument.Expression is MemberAccessExpressionSyntax member
        && member.Expression is IdentifierNameSyntax type
        && type.Identifier.Text == "DateTimeKind") ?? false;
```

Note: don't fold in `DateTime.Now`/`DateTime.UtcNow` — that shape is already covered by the
existing, separate `no-datetime-now-timing` row (`Row.Invoke("no-datetime-now-timing",
CheckClass.Syntax, NowProperty, DateTimeType)`); doing so here would make `datetime-kind` a
near-duplicate of that check. `datetime-kind`'s own scope, per its message, is specifically
`new DateTime(...)` construction.

## Test plan

- Update `tests/Qlcheck.Tests/Catalog/fixtures.json["datetime-kind"]`:
  - `bad`: `new DateTime(2024, 1, 1);` (no `DateTimeKind` argument).
  - `good`: `new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);`.
- Add a unit test confirming `DateTime.SpecifyKind(value, DateTimeKind.Utc)` alone (the
  previously-flagged false-positive shape) produces `Empty` findings — this directly locks in
  the fix for the reported issue.
- Add a unit test confirming `new DateTime(2024, 1, 1)` (no kind) still fires — the actual
  true-positive case the check is meant to catch, which the current implementation has never
  actually tested.

## Confidence

Clean, minimal, syntax-only fix (no semantic model needed — `Names.Creation` is already a
syntactic type-name lookup). The only judgment call is not overlapping with
`no-datetime-now-timing`, addressed above by scoping strictly to `new DateTime(...)`.
