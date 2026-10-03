# Fix: `prefer-set-min-max` matches any `Min`/`Max` call, not `Set` receivers

## Reported issue

From `requisitionops/api/QLCHECK_KNOWN_ISSUES.md` (7 findings, confirmed false positives):

> message says "`Min`/`Max` properties of `Set` types should be used instead of `Enumerable`
> extension methods," but every instance is a plain two-argument `Math.Min(a, b)`/
> `Math.Max(a, b)` scalar comparison, not a collection. Confirmed by swapping to
> `int.Min(a, b)` — still flagged, so it's matching on the identifier `Min`/`Max`
> generically, not on actual collection usage.

## Root cause

- Row: `SyntaxEngine.cs:251` — `Row.Invoke("prefer-set-min-max", CheckClass.Syntax, "Min")`.
- `InvokeMatch.Report` (`Match/InvokeMatch.cs:9-23`) matches any invocation named `"Min"` —
  full stop, no receiver-type filter (the row passes no `type` argument).
- Two separate bugs:
  1. **No `"Max"` row at all.** The row only wires `"Min"`; the message mentions both, but
     the implementation never checks `Max` calls.
  2. **No receiver-type check for `"Min"`.** `Math.Min(a, b)`, `int.Min(a, b)`, and any
     user-defined `.Min()` all match identically to `mySet.Min()`.
  3. The existing `Row.Invoke(id, class, method, type)` DSL's optional `type` filter
     (`Names.InvocationType`, `Queries/Names.cs:18-30`) can't fix this even if wired: it reads
     the receiver expression's *spelled syntax text* (`TypeText(member.Expression)`), which for
     an instance call like `mySet.Min()` returns the **variable name** `"mySet"`, not its
     resolved type `HashSet<T>`. It only works for static-style call sites where the type name
     is spelled at the call site (e.g. `DateTime.Now`). This check genuinely needs the semantic
     model to know the receiver's real type.

## Proposed fix

Move this check to `SymbolEngine.cs` (it is now legitimately symbol-based) with a new predicate
that resolves the receiver's type via `ctx.Model` and requires it to actually be (or implement)
a `Set` type:

```csharp
// SymbolEngine.cs
Row.Handle("prefer-set-min-max", CheckClass.Symbol, Patterns.SetMinMax),
```

```csharp
// Patterns.cs (or a new SymbolPatterns.cs, matching the "Symbol" classification)
private static readonly HashSet<string> MinMaxMethods = new(StringComparer.Ordinal) { "Min", "Max" };

private static readonly HashSet<string> SetTypeNames = new(StringComparer.Ordinal)
{
    "HashSet", "SortedSet", "ISet", "IReadOnlySet",
};

public static void SetMinMax(WalkContext ctx, string id)
{
    foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
    {
        if (node is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax member
            || !MinMaxMethods.Contains(member.Name.Identifier.Text)
            || invocation.ArgumentList.Arguments.Count != 0)
        {
            continue;
        }

        var receiverType = Symbols.TypeOf(ctx, member.Expression);
        if (IsSetType(receiverType))
        {
            ctx.Report(id, invocation);
        }
    }
}

private static bool IsSetType(ITypeSymbol? type)
{
    for (var current = type; current is not null; current = current.BaseType)
    {
        if (SetTypeNames.Contains(current.Name))
        {
            return true;
        }
    }

    return type is not null && type.AllInterfaces.Any(i => SetTypeNames.Contains(i.Name));
}
```

`Symbols.TypeOf` (`Queries/Symbols.cs:12-16`, wraps `ctx.Model.GetTypeInfo`) and
`Symbols.Implements` already exist and are used by at least one other row (`Patterns.PrivateUnused`
/`Symbols.IsDisposable`), so this isn't introducing a brand-new capability to the engine — just
a second real consumer of it.

This also fixes the `"Max"` gap in one pass, since `MinMaxMethods` checks both names.

## Test plan

- Update `tests/Qlcheck.Tests/Catalog/fixtures.json["prefer-set-min-max"]`:
  - `bad`: `var s = new HashSet<int> { 1, 2 }; var m = s.Min();`
  - `good`: `var list = new List<int> { 1, 2 }; var m = list.Min();` (a non-Set receiver,
    which must not fire — `List<T>` has no faster `Min`/`Max` alternative, so LINQ's
    `Enumerable.Min()` is the correct call there).
- Add unit tests (new `SymbolMatchTests.cs`-style, or alongside `PatternsTests.cs`) for:
  - `Math.Min(a, b)` — must not fire (regression test for the exact reported false positive).
  - `int.Min(a, b)` — must not fire.
  - `mySortedSet.Max()` — must fire (confirms the new `"Max"` coverage).
  - `myList.Min()` (`List<int>`) — must not fire.

## Confidence

Needs `ctx.Model`, which is used sparingly elsewhere in the catalog (mainly `SymbolEngine.cs`
rows), so this is a real but narrow and well-precedented extension — not a pure syntax tweak,
but not new infrastructure either. Main judgment call: whether to treat a generic type
parameter constrained to `ISet<T>` as a match — recommend keeping it conservative (concrete
`HashSet<T>`/`SortedSet<T>`/`ISet<T>`/`IReadOnlySet<T>`-assignable types only) to avoid new false
positives on unconstrained generics.
