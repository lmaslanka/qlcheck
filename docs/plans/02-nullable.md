# Plan 02: surface the compiler's nullable-reference warnings

Status: Agreed 2026-10-29 · Depends on: 00

Probes and raw output (outside the repo):
`/tmp/claude-1000/-home-lmaslanka-projects-qlcheck/1fadbd8a-84fa-480a-a869-298258cc64bb/scratchpad/probes-nullable/`.
- `probe/` is a per-project diagnostics census.
- `probe2/` covers semantics cases.
- `probe3/` covers timing.

## Goal

Report the compiler's nullable-reference warnings as Findings from the `CSharpCompilation` qlcheck already builds.
The ids are CS8602, CS8605, CS8670, CS8604, CS8603, CS8600, CS8601, CS8625 and CS8618. Findings should match what
`dotnet build` warns about. Nothing should be reported that could only be wrong because the compilation is incomplete,
which is ADR 0003's rule.

Two further changes:
- `null-dereference`'s text heuristic is replaced by the compiler's CS8602.
- This plan also revives `validate-public-arguments`, the semantic null-validation Check for public APIs in code where
  nullable annotations are off.

Acceptance:
- In a `<Nullable>enable</Nullable>` project, `string? s = Environment.GetEnvironmentVariable("X"); return s.Length;`
  gives a `null-dereference` Finding.
- With Plan 00 landed, `./run .` on this repo gives **zero** nullable Findings. The probe confirms this: the real build
  has no nullable warnings, and the census with global usings plus the csproj's Nullable value found none.

## Current state

**Prerequisite (Plan 00).** Today every compilation runs with nullable disabled, and the csproj's `<Nullable>` is
never read (`src/Qlcheck/Languages/CSharp/CSharpCompilations.cs:40-52`). Implicit global usings are also missing, so
76 of 89 src trees and 51 of 51 test trees have unresolved-name errors. Plan 00 fixes both: it reads
`Nullable`/`OutputType`/TFM, adds the global usings as a context tree, and introduces an explicit reportable-tree set
on `CompilationContext`. This plan assumes all of that.

**Behaviour verified by probes:**
- `#nullable enable`/`disable` directives override the project setting per file, as expected.
- In a disabled context the task probe yields only CS8632, the warning for a `?` annotation outside a nullable context.
  That is why nothing is reported today.
- Error types never produce nullable warnings. `Missing m = ...; m.Name` gives only CS0246/CS1061.
- `#pragma warning disable CS86xx` removes the diagnostic from `GetDiagnostics()` entirely.
- A partial class whose constructor lives in an *unloaded* part gives CS8618 **with no error in the tree**. That is a
  false positive, and the tree filter cannot catch it.
- With an explicit constructor, CS8618 is reported **once per uninitialized member at the constructor's identifier**.
  Two fields therefore give two identical `{check}:{file}:{line}:{column}` IDs (`Finding.cs:14-33`). Nothing in
  `CheckRun`/`Report` dedupes them.
- There is no public Roslyn tag that marks nullable diagnostics: `CustomTags` is just `Compiler,Telemetry`. The id set
  has to be curated.

**Diagnostics reader.**
- `Catalog/UnusedUsingPattern.cs:8-13,16-49`: one `Compilation.GetDiagnostics()`, and trees with
  {CS0103, CS0246, CS1061} are skipped.
- `Catalog/CompilationEngine.cs:5-18`: two rows.

**Rows that touch nullability:**
- `null-dereference`: `FlowEngine.cs:20` → `Patterns.NullDeref` (`Patterns.cs:451`) → `IsNullDeref`/`NullName`
  (`Patterns.cs:934-964`). It fires when an `if (x == null)` block's **text** contains `"x."`.
  - False positives: `x = new(); x.Foo`, `myx.`, and text inside string literals.
  - Fixture: `tests/Qlcheck.Tests/Catalog/fixtures.json:1632`.
- `no-empty-nullable-access` (`SymbolEngine.cs:97`, `SymbolPatterns.cs:865`): works in any context. The compiler's
  CS8629 would duplicate it in enabled code.
- `null-forgiving-needs-nullable` (`SyntaxEngine.cs:203`) and `redundant-null-forgiving` (`SyntaxEngine.cs:241`):
  placeholder `Row.Invoke(..., "value")` rows that never fire.
- `validate-public-arguments` (`SyntaxEngine.cs:286`): a placeholder `Row.Invoke(..., "ArgumentNullException")` that
  matches a *call* named `ArgumentNullException`. Its message is "Arguments of public methods should be validated
  against null." (`checks.json:2003`) and its fixture is `fixtures.json:2462`. The placeholder triage marked it DEAD
  and moved it here because it overlaps `null-dereference`.

**Cost.**
- `GetDiagnostics()` is cached per compilation. The first call is 55–160 ms warm on src/Qlcheck; repeat calls are
  7–29 ms. The nullable rows therefore add almost nothing on top of `unused-using`.
- The nullable-binding cost itself is paid by Plan 00 when it turns on `<Nullable>`.

**`--uncommitted`.** `QlcheckApp.cs:33,88-104` still compiles every loaded file and narrows only what is reported.
Nullable Findings therefore have full context, but a warning in an unchanged file B caused by an edit to A is filtered
out.

**Tests.**
- Per-pattern unit tests in the style of `tests/Qlcheck.Tests/Catalog/UnusedUsingPatternTests.cs`.
- A shared fixture runner, `CatalogTests.cs:124-149` and `:424-441`, which sends Compilation-class rows through
  `CompilationEngine.Apply` on an adhoc compilation. After Plan 00 that compilation has Nullable=Enable, so fixtures
  need no `#nullable` header.
- An enabled-count pin, `CatalogTests.cs:75-79`.

## Design

### 1. Shared diagnostics

`CompilationContext` gets two lazy members:
- `Diagnostics`: one `GetDiagnostics()` call per compilation.
- `ErrorTrees`: the trees that contain **any** error-severity diagnostic (D3).

`UnusedUsingPattern` moves onto `Diagnostics` and keeps its own narrower unresolved-id filter.

### 2. NullablePattern

`NullablePattern.Apply(ctx, id, compilerIds)` is shared by all nullable rows. It keeps a diagnostic when all of these
hold:
- it is a Warning with an id in the row's set;
- it is in a reportable tree (Plan 00);
- it is not in `ErrorTrees`.

It then drops:
- CS8618 whose containing type is `partial` (D6). The type comes from the semantic model at the location.
- Anything in a project whose TFMs are all non-annotated (netstandard\*, net4x, netcoreapp<3). The compilation binds
  against qlcheck's net10 references, so those warnings would not match `dotnet build`. The TFM comes from Plan 00's
  `ProjectSettings`.
- Duplicate Finding IDs. Collapsed CS8618 members are folded into one message (see 4).

Projects or regions with nullable warnings off produce nothing. The compiler doesn't warn there, and qlcheck does not
force it to (D5).

### 3. Rows

The rows go in `CompilationEngine` with `CheckClass.Compilation`, one per fix action (D1). All are default-on (D11).

| id | compiler ids | checks.json message |
|---|---|---|
| `null-dereference` (existing id, reimplemented) | CS8602, CS8605, CS8670 | Possibly null values should not be dereferenced; check for null or make the source non-nullable instead of adding `!`. |
| `nullable-argument` | CS8604 | Possibly null values should not be passed to non-nullable parameters. |
| `nullable-return` | CS8603 | Methods with a non-nullable return type should not return a possibly null value. |
| `nullable-assignment` | CS8600, CS8601, CS8625 | Null or possibly null values should not be assigned or converted to non-nullable types. |
| `non-nullable-uninitialized` | CS8618 | Non-nullable fields and properties should be set before the constructor exits; initialize them, mark them `required`, or make them nullable. |

Excluded:
- CS8629, so there are no duplicates with `no-empty-nullable-access` (D8).
- The annotation-mismatch family CS8619–CS8622, CS8631–CS8634, CS8714, CS8762–CS8769, CS8774, CS8777 (D9).

### 4. Finding message

The Finding message is the checks.json sentence followed by the compiler's id and text, through `ReportCustom` (D10).
For example: `… (CS8604: Possible null reference argument for parameter 'path' in 'string File.ReadAllText(string path)'.)`.

For a collapsed CS8618 the compiler texts are joined, so every member name survives:
`(CS8618: … field 'a' …; … field 'b' …)`.

### 5. Retire the text heuristic

Delete `FlowEngine.cs:20`, `Patterns.NullDeref`, `IsNullDeref` and `NullName` (`Patterns.cs:451,934-964`).

The id keeps working in `.qlcheck_disable` and suppression comments. It moves from the Flow class to the Compilation
class. Consequence of D5: in nullable-off code, `null-dereference` no longer fires at all. For public APIs,
`validate-public-arguments` (6) is the replacement there.

### 6. `validate-public-arguments`, revived

This is a per-file Flow row (`FlowEngine`, `Row.Handle`), replacing the placeholder at `SyntaxEngine.cs:286`. If
Plan 01 already removed that row, this plan re-adds it. The message is unchanged.

**Scope.** It looks at methods, constructors, indexers and operators that are visible outside the assembly. That means
`public`/`protected` members of `public` types, nested types included. It does not look at:
- overrides and explicit interface implementations whose base declares the parameter (the base owns the contract);
- `[Fact]`/`[Test]`-style test methods.

**Parameters.**
- In scope: reference-type parameters, including `this` of an extension method, that are not `out` and not `params`.
- Only parameters declared where nullable **annotations are off**:
  `SemanticModel.GetNullableContext(param.SpanStart)` without `AnnotationsEnabled`.
- In annotated code the compiler and the rows above own this, so there is no overlap with `null-dereference`.

**Dereference.** The parameter is the receiver of a member access, an element access, a `foreach` expression, a `lock`
expression, or the receiver of an extension-method call that reaches an instance member. These do *not* count:
`?.`, `??`, passing the parameter on as an argument, and `nameof`.

**Validated.** The *first* dereference in source order must be preceded, within the same body, by at least one of:
- a null check of that parameter (`== null`, `is null`, `is not null`, `ReferenceEquals(p, null)`, `p ?? throw`,
  `ArgumentNullException.ThrowIfNull(p)`, `throw new ArgumentNullException(nameof(p))`);
- passing it to a method whose parameter carries `[NotNull]`/`[ValidatedNotNull]`.

This is a source-order approximation, so a check in a sibling branch also counts. It errs toward silence.

**Report** at the first unvalidated dereference.

**Skipped:**
- trees in `ErrorTrees`-equivalent state, meaning the per-file model has error diagnostics (ADR 0003);
- parameters with a `= null` default, because those are deliberately optional.

## Implementation steps (test-first, one commit each)

Plan 00 must be merged first.

1. **Shared diagnostics.**
   - Red: a `CompilationContext` test that `ErrorTrees` contains a tree with CS0246, and also one with CS8795.
   - Green: lazy `Diagnostics`/`ErrorTrees`, and port `UnusedUsingPattern`. Its existing tests stay green.
2. **NullablePattern + four new rows.**
   - Red: `tests/Qlcheck.Tests/Catalog/NullablePatternTests.cs`. A temp csproj is used where project settings matter.
     Cases:
     - the task probe gives `null-dereference`;
     - each compiler id goes to its row;
     - `#nullable disable` gives nothing;
     - a `<Nullable>disable</Nullable>` project gives nothing;
     - a tree with CS0246 gives nothing;
     - an error-type receiver gives nothing;
     - `#pragma` gives nothing;
     - a partial-type CS8618 gives nothing;
     - two fields plus a constructor give **one** Finding whose message names both;
     - a context tree gives nothing;
     - a `netstandard2.0` project gives nothing;
     - the message has the compiler suffix.
   - Add bad/good fixtures to `fixtures.json`.
   - Green: the pattern, the rows, the checks.json entries, and the count bump in `CatalogTests.cs:75-79`.
3. **Reimplement `null-dereference`.**
   - Red: update its fixture. Add the old heuristic's false positive (`if (x == null) { x = new(); return x.Length; }`
     gives nothing) and an outside-`if` case (`s?.Trim(); s.Length` gives a Finding).
   - Green: move the row to `CompilationEngine`, delete the `Patterns.cs` code, and update the checks.json message.
4. **Revive `validate-public-arguments`.**
   - Red: `tests/Qlcheck.Tests/Catalog/ValidatePublicArgumentsTests.cs` plus a `fixtures.json` entry replacing the
     placeholder one at `:2462`. Should give a Finding:
     - `public int Len(string s) => s.Length;` in disabled code;
     - a deref inside `foreach (var x in items)`;
     - extension `this`.
   - Should give nothing:
     - `ThrowIfNull` first;
     - `if (s == null) throw …` first;
     - `s ?? throw`;
     - `s?.Length`;
     - an internal method;
     - a private type;
     - an override;
     - a `= null` default;
     - annotations enabled (`#nullable enable`);
     - a tree with errors.
   - Green: the `FlowEngine` row and a `PublicArgumentPattern.cs` matcher. Remove the `SyntaxEngine.cs:286`
     placeholder.
5. **Self-scan and docs.**
   - Self-scan: `./run .` must show 0 new nullable Findings. Report the `validate-public-arguments` count; it is
     expected to be 0, since this repo is nullable-enabled throughout.
   - README: check counts and class table (Flow −1 then +1 for `validate-public-arguments`'s move from Syntax;
     Compilation +5; Syntax −1; total 509→513), a sample row, and a `--uncommitted` note about cross-file effects.
   - CONTEXT.md: add a **Nullable finding** entry, "reported only where the compiler would warn and the file has no
     compile errors".
6. **Follow-up note.** Record the two null-forgiving placeholder rows as a follow-up plan (D13). They are not
   implemented here.

## Files touched

- `src/Qlcheck/Languages/CSharp/Catalog/CompilationContext.cs`, `CompilationEngine.cs`, `UnusedUsingPattern.cs`
- `src/Qlcheck/Languages/CSharp/Catalog/NullablePattern.cs` (new), `PublicArgumentPattern.cs` (new)
- `src/Qlcheck/Languages/CSharp/Catalog/FlowEngine.cs`, `SyntaxEngine.cs`, `Patterns.cs` (deletions and moves)
- `src/Qlcheck/Languages/CSharp/Catalog/checks.json`
- `tests/Qlcheck.Tests/Catalog/NullablePatternTests.cs` (new), `ValidatePublicArgumentsTests.cs` (new),
  `UnusedUsingPatternTests.cs`, `CatalogTests.cs`, `fixtures.json`
- `README.md`, `CONTEXT.md`

## ADR needed?

**No.** The project-settings work, which is the hard-to-reverse and surprising part, is ADR 0011 in Plan 00.
- Reading compiler warnings and skipping files with errors follows directly from ADR 0003.
- D5's "no", which respects nullable-off projects, removed the one surprising trade-off this plan had.

## Risks

- **Coverage loss in nullable-off code.** Retiring the text heuristic means `if (x == null) { x.… }` is no longer
  reported where nullable warnings are off. `validate-public-arguments` covers public parameters only. Accepted with D5.
- **`validate-public-arguments` noise.** On large nullable-off public APIs it behaves like CA1062, which is noisy.
  - The source-order "validated" rule errs toward silence: a check anywhere earlier counts.
  - It is default-on, as the placeholder was. Teams can disable it with `.qlcheck_disable`.
- **Partial scans.** Missing partial parts are handled by D6. Other missing context shows up as compile errors, so the
  tree is skipped.
- **Stale ProjectReference DLLs** with different annotations could produce warnings that `dotnet build` wouldn't. This
  is rare and accepted.
- **Messy projects** with hundreds of real nullable warnings get hundreds of Findings. That is accurate, but large.
- **`--uncommitted`** hides cross-file effects (D12). This gets documented.
- **Plan 00 misreads `<Nullable>`.** A wrong Enable gives false positives, and a wrong Disable gives silence. See
  Plan 00's risks.

## Effort

About 2 days:

| Steps | Effort |
|---|---|
| 1 | ¼ day |
| 2 | ½ day |
| 3 | ¼ day |
| 4 (`validate-public-arguments`) | ¾ day |
| 5 (docs) | ¼ day |

## Decisions (resolved)

- **D1. Check ids:** one per fix action: `null-dereference` (existing id, reimplemented), `nullable-argument`,
  `nullable-return`, `nullable-assignment` and `non-nullable-uninitialized`.
- **D2. Files outside any csproj:** the adhoc compilation uses Nullable=Enable. Plan 00 implements it.
- **D3. Silencing:** any error-severity diagnostic in a tree silences that tree's nullable Findings.
- **D4. Global usings, `<Nullable>`, `<OutputType>`:** moved to **Plan 00**, which also accepts the +118 Findings from
  other Checks.
- **D5. Nullable-off projects and regions:** **No.** Respect them: no nullable Findings where the compiler wouldn't
  warn, and warnings are never forced on.
- **D6. CS8618 on `partial` types:** always skipped.
- **D7. `<NoWarn>` and `.editorconfig` severities:** ignored. `.qlcheck_disable` is the visible policy mechanism.
  `#pragma warning disable` is honoured, because the compiler drops those diagnostics.
- **D8. CS8629:** excluded. `no-empty-nullable-access` is left as it is.
- **D9. Annotation-mismatch warnings:** left out of v1.
- **D10. Finding message:** the checks.json sentence plus the compiler's id and text.
- **D11. New ids:** default-on.
- **D12. `--uncommitted`:** keeps its contract of reporting only changed files. The cross-file blind spot is documented.
- **D13. `null-forgiving-needs-nullable` / `redundant-null-forgiving`:** a separate follow-up plan, not this one.
- **D14. `validate-public-arguments`:** owned by this plan, moved from the placeholder plan's api-design theme. It is
  revived as a semantic, default-on Flow row for externally visible members, in nullable-annotations-off code only, as
  specified in Design 6.

## Follow-ups (not in this plan)

- **`null-forgiving-needs-nullable` and `redundant-null-forgiving`** (D13). Plan 01 removed both placeholder rows. A
  follow-up plan should revive them on the semantic model: a `!` in a nullable-disabled context (where it has no
  effect), and a `!` on an expression whose flow state is already not-null
  (`SemanticModel.GetTypeInfo(expr).Nullability.FlowState == NotNull` before the operator).
