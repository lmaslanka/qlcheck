# Plan 07: redundant null checks on never-null results

Status: Agreed 2026-10-29 · Depends on: 02 (nullable) · Scheduled after 06

## Goal

Developers and AI agents often call a method that always returns an object or an empty collection, then check the
result for null anyway: `var items = repo.GetAll(); if (items == null) return;`. The check is dead code, and it
misleads the next reader into thinking the method can return null. A new Check, `redundant-null-check`, reports it.

The decisive term is a **Never-null result** (`CONTEXT.md`): the result of a method that cannot return null, because
the method is annotated to return a non-nullable value, or because it has no annotations, cannot be overridden, and
only returns new objects, empty collections or other never-null results (or is an iterator).

Acceptance:
- `List<int> Get() => new(); ... if (Get() == null) { }` gives one `redundant-null-check` Finding whose message names
  `Get` and why it cannot return null.
- A method annotated `List<int>? Get()` gives nothing, whatever its body returns.
- A call through an interface or virtual method to an unannotated callee gives nothing.

## Current state

- **Producer side exists:** `return-empty-not-null` (`SyntaxEngine`, `QuietPatterns.ReturnsNullArray`) tells a method
  to return an empty collection instead of null. Nothing covers the consumer side.
- **Related, not overlapping:** `redundant-nullable-compare` is about `Nullable<T>` (`x.HasValue == true`);
  `no-null-and-is` about `x != null && x is T`; plan 02's nullable rows report *missing* null checks, this plan
  reports *superfluous* ones.
- **From plan 02:** compilations honour each csproj's `<Nullable>`, the adhoc compilation enables nullable, and
  `CompilationContext` shares diagnostics and error trees. A per-file row reads errors from `ctx.Model`.
- **Roslyn facts the design relies on:**
  - In annotated code, `SemanticModel.GetTypeInfo(expr).Nullability.FlowState` is `NotNull` when the compiler knows a
    value is not null, including through locals.
  - A method symbol carries its return annotation even when the *calling* file has nullable off:
    `IMethodSymbol.ReturnNullableAnnotation` is `NotAnnotated`, `Annotated` or `None` (oblivious). The BCL and many
    packages are annotated, so `ToList`, `Split` or `Directory.GetFiles` need no hand-written list.
  - `[return: MaybeNull]` and `[return: NotNullIfNotNull(...)]` weaken a non-nullable annotation.

## Design

### 1. Never-null results (`Queries/NeverNull.cs`)

`NeverNull.Reason(model, invocation)` returns why a call's result cannot be null, or null when it might be. The
invocation may be wrapped in `await` (the awaited value of `Task<T>`/`ValueTask<T>`).

In order:
1. **Annotated callee.** The return type (for awaited calls, the task's `T`) is annotated non-nullable and carries no
   `[MaybeNull]`/`[NotNullIfNotNull]`: never-null, reason *declared*. A type-parameter return counts only through
   flow state (rule 3), because the type argument may be nullable.
2. **Annotated as nullable.** `Annotated` (`?`): **not** never-null, whatever the body does (D1). Stop.
3. **Annotated caller.** In nullable-enabled caller code, a `NotNull` flow state on the call is never-null, reason
   *declared*. This covers generic returns resolved to non-nullable types.
4. **Oblivious callee with source.** Only when the callee has no annotations (`None`), is in this compilation, and
   cannot be overridden: static, private, sealed, non-virtual instance, or a local function (D2). Then its body is
   analysed:
   - an **iterator** (`yield`) is never-null, reason *iterator*;
   - otherwise every `return` (or the expression body) must be never-null, reason *returns new or empty*.

   Never-null return expressions: `new ...` (incl. target-typed and anonymous), collection expressions `[]`,
   `Array.Empty`, `Enumerable.Empty`, `string.Empty`, literals other than `null`/`default`, interpolated strings,
   `this`, another never-null result (recursively, at most 3 calls deep, with a cycle guard), `x ?? <never-null>`,
   `c ? <never-null> : <never-null>`, and a local whose every assignment in the method is never-null. A `throw` path
   is fine; a `null` or `default` return disqualifies the method. Results are cached per method symbol.

### 2. Check sites (`RedundantNullCheckPattern.cs`)

The checked value must be a never-null result used in one of three ways (D3):
- directly: `if (Get() == null)`, `Get()?.Count`, `await GetAsync() ?? []`;
- through a local initialised from the call (incl. `await`) that is not assigned, or passed by `ref`/`out`, between
  its declaration and the check in source order. In annotated callers the compiler's flow state confirms this too.

Counted shapes (D4): `x == null`, `x != null`, `null == x`, `x is null`, `x is not null`, `x?.` and `x ?? ...`
(including `?? throw`). Not counted: `is { }`, declaration and type patterns (D5), `ArgumentNullException.ThrowIfNull`,
`Debug.Assert`, `string.IsNullOrEmpty`/`IsNullOrWhiteSpace`, `ReferenceEquals`.

Not reported:
- a file with compile errors (ADR 0003);
- parameters, fields and properties (out of scope by D3; they can be null at run time despite annotations).

The Finding points at the null-check expression (the comparison, the pattern, the `?.` or the `??`). One Finding per
check site.

### 3. Row and message

- `Row.Handle("redundant-null-check", CheckClass.Symbol, RedundantNullCheckPattern.Apply)` in `SymbolEngine`.
  Default-on, test files included (D7).
- `checks.json`: "Results of methods that never return null should not be checked for null."
- No replacement (D6). The message adds one detail naming the method (D8):
  - *declared*: "`GetItems` is declared to return a non-nullable `List<Item>`."
  - *returns new or empty*: "`GetItems` returns a new or empty value on every path."
  - *iterator*: "`ReadLines` is an iterator, which never returns null."

## Implementation steps (test-first, one commit each)

Plan 02 must be merged first.

1. **`NeverNull`.**
   - Red, `tests/Qlcheck.Tests/Catalog/Queries/NeverNullTests.cs`:
     - annotated non-nullable return gives *declared*; `?` gives null even when the body returns `new()`;
     - `[return: MaybeNull]` gives null;
     - oblivious static/private/sealed/local-function callee returning `new`, `[]`, `Array.Empty`, `string.Empty`, a
       literal, an interpolated string, `this`, `x ?? new()`, `c ? new A() : new B()`, a never-null local, or another
       never-null method gives *returns new or empty*;
     - a `null`, `default` or possibly-null return disqualifies it;
     - virtual, abstract, interface and override callees give null;
     - an iterator gives *iterator*;
     - `await` of `Task<List<T>>` follows `T`;
     - mutual recursion terminates; the depth limit gives null beyond 3.
   - Green: the query.
2. **`RedundantNullCheckPattern`.**
   - Red, `tests/Qlcheck.Tests/Catalog/RedundantNullCheckPatternTests.cs`: each counted shape gives one Finding; each
     excluded shape gives none; direct use, a single-assignment local and `await` are found; a reassigned local, a
     `ref`/`out` local, a parameter, a field and a property give nothing; a file with compile errors gives nothing;
     the detail names the method and reason; there is no replacement.
   - Green: the matcher.
3. **Row, catalog and fixtures.** Add the row and `checks.json` entry; a compiling bad/good fixture (G1, G3); bump
   `CatalogCount`, `EnabledCatalogCount` and the `--stats` pin by one.
4. **Self-scan and docs.** Run `./run .` and record the count. Fix real Findings in qlcheck's own code in the same
   commit, or explain each one that stays. README: counts, Symbol class +1, a sample row.

## Files touched

- New: `src/Qlcheck/Languages/CSharp/Catalog/Queries/NeverNull.cs`,
  `src/Qlcheck/Languages/CSharp/Catalog/RedundantNullCheckPattern.cs`,
  `tests/Qlcheck.Tests/Catalog/Queries/NeverNullTests.cs`,
  `tests/Qlcheck.Tests/Catalog/RedundantNullCheckPatternTests.cs`
- Changed: `SymbolEngine.cs`, `checks.json`, `tests/Qlcheck.Tests/Catalog/fixtures.json`, `CatalogTests.cs`,
  `QlcheckAppTests.cs`, `README.md`

## ADR needed?

No. It is one Check built from rows and queries (ADR 0006/0007), easy to remove or narrow, and it follows ADR 0003 on
files with errors. The one surprising rule, that an explicit `?` wins over the body, is recorded here and in the
glossary.

## Risks

- **Wrong third-party annotations.** A package that declares a non-nullable return but returns null makes the Finding
  wrong. Accepted: the annotation is the package's contract, and the message names the method so the agent can check.
- **Defensive checks across trust boundaries.** Code that guards against a misbehaving plugin or reflection-created
  object will be flagged. Teams can suppress the line or disable the Check.
- **Body analysis cost.** Bounded by the depth limit and the per-symbol cache; callees in other files need their
  tree's semantic model.
- **Source-order local tracking** in nullable-off code is an approximation: an assignment in an earlier sibling branch
  makes the local ineligible, which errs toward silence.
- **Self-scan noise.** qlcheck's own code may contain defensive `?? []` on annotated results; step 4 decides each.

## Effort

About 2 days: `NeverNull` 1 day, the matcher ½ day, row, fixtures, self-scan and docs ½ day.

## Decisions (resolved)

- **D1. Annotation wins.** A callee annotated as returning a nullable value never has a never-null result, even if
  its body never returns null today. Body analysis applies only to unannotated callees.
- **D2. Body analysis only for callees that cannot be overridden** (static, private, sealed, non-virtual, local
  functions). Interface, abstract and virtual calls are skipped, since another implementation may return null.
- **D3. Scope:** method results only, used directly, through a single-assignment local, or after `await`. Not
  parameters, fields or properties.
- **D4. Shapes:** `== null`, `!= null`, `is null`, `is not null`, `?.` and `??`. Not `ThrowIfNull`, `Debug.Assert` or
  `IsNullOrEmpty`.
- **D5. Patterns:** only null constants; `is { }`, declaration and type patterns are not counted.
- **D6. Fix:** message only, no replacement.
- **D7. Default-on everywhere,** test files included.
- **D8. Message detail** names the method and why it cannot return null.
- **D9. Never-null sources:** compiler flow state in annotated callers; the callee's declared annotation in
  nullable-off callers (replacing a hand-curated framework list); body analysis for unannotated, non-overridable
  callees, composed through other never-null results, `??` and `?:`, a few calls deep; iterators always count.
- **D10. Id and term:** `redundant-null-check`; glossary term **Never-null result**.
- **D11. Order:** plan 07, after plan 06.
