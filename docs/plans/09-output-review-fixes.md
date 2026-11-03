# Plan 09: checker bugs found reviewing a self-scan

Status: Agreed 2026-11-02 · Depends on: 08

## Goal

A review of 36 self-scan Findings (`output.txt`, 2026-11-02) found 14 valid, 5 weak and 17 wrong. The wrong ones trace to
qlcheck bugs, not to qlcheck's code. This plan fixes those bugs:

1. **Checks that run another Check's code.** `simplify-linq` runs `prefer-linq`'s handler, `multiline-needs-braces` runs
   `braces-required`'s, and `seal-attribute` runs `redundant-modifier`'s. Each reports under a message its code never
   tests, and every site is reported twice. In all, 24 handlers serve 67 rows; plan 01 only reviewed placeholder rows.
2. **Matchers that are too loose:** `else if` counts as a braceless body; a loop whose text contains `.Add(` counts as an
   add loop; `});` counts as a misplaced brace; `static-init-order` compares names as substrings.
3. **`identical-method` advice that cannot be followed:** it reports copies with renamed identifiers (by plan 04's
   design) but always says "make one call the other".
4. **A source folder the self-scan never reads:** the built-in ignore rule `coverage/` matches regardless of case, so
   `src/Qlcheck/Languages/CSharp/Checks/Coverage/` (9 files, 1,057 lines) is skipped.
5. **Compilations that reference the qlcheck process:** platform references come from the process's trusted platform
   assemblies, so every scanned project sees `qlcheck.dll`, Roslyn and their dependencies. With (4), this made a used
   `using` in `CSharpLanguage.cs` look unused.

Acceptance:
- No handler serves two rows, except groups listed in a shrink-only baseline (21 groups today).
- `simplify-linq`, `multiline-needs-braces` and `seal-attribute` each report what their message says, and nothing else.
- None of the 9 wrong non-duplicate Findings from the review is reported: `simplify-linq` ×3, `prefer-linq`
  `DisableFiles.cs:102`, `braces-required` and `multiline-needs-braces` `ProjectReferences.cs:111`,
  `brace-at-line-start` `ResourcePatterns.cs:212`, `static-init-order` `SymbolPatterns.cs:295` and `unused-using`
  `CSharpLanguage.cs:4`.
- `identical-method` says "make one call the other" only for exact copies.
- The self-scan reads `Checks/Coverage/`.

## Current state

- **Rows and handlers:** `Row.Handle(id, class, handler)` wraps the handler in a closure (`WalkRow.Apply`), so the
  handler is not visible afterwards. Shared groups today, computed from the engine tables: 24 handlers, 67 rows. Only 4
  rows in 2 groups give self-scan Findings (`prefer-linq`/`simplify-linq` 6+6, `braces-required`/`multiline-needs-braces`
  1+1).
- **Matchers:**
  - `StmtFacts.NeedsBraces` treats an `ElseClause` whose statement is an `IfStatement` as braceless.
  - `StylePatterns.OnlyAdds` tests `only.ToString().Contains(".Add")`, so an `if`/`else` that adds or removes matches.
  - `StylePatterns.CloseBraceNotAlone` reports any block whose `}` line has more than the brace on it after trimming,
    so `});` matches. It reports at the block, which starts at the `{`.
  - `QuietPatterns.StaticUsesLater` reports a static field when an *earlier* field's text contains its name as a
    substring, so `PlaceholderPatternText` (a `const`) "uses" `PlaceholderPattern`.
- **`identical-method`:** `IdenticalMethodPattern` reports every whole-body clone group with "...; make one call the
  other." `CloneGroup` does not record whether the copies are exact or renamed.
- **Ignore rules:** `IgnorePatterns` compiles every rule, built-in or from a file, with `RegexOptions.IgnoreCase`. The
  built-in list is `bin/ obj/ .git/ .vs/ .idea/ .svn/ .hg/ node_modules/ bower_components/ packages/ TestResults/
  coverage/ dist/`.
- **References:** `CSharpCompilations.LoadPlatform` returns every file in `TRUSTED_PLATFORM_ASSEMBLIES`. Packages come
  from `project.assets.json` (`ProjectReferences`) and ASP.NET from the reference pack (`SharedFrameworks`, ADR 0011).
  A probe compilation of `src/Qlcheck` shows CS0436 conflicts with the imported `qlcheck.dll`.
- **`unused-using`:** stays silent in a tree with CS0103, CS0246 or CS1061, but not CS0122 (inaccessible).

## Design

### 1. One handler per row

- `WalkRow` gains `Handler` (the `MethodInfo` of the delegate passed to `Row.Handle`/`Row.Test`).
- A catalog test fails when two rows share a handler, unless both ids are in `Baseline/SharedHandlers.txt`
  (shrink-only, like `FixturesCompile.txt`: one group per line, `Handler: id id ...`). The baseline starts with the 21
  groups this plan does not touch. Reviewing them is later work.

### 2. Real code for the three mismatched rows

All three stay `CheckClass.Syntax` and may read `ctx.Model`, as plan 08's rows do. Catalog counts do not change.

- **`seal-attribute`** ("Non-abstract attributes should be sealed"): a class that is neither `abstract`, `sealed` nor
  `static` and derives from `System.Attribute` (resolved, `Symbols.DerivesFrom`). Reported at the class identifier.
- **`multiline-needs-braces`** ("Multiline blocks should be enclosed in curly braces"): a braceless body of `if`,
  `else`, `for`, `foreach` or `while` whose *next* statement in the same block starts on a later line at the body
  statement's column, which is deeper than the control statement's column. Reported at that next statement, the one
  that looks conditional but is not. `else if` and bodies on the same line are not candidates.
- **`simplify-linq`** ("LINQ expressions should be simplified"): calls that resolve to `System.Linq.Enumerable` or
  `Queryable`:
  - `Where(p)` followed by a parameterless `Any`, `Count`, `LongCount`, `First`, `FirstOrDefault`, `Single`,
    `SingleOrDefault`, `Last` or `LastOrDefault`: use the overload taking `p`;
  - `Select(x => x)`: drop it.

  The message adds the rewrite as detail ("use `Any(predicate)`"). There is no replacement text.

### 3. Precise matchers

- **`braces-required`:** an `else` whose statement is an `if` is not a candidate.
- **`prefer-linq`:** the loop's single statement must be an expression statement whose expression is an invocation of a
  member named `Add`. An `if`, `switch` or other statement is not a candidate.
- **`brace-at-line-start`:** a block whose `{` and `}` are on different lines, where a token other than whitespace
  precedes the `}` on its line. Reported at the `}`. `});`, `},` and `} while (...)` are fine.
- **`static-init-order`:** by symbol. A static, non-const field whose initializer references (by `GetSymbolInfo`) a
  static, non-const field of the same type declared *later* in the same type declaration. Reported at the reference in
  the earlier initializer. Constants never count, and neither do names that merely contain another name.

### 4. `identical-method` advice

`CloneGroup` records whether its copies are exact (every copy's token text equal) or renamed. Exact: unchanged ("...;
make one call the other."). Renamed: "...; extract one shared method whose parameters are the parts that differ."
`duplicate-code` is unchanged.

### 5. Built-in ignore rules match case

`IgnorePatterns.Rule` gains `IgnoreCase`. Rules parsed from `.qlcheck_ignore` files keep today's case-insensitive
matching. The built-in list matches case exactly, as written: `coverage/` skips `coverage/` but scans `Coverage/`,
`packages/` scans `Packages/`, `TestResults/` skips only `TestResults/`.

### 6. Platform references from the runtime only

`LoadPlatform` keeps only trusted platform assemblies in the directory of `typeof(object).Assembly` (the
`Microsoft.NETCore.App` folder). The `typeof(object)` fallback stays. Projects get packages from `project.assets.json`
and ASP.NET from the reference pack, as today. `unused-using` also stays silent in a tree with CS0122.

## Implementation steps (test-first, one commit each)

1. **Handler guard.** Red: `CatalogTests` test that no two rows share a handler outside the baseline; it fails with
   all 24 groups. Green: `WalkRow.Handler`, `Baseline/SharedHandlers.txt` with all 24 groups.
2. **`seal-attribute`.** Red, `StylePatternsTests.cs` (or a new `SealAttributePatternTests.cs`): an unsealed attribute is
   reported; `sealed`, `abstract` and a non-attribute class are not; a `public` interface method is no longer reported.
   Green: the matcher; drop its group from the baseline; update the `fixtures.json` pair.
3. **`multiline-needs-braces`.** Red: the misleading-indentation case under `if`, `else`, `for`, `foreach` and `while`;
   a correctly dedented next statement, a braced body, `else if` and a same-line body give nothing. Green: the matcher;
   drop its group; update the fixtures.
4. **`simplify-linq`.** Red: each `Where(p).X()` and `Select(x => x)` case; `Where(p).Any(q)`, a non-LINQ `Where` and a
   `foreach` give nothing; the detail names the rewrite. Green: the matcher; drop its group; update the fixtures.
5. **`braces-required` and `prefer-linq` precision.** Red: `else if` gives nothing; an `if`/`else` loop body that adds
   or removes gives nothing; `foreach (...) { set.Add(x); }` still gives one. Green: both matchers.
6. **`brace-at-line-start` and `static-init-order`.** Red: `});`, `},` and `} while` give nothing, `x(); }` on the
   brace's line gives one at the `}`; a const named as a prefix of a later field gives nothing, an earlier initializer
   reading a later static field gives one at the reference. Green: both matchers.
7. **`identical-method` advice.** Red, `IdenticalMethodPatternTests.cs`: exact copies keep "make one call the other";
   renamed copies get the extract advice. Green: `CloneGroup.Exact` and the message.
8. **Case-sensitive built-in ignore rules.** Red, `IgnorePatternsTests`/`SourceScan` tests: a `Coverage/` source folder
   is scanned and `coverage/` is skipped; a `.qlcheck_ignore` rule still matches case-insensitively. Green: `Rule.IgnoreCase`.
9. **Runtime-only platform references and CS0122.** Red: a compilation built inside the test host has no reference to
   `qlcheck.dll` or `Microsoft.CodeAnalysis`; `unused-using` stays silent in a tree with CS0122. Green: both.
10. **Self-scan and docs.** Run `./run .` and diff against the 448 baseline. Expected: the 9 wrong Findings above are
    gone, `identical-method` messages change, and `Checks/Coverage/` adds Findings (24 when scanned alone, some of them
    artefacts of a partial compilation or of the bugs fixed here). Fix real Findings in qlcheck's own code in the same
    commit, or explain each one that stays. Record the count in Progress; README if any count changes.

## Files touched

- Catalog: `WalkRow.cs`, `Row.cs`, `StylePatterns.cs`, `Patterns.cs`, `QuietPatterns.cs`, `Queries/StmtFacts.cs`,
  `CloneGroup.cs`, `CloneIndex.cs`, `IdenticalMethodPattern.cs`, `UnusedUsingPattern.cs`, `SyntaxEngine.cs` (handler
  names for the three rows)
- Elsewhere: `Scan/IgnorePatterns.cs`, `Languages/CSharp/CSharpCompilations.cs`
- Tests: `CatalogTests.cs`, `Baseline/SharedHandlers.txt` (new), `StylePatternsTests.cs`, `PatternsTests.cs`,
  `QuietPatternsTests.cs`, `IdenticalMethodPatternTests.cs`, `UnusedUsingPatternTests.cs`, ignore and compilation
  tests, `fixtures.json`
- Docs: this plan, `docs/plans/README.md`

## ADR needed?

No. Each change narrows a matcher, fixes advice or removes a leak; none is hard to reverse. Step 6 refines ADR 0011
(compilations mirror the project build) rather than changing it: a project never referenced Roslyn, so its compilation
should not either.

## Risks

- **Unrestored projects lose accidental references.** A project that uses a package without a restore no longer
  resolves it through qlcheck's own copy, so more trees have compile errors and some rows go silent there (ADR 0003).
  That is the honest result.
- **New Findings from `Checks/Coverage/`** in the self-scan. Step 10 decides each.
- **`multiline-needs-braces` and tabs.** Columns are compared in characters; a file mixing tabs and spaces may mislead
  it. `no-tabs` already reports tabs.
- **The 21 baselined groups** still contain mismatched rows (for example `StylePatterns.StringToString` serves 7 rows,
  including `no-hardcoded-date-format` and `use-return-value`). They report today as before; the baseline makes the
  debt visible and keeps it from growing.

## Effort

About 2 days: guard ½ day, the three matchers 1 day, the precision fixes, advice, ignore rules and references ½ day.

## Decisions (resolved)

- **D1. Scope of shared handlers:** fix the three pairs from the review; guard the rest with a shrink-only baseline.
- **D2. The three mismatched rows get real code**, not removal: `seal-attribute`, `multiline-needs-braces` and a small,
  exact `simplify-linq` set.
- **D3. Built-in ignore rules match case exactly**; rules from `.qlcheck_ignore` keep case-insensitive matching.
- **D4. Platform references come from the runtime folder only**, and `unused-using` treats CS0122 like a missing type.

## Noticed in passing (not in this plan)

- `no-nested-generic-signature` ignores visibility (a private helper is reported), like the rows plan 08 fixed.
- `prefer-linq`'s advice is wrong for `foreach (var x in xs) set.Add(x)`: the fix is `UnionWith`/`AddRange`, not LINQ.
- `no-path-resolution` on `GitChanges.cs:115` is deliberate (qlcheck runs the user's `git`) and wants a suppression
  with a reason.
