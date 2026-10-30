# Plan 01: honest catalog - remove placeholder Checks

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Appendices:
- [`01-placeholder-triage.md`](01-placeholder-triage.md): one line per name-only row, with verdict,
  size, action and evidence.
- [`01-placeholders/`](01-placeholders/): 12 theme files, one revival plan per removed Check.

## Goal

Every Check qlcheck reports as run should be able to produce the Finding its message describes,
and only that Finding. Today, 183 of the 198 rows built from a bare name matcher fail this:
- 132 can never fire on compiling code (DEAD).
- 51 fire whether or not the stated condition holds, often on the compliant code itself (NOISY).

**Agreed:**
- Remove 176 rows now: the DEAD and NOISY rows, minus the 8 owned by other plans, plus
  `no-suppress-finalize`.
- Plan each removed Check's real implementation separately.
- Keep the other 14 WORKS rows.
- Add guards so a placeholder can't come back.

## Current state

### Matchers

- `Row.Invoke` (`src/Qlcheck/Languages/CSharp/Catalog/Row.cs:24`) calls `InvokeMatch.Report`
  (`Match/InvokeMatch.cs:9`).
  - It reports every `InvocationExpression` whose simple name equals a string.
  - The optional "type" compares the receiver's *text*, not its type
    (`Queries/Names.cs:20` `InvocationType` -> `TypeText`).
  - So `Row.Invoke(.., "Count", "Enumerable")` matches only the static `Enumerable.Count(xs)` form,
    never `xs.Count()`. `("Next", "Random")` matches nothing real.
- The other name-only builders have the same weakness: `Row.Attribute` (`Match/AttributeMatch.cs`),
  `Row.Ident` (`Match/IdentMatch.cs`, which matches any identifier token anywhere), `Row.Trivia`,
  and `Row.Kinds`.

### Rows

- 198 name-only rows in total:
  - `SyntaxEngine.cs`: 101 (lines 26-286).
  - `SymbolEngine.cs`: 94 (lines 16-199).
  - `FlowEngine.cs`: 3 (lines 13-22).
- By builder: `Row.Invoke` 185, `Row.Ident` 6, `Row.Kinds` 3, `Row.Attribute` 2, `Row.Trivia` 2.
- No catalog row uses `Row.Invokes`, `Create`, `Creates`, `Member`, `Members` or `Token`. Only
  `tests/Qlcheck.Tests/Catalog/RowTests.cs` uses them.

### Why the tests pass anyway

- `CatalogTests.Each_implemented_check_has_a_failing_and_clean_fixture`
  (`tests/Qlcheck.Tests/Catalog/CatalogTests.cs:125`) only checks that `bad` fires and `good` does not.
- The fixtures were written to match the matcher, not the message. For example, the bad fixture for
  `obsolete-needs-reason` is `Obsolete();`.
- Measured before Plan 00:
  - 177 of the 185 `Row.Invoke` bad fixtures do not compile (CS0103).
  - Across all 501 fixtures, 272 ids have a `bad` or `good` that does not compile.
  - 204 `good` fixtures are the same `return;` template.

### Evidence

- I ran 47 realistic probe files.
- I also ran all 198 name-only Checks on qlcheck's own `src/` and `tests/`. That gave 18 Findings,
  all false positives:
  - `escape-field-keyword`: 13
  - `indexof-not-positive`: 2
  - `escape-extension-keyword`: 2
  - `no-delegate-subtraction`: 1
- Per-row evidence is in the triage appendix.

### Removal mechanics

- Removing a Check deletes both its row and its id in `checks.json`.
- The id then becomes `Unknown check:` everywhere (`Run/CheckDiscovery.cs:54`, `:69`):
  - `--check <id>` exits 2.
  - A `.qlcheck_disable` line naming the id exits 2.
- That is different from the unimplemented state used for `architecture-*` (ADR 0006/0007): there
  the id stays in `checks.json` with no row, and selecting it exits 2 with "has no implementation".

### Counts today

- `checks.json` has 509 ids: 501 enabled, 7 `architecture-*` unimplemented, 1 opt-in (`coverage`).
- README:
  - "509 checks" appears twice (`README.md:52`, `:175`).
  - The stats box shows "Checks run 501" (`README.md:17`).
  - The class table (`README.md:177-184`) says Syntax 264, Symbol 184, Taint 27, Metric 14, Flow 11,
    opt-in 1. The real values are Metric 13 and Compilation 2, and the table has no Compilation row.
- Test constants (`CatalogTests.cs:11-15`): `CatalogCount = 509`, `EnabledCatalogCount = 501`,
  `UnimplementedCatalogCount = 7`.

### Docs that name removed ids

- `docs/adr/0008-suppression-comments.md:12` (`no-inline-suppression`)
- `docs/fixes/no-fix-yet.md:62-66`
- `docs/fixes/datetime-kind.md:59,80`
- `docs/fixes/loop-bound-injection.md:71`

### Triage result

| Verdict | Rows | Owned elsewhere | Removed here | Kept here |
|---|---|---|---|---|
| WORKS | 15 | 0 | 1 (`no-suppress-finalize`, D5) | 14 |
| DEAD | 132 | 5 (4 secrets, `identical-method`) | 127 | 0 |
| NOISY | 51 | 3 (flow) | 48 | 0 |
| **Total** | **198** | **8** | **176** (Syntax 90, Symbol 86) | **14** |

The 14 WORKS rows kept as they are:
`empty-statement`, `no-async-identifier`, `no-dangerous-get-handle`, `no-fixme`, `no-gc-collect`,
`no-get-executing-assembly`, `no-goto`, `no-public-temp-file`, `no-todo`, `no-trace-write`,
`no-unsafe`, `no-weak-hash`, `restrict-cors`, `secure-temp-file`.

Before Plan 00, 8 of these 14 have fixtures that do not compile (missing `using`s or stub receivers).
This plan fixes those fixtures that still fail after Plan 00.

### Owned by other plans (not touched here)

- **Secrets (plan 05):** `hardcoded-secret`, `hardcoded-credential`, `database-password`,
  `no-hardcoded-jwt-secret`. All DEAD.
- **Taint/flow (plan 03):**
  - All Taint rows.
  - The three Flow-class name-only rows: `lock-released`, `matching-lock-release`,
    `release-lock-same-method`. All NOISY: they fire on every `Monitor.Enter`/`Exit`, including a
    correct try/finally.
- **Duplication (plan 04):** `identical-method`. It is DEAD (an `M` placeholder), but plan 04
  reimplements it, so it stays.
- **Nullable (plan 02):**
  - `null-dereference` (a `Row.Handle`, not name-only).
  - The revival of `validate-public-arguments`. That row is removed here; plan 02 owns bringing it back.
- **Architecture (plan 06):** `architecture-*` (7, already unimplemented, not name-only).

## Design

### 1. Removal

For each of the 176 ids:
- Delete its object in `checks.json`.
- Delete its row in `SyntaxEngine.cs`/`SymbolEngine.cs`.
- Delete its entry in `fixtures.json`.

Then delete the constants left with no use:
- In `SyntaxEngine.cs:8-20`: `ValueKeyword`, `NowProperty`, `DateTimeType`, `HttpGetAttribute`,
  `ChangeTypeMethod`, `EnumerableType`. `MethodPlaceholder` stays, because `identical-method` still uses it.
- In `SymbolEngine.cs:8-12`: `DisposeMethod`, `LoggerType`, `UriType`.

Keep these unchanged: the matcher classes, `Row.cs` and `RowTests.cs`.
- Revival plans use `Row.Member`, `Row.Create` and `Row.Attribute` as building blocks
  (`no-weak-tls`, `no-new-guid`, `datetime-utc`, `no-expected-exception`).
- Each revival plan quotes the original id and message, so restoring a Check takes three edits.

### 2. Guard G1: fixtures compile

New test `Fixtures_compile_without_errors`:
- Compiles each implemented id's `bad` and `good` with the same `CSharpCompilations.Create` the
  runner uses, after Plan 00's fidelity changes.
- Fails on any `DiagnosticSeverity.Error`.

It uses the ADR 0009 baseline mechanism:
- `tests/Qlcheck.Tests/Catalog/Baseline/FixturesCompile.txt` lists the exempt ids.
- A new failure fails the test.
- A baseline id that now compiles also fails the test, so the list can only shrink.

A DEAD row cannot pass G1, because its "call" does not resolve. Framework types that are still
missing after Plan 00 are stubbed inside the fixture.

**The baseline must be measured after Plan 00 lands.** Plan 00 adds global usings, honours csproj
`Nullable` and adds ASP.NET references, so many fixtures that fail today will compile. Pre-00
estimates, for scale only:
- 272 ids before this plan.
- 100 after it: 93 `Row.Handle`/Taint ids plus 7 of the owned rows (`identical-method`'s fixture compiles).

### 3. Guard G2: name-only allowlist

New test `Name_only_rows_are_allowlisted`:
- Parses `*Engine.cs` with Roslyn.
- Collects every `Row.<Invoke|Invokes|Create|Creates|Member|Members|Attribute|Ident|Token|Trivia|Kind|Kinds>("id", ...)` call.
- Requires that set to equal an allowlist in the test, with two groups:
  - **WORKS (14):** the kept rows listed above.
  - **Owned (8):** `hardcoded-secret`, `hardcoded-credential`, `database-password`,
    `no-hardcoded-jwt-secret`, `lock-released`, `matching-lock-release`,
    `release-lock-same-method`, `identical-method`.

Owned ids drop off as their plans replace them. Adding a new name-only row then needs an allowlist
edit that a reviewer will see. G2 catches NOISY placeholders, which G1 cannot: `GetHashCode()`
compiles fine.

### 4. Guard G3: good fixtures exercise the target

New test `Good_fixture_is_not_the_empty_template`:
- A `good` fixture equal to the `return;` template is a failure.
- Shrink-only baseline: 19 ids after removal, `identical-method` among them.

This pushes new fixtures to show compliant use of the same API, which is where NOISY rows get caught.

### 5. Counts

| | Before | After |
|---|---|---|
| `CatalogCount` | 509 | **333** |
| `EnabledCatalogCount` | 501 | **325** |
| `UnimplementedCatalogCount` | 7 | 7 |

README changes:
- "509 checks" becomes **333 checks** in both places.
- "Checks run 501" becomes **325**.
- Class table, recounted from the rows:

| Class | Checks |
|---|---|
| Syntax | 174 |
| Symbol | 98 |
| Taint | 27 |
| Metric | 13 |
| Flow | 11 |
| Compilation | 2 |
| *(opt-in)* | 1 |

The table sums to 326: 325 enabled plus `coverage`.

## Implementation steps (test-first, small commits)

0. **Wait for Plan 00 to merge.** Then re-run the fixture-compile measurement and record the real
   G1 baseline.
1. **Add G1 with the post-00 baseline.** It passes as soon as it is added.
   Commit: "test: fixtures must compile (baseline)".
2. **Fix the WORKS fixtures that still fail G1.** Before Plan 00 that is 8 of the 14 kept rows.
   - Add a real `SafeHandle`, `Path` or `MD5` receiver where needed, and for `restrict-cors` a stub
     `CorsPolicyBuilder` if Plan 00's ASP.NET references don't cover it.
   - Make each `good` show compliant use of the same API (e.g. `SHA256.Create()` for `no-weak-hash`).
   - Remove the fixed ids from the baseline. G1 fails until you do.
   - Commit.
3. **Write G2 with the final 22-id allowlist.** It fails, listing the 176 extra ids. Leave it
   uncommitted in the working tree.
4. **Remove the 86 Symbol rows**, including `no-suppress-finalize`. For each, delete:
   - the `checks.json` object
   - the `SymbolEngine.cs` row
   - the `fixtures.json` entry
   - its G1 baseline line, if any (G1's shrink-only rule forces this)

   Also delete the unused constants and update the count constants. Commit.
5. **Remove the 90 Syntax rows** the same way. Leave `identical-method` in place. G2 now passes.
   Commit G2 with this step.
   - Commit message: "catalog: remove 176 placeholder checks".
   - Commit body must say: *"Removed ids are gone from `checks.json`. A `.qlcheck_disable` line or
     `--check` naming one now fails with `Unknown check: <id>` (exit 2). Delete the line. Revival
     plans live in `docs/plans/01-placeholders/`."*
   - Steps 4 and 5 may be one commit if that is easier to review.
6. **Add G3** with its baseline (19 before Plan 00; re-measure). Commit.
7. **Update the docs.**
   - **README:**
     - Lines 17, 52 and 175.
     - The class table (177-184).
     - The sample table: replace `hardcoded-secret` with `no-weak-hash` (D7).
   - **ADR 0008:12:** say that `no-inline-suppression` is planned, not present.
   - **`docs/fixes/no-fix-yet.md`:** add a closing note that points at this plan and records the removal.
   - **`docs/fixes/datetime-kind.md` and `docs/fixes/loop-bound-injection.md`:** note that the rows
     they reference were removed.
   - Commit.
8. **Verify.**
   - Run `dotnet test`.
   - Run `./run --human .`. The dogfood run should lose the 18 false positives listed above.

## Files touched

- `src/Qlcheck/Languages/CSharp/Catalog/checks.json`: 176 objects removed.
- `src/Qlcheck/Languages/CSharp/Catalog/SyntaxEngine.cs`: 90 rows removed, plus unused constants.
- `src/Qlcheck/Languages/CSharp/Catalog/SymbolEngine.cs`: 86 rows removed, plus unused constants.
- `tests/Qlcheck.Tests/Catalog/fixtures.json`: 176 entries removed; WORKS fixtures rewritten.
- `tests/Qlcheck.Tests/Catalog/CatalogTests.cs`: count constants; G1, G2, G3.
- New: `tests/Qlcheck.Tests/Catalog/Baseline/FixturesCompile.txt`, `Baseline/GoodFixtureTemplate.txt`.
- Docs: `README.md`, `docs/adr/0008-suppression-comments.md`, `docs/fixes/no-fix-yet.md`,
  `docs/fixes/datetime-kind.md`, `docs/fixes/loop-bound-injection.md`.
- Not touched: `FlowEngine.cs` (owned rows), `Match/*.cs`, `Row.cs`, `RowTests.cs`.

## ADR needed?

No.
- Removal is easy to reverse: each revival plan restores the id, message and row.
- The guards follow the shrink-only baseline precedent from ADR 0009, so they are not a surprise.
- A comment on each guard test explaining why it exists is enough.

## Risks

- **`.qlcheck_disable` breakage.** A repo that disabled a removed id gets `Unknown check:` and exit 2
  until the line is deleted. `escape-field-keyword` is the likely one, given how noisy it is. The
  qlcheck repo itself has no such file. The commit body (step 5) says how to fix it.
- **Some real coverage is lost.** A few NOISY rows did catch true positives, e.g. `hardcoded-ip` on
  `IPAddress.Parse("10.0.0.1")` and `dynamic-sql` on `FromSqlRaw($"...")`. Agents stop seeing these
  until the rows are revived. The noise those rows produced outweighs this.
- **G1 depends on Plan 00.** Measure the baseline before Plan 00 lands and it will be wrong in both
  directions. Step 0 guards against this.
- **Fixture stubs can drift.** A stub that differs from the real API could let a wrong predicate
  pass. Keep stubs minimal, namespaced like the real type, and prefer Plan 00's real references.
- **G2 parses source.** Rows built through helper methods would slip past it. If that happens the
  test fails loudly; it does not pass silently.
- **Owned rows stay dishonest** until their plans land. G2's allowlist names them explicitly.
- **Some kept WORKS rows are incomplete.** These are follow-ups, not regressions:
  - `no-trace-write` misses `Trace.Write`.
  - `no-weak-hash` misses SHA1 and `HashData`.
  - `no-unsafe` misses `unsafe` modifiers.
  - `no-async-identifier` misses `await`.

## Effort

- **This plan:** about 1.5-2 days.
  - Removal is mechanical, about half a day with a script.
  - G1-G3: a day.
  - Fixtures and docs: half a day.
- **Revival backlog:** 152 Checks across 12 theme files (S 84, M 64, L 4), roughly 150 dev-days in
  total. Each theme file becomes its own follow-up plan.
  - 23 Checks are marked not worth reviving.
  - 1 (`validate-public-arguments`) is revived by plan 02.

| Theme file | Removed | Revive | Not worth reviving | Revive sizes |
|---|---|---|---|---|
| `01-placeholders/attributes.md` | 33 | 21 | 12 | S 17, M 4 |
| `01-placeholders/api-design.md` | 27 | 25 | 1 (+1 to plan 02) | S 11, M 11, L 3 |
| `01-placeholders/linq-collections.md` | 19 | 18 | 1 | S 10, M 7, L 1 |
| `01-placeholders/expressions.md` | 18 | 17 | 1 | S 12, M 5 |
| `01-placeholders/aspnet-ef.md` | 16 | 11 | 5 | S 6, M 5 |
| `01-placeholders/types-equality.md` | 14 | 12 | 2 | S 4, M 8 |
| `01-placeholders/security-config.md` | 13 | 12 | 1 | S 5, M 7 |
| `01-placeholders/keywords-enums.md` | 12 | 12 | 0 | S 9, M 3 |
| `01-placeholders/async-threading.md` | 7 | 7 | 0 | S 2, M 5 |
| `01-placeholders/crypto.md` | 6 | 6 | 0 | M 6 |
| `01-placeholders/uri.md` | 6 | 6 | 0 | S 4, M 2 |
| `01-placeholders/logging.md` | 5 | 5 | 0 | S 4, M 1 |
| **Total** | **176** | **152** | **23** (+1) | S 84, M 64, L 4 |

**Not worth reviving (23).** Each theme file gives the reason.
- Assembly attributes: `assembly-version`, `cls-compliant`, `com-visible-explicit`,
  `neutral-resources-language`.
- Serialization and legacy frameworks: `constructor-argument-exists`, `iserializable-correct`,
  `optional-field-deserialize`, `validate-on-deserialize`, `no-conflicting-transparency`,
  `one-way-operation-void`, `service-and-operation-contract`, `request-validation`, `set-locale`,
  `durable-entity-interface`.
- Razor, Blazor and EF: `route-constraint-type`, `no-lambda-in-blazor-loop`, `merge-include`.
- Other: `remove-obsolete`, `concise-declaration`, `no-recursive-inheritance`,
  `prefer-first-not-or-default`, `prefer-datetimeoffset`, `no-suppress-finalize`.

## Decisions (resolved)

- **D1. Owned rows stay.** The 8 owned rows stay in place for their plans and are listed in G2's
  allowlist: 4 secrets (plan 05), 3 flow lock rows (plan 03), and `identical-method` (plan 04).
- **D2. No retired-id list.** A `.qlcheck_disable` line or `--check` naming a removed id fails with
  `Unknown check:` (exit 2). The removal commit message says so.
- **D3. All three guards are adopted.** G1 (fixtures compile; shrink-only baseline measured after
  Plan 00), G2 (22-id name-only allowlist) and G3 (no empty-template `good`; shrink-only baseline).
- **D4. No revival plan for the 23.** Checks marked not worth reviving get a one-line reason and no
  revival plan.
- **D5. `no-suppress-finalize` is removed now.** Its advice contradicts CA1816. It is marked not
  worth reviving in `types-equality.md`; `suppress-finalize-needs-destructor` is the check that
  comes back for misuse.
- **D6. The four `DateTime.Now` Checks.**
  - `datetime-utc` and `no-datetime-now-timing` come back on by default.
  - `testable-clock` comes back as opt-in, next to `coverage`.
  - `prefer-datetimeoffset` stays dropped.
- **D7. README sample swap.** The README sample table swaps `hardcoded-secret` (DEAD; owned by
  plan 05) for `no-weak-hash` until plan 05 lands.
- **D8. README class table.** It is recounted from the rows, a `Compilation` row (2) is added, and
  Metric is corrected from 14 to 13.
- **D9. Revival order.** One theme file per follow-up plan, S-sized Checks first, starting with
  `attributes.md` and `keywords-enums.md` (26 S-sized Checks between them).
- **D10. `validate-public-arguments` goes to plan 02.** It is removed here and revived by the
  nullable plan. `api-design.md` keeps a pointer.
