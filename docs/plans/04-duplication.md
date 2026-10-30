# Plan 04: duplicate code (clone) detection

Status: Agreed 2026-10-29 · Depends on: none technically; scheduled after 00, 01, 05, 02, 03

The prototype and the measurements
live in `/tmp/claude-1000/-home-lmaslanka-projects-qlcheck/1fadbd8a-84fa-480a-a869-298258cc64bb/scratchpad/probes-duplication/`
(`proto/Program.cs`, `qlcheck-self-50-4.txt`). Cohesion metrics (LCOM) are out of scope, as the user decided.

## Goal

Add a Check, `duplicate-code`, that finds copy-pasted code (Type-1 exact and Type-2 renamed clones)
within one file and across files. It emits a Finding an agent can act on. Each Finding:

- points at one copy,
- names the other copies (`path:line-line`), and
- says what to do: "extract one shared method and call it from each copy".

There is no `replacement`, because extracting a method cannot be automated safely. The Check must:

- stay quiet on boilerplate: property lists, DTOs, row tables, switch-expression arms, runs of similar one-liners,
- add only a few percent to a run, and
- compose with `--uncommitted`, `.qlcheck_disable` and suppression comments without special cases.

## Current state

- **No duplication check exists.** `identical-method` is owned by this plan: the placeholder cleanup
  (plan 01) deliberately leaves it alone, and step 8 below replaces it. Today `identical-method` ("Methods should not have identical
  implementations.") is a placeholder row, `Row.Invoke("identical-method", CheckClass.Syntax, MethodPlaceholder)`
  at `src/Qlcheck/Languages/CSharp/Catalog/SyntaxEngine.cs:84`. It fires on any call to a method
  named `M` (its fixture's `bad` case is just `M();`). It is enabled by default and counted as implemented.
- **Engines are chosen by scope (ADR 0007).** There are three:
  - Per tree: `CatalogWalker.Walk`, called from `CatalogRun.Execute` (`Catalog/CatalogRun.cs:11`).
  - Per Compilation: `CompilationEngine` (`Catalog/CompilationEngine.cs:6`), run at `CatalogRun.cs:30-35`.
  - Process: `RunEngine` (coverage).
- **One Compilation per csproj.** `CSharpLanguage.RunCatalog` (`Languages/CSharp/CSharpLanguage.cs:56`)
  parses every loaded file once into `trees` (`:67`). It then groups files by owning csproj and calls
  `CatalogRun.Execute` once per group (`:71-75`). A run over this repo has 2+ Compilations
  (`src/Qlcheck`, `tests/Qlcheck.Tests`, `tests/Qlcheck.ArchitectureTests`). No code sees all trees together except `RunCatalog`.
- **Closest prior art: `MagicLiteralSharedPattern`** (`Catalog/MagicLiteralSharedPattern.cs`). It:
  - collects candidates over all trees of one Compilation,
  - groups them by key (`:29-31`), and
  - reports **one Finding per use**, whose message names the other files (`:52-58`, `SharedMessage`).

  It reports through `CompilationContext.ReportCustom` (`Catalog/CompilationContext.cs:29`). It never
  asks whether a file is "changed": `Includes` (`:32`) is effectively always true, because
  `RunCatalog` passes every loaded file.
- **`--uncommitted` works by filtering Findings after the run.** `QlcheckApp.cs:33` computes the
  `reported` files, and every check still runs over all loaded files (`:88-96`, comment at `:88`).
  Findings outside changed files are dropped afterwards (`InFiles`, `:98-104`).
- **Suppression and disabling are also post-run filters, keyed on the Finding's own file and line.**
  - Suppression: `Run/Suppressions.cs:44-45` (same line or the line above).
  - Disabled trees: `Run/CheckRun.cs:38-39` and `WithoutDisabled` `:42-58`. The comment at `:42` says
    "Cross-file Checks still saw files in a disabled tree; only their Findings there are dropped."
- **Thresholds** are `internal const int`s in `Catalog/Thresholds.cs`, for example `SharedLiteralFiles = 2` at `:34`.
- **Generated-file detection exists only in coverage.** `CoverageCheck.IsGenerated` checks the `.g.cs`
  and `.Designer.cs` suffixes (`Checks/Coverage/CoverageCheck.cs:15-17,67`).
- **Tests.**
  - `tests/Qlcheck.Tests/Catalog/CatalogTests.cs` pins the counts (`CatalogCount = 509` `:11`,
    `EnabledCatalogCount = 501` `:13`).
  - It also requires a bad/good fixture per implemented id in `Catalog/fixtures.json`
    (`Each_implemented_check_has_a_failing_and_clean_fixture`, `:125`). Its `Run` helper dispatches
    on class, with a `CheckClass.Compilation` branch at `:432`.
  - `MagicLiteralSharedPatternTests.cs` is the template for multi-file unit tests.
  - `QlcheckAppTests.cs:271-331` covers `--uncommitted` with a temp git repo.
- **Architecture rule L5** (`tests/Qlcheck.ArchitectureTests/Rules/LayeringTests.cs:38,71`): Catalog
  code must not depend on Run, Report or Cli.

## Measurements (prototype, this machine, 32 cores)

| What | Result |
|---|---|
| Baseline `./run .` (dotnet run, includes build check) | **6.3 s** |
| Baseline release binary `qlcheck --stats .` | **5.6-5.9 s** (131 files, 19,057 lines, 367 Findings) |
| Prototype on this repo (145 .cs, 20.7k lines), after parsing | **~0.2 s** (units 160 ms + match 40 ms). Parsing is shared with qlcheck, so the cost is about **+3 %** |
| Newtonsoft.Json Src (948 files, 196k lines), full qlcheck run | 19.3 s |
| Same corpus, prototype after parsing | ~1.3 s, about **+7 %**, peak RSS 166 MB including its own trees |
| Worst case: that corpus copied 3× (2,844 files, 587k lines, everything duplicated) | match 2.2 s, 8.7k occurrences, peak RSS 309 MB. The prototype keeps a token array per *nested* statement; the production design below needs about 8 B/token, roughly 10 MB per 200k lines |

### Strategy comparison on this repo

All rows use "consistent" Type-2 unless they say otherwise.

| Strategy | Groups / occurrences | Verdict |
|---|---|---|
| Token windows (CPD-style), blind Type-2, 100 tokens | 168 groups, 538 occ., "139 % duplicated" | Useless. Windows run across member boundaries, and every `[Fact]` looks the same |
| Token windows, consistent renaming, 100 tokens | 29 groups, 96 occ. | Noisy. It matches runs of `const` declarations, rows of one-line `Flag(...)` members and namespace headers |
| Token windows, exact (Type-1), 100 tokens | 3 groups | Misses all renamed clones |
| **Statement-aligned, identifiers consistently renamed, literals exact, ≥50 tokens and ≥4 nested statements, periodic-run filter** | **8 groups, 18 occ.** (src 4/10, tests 4/8) | **All 8 are genuine and extractable** |
| Same, but literals also normalized | tests: 11 groups / 25 occ. | Extra hits are data-driven tests (same shape, different inputs), a different fix |

### The 8 groups this repo would get (`qlcheck-self-50-4.txt`)

- `MetricMatch.cs:82/103/145/182`: 4 copies of `foreach node … score … if > T Report`.
- `MetricMatch.cs:196-205` / `:213-222`: type arity vs method arity.
- `InvokeMatch.cs:11-24` / `MemberMatch.cs:11-24`.
- `SymbolPatterns.cs:782-795` / `:973-986`.
- 4 test-helper blocks: `ReportTests`, `QlcheckAppTests`, `DisableFilesTests`, `CatalogTests`.

### Newtonsoft.Json with the recommended setting

- Production code: 20 groups, 41 Findings in 70k lines. A sample of 20 read as genuine.
- Tests: 375 groups, 874 Findings in 125k lines. Long Arrange/Assert blocks are copy-pasted.
- At 100 tokens / 8 statements the tests still have 128.

## Design

### 1. Algorithm (recommended): statement-aligned hashing with token verification

Rejected alternatives:

- **Raw token windows (Rabin-Karp or suffix array over all tokens).** They are fast, but a clone
  can start and end anywhere: `} } public void X(`, half of a const list. That is noise, and an
  agent cannot "extract a method" from a region that is not a statement sequence (measured above).
  A suffix array finds the same maximal repeats as a rolling hash plus left-maximal extension, at
  more code and memory. Its only edge, exact maximal repeats in O(n log n), does not matter once
  units are statements (about 1/13 as many units as tokens).
- **Syntax-subtree hashing (Baxter).** It finds whole-subtree clones only. It misses "3 of the 5
  statements in this block are copied". The statement-sequence approach covers it: a single
  statement whose subtree is ≥50 tokens is already a 1-unit clone.

Recommended pipeline:

1. **Token stream per file.** One pass over `root.DescendantTokens()` (trivia ignored) fills two arrays:
   - `norm[i]`: the token kind. Identifiers become one `$id` symbol. Literals stay exact (interned text).
   - `raw[i]`: interned identifier text.

   A prefix rolling hash over `norm` gives the hash of any token range in O(1).
2. **Units = statements.** Every `BlockSyntax` and `SwitchSectionSyntax` statement list is a
   *sequence*, and each `StatementSyntax` in it is a unit:
   - `(file, firstTok, lastTok, hash, tokenCount, nestedStatementCount)`
   - `nestedStatementCount` counts non-block statements in its subtree.
   - A sentinel separates sequences, so a clone never crosses a block boundary.

   Nested blocks are sequences of their own, so a duplicated `if` body inside non-duplicated code is still found.
3. **Candidates.** Bucket units by hash. For each pair in a bucket:
   - skip the pair if the previous units also match (left-maximality, as in CPD);
   - otherwise extend right while the unit hashes are equal *and* the `norm` token ranges are equal
     (this guards against hash collisions);
   - cap the bucket: pairwise up to 32 entries, then compare to the first entry only. This keeps
     degenerate input linear.
4. **Filters on a candidate region:**
   - ≥ `Thresholds.DuplicateTokens` (50) tokens and ≥ `Thresholds.DuplicateStatements` (4) nested statements.
   - **Consistent renaming**: identifier text must map bijectively (`a→x` everywhere, never
     `a→x` and `a→y`). Then "extract a method whose parameters are the renamed identifiers" is a valid fix.
   - **No self-overlap**: two copies in the same sequence must not overlap.
   - **Periodic run filter**: reject a region whose statement hashes repeat with period
     `p ≤ len/2`. This drops `Assert.Equal(a.X, b.X)` ×N, `WritePropertyIfNotNull(...)` ×N and
     other "list of similar calls" runs, which a loop or table fixes, not an extracted method.
5. **Groups.** Key occurrences by the region's normalized content. A group is all copies of one
   region. Then claim greedily, deterministically ordered by tokens desc, copies desc, first path,
   first position:
   - an occurrence that overlaps an already-claimed span is dropped;
   - a group survives only with ≥2 unclaimed copies.

   This removes nested and shifted sub-clones of a bigger clone. Use per-file sorted interval lists,
   not the prototype's O(n²) scan.

### What is excluded

Most exclusions are structural: only statements are units. So these never participate:

- `using` directives, attributes, namespace and type headers;
- property, field and enum-member lists, DTOs, records;
- collection and array initializers outside statements (the `Row.*` tables in `*Engine.cs`);
- expression-bodied members (`Row.cs` factories);
- switch-expression arms.

Switch-*statement* sections are sequences, so a duplicated `case` body counts if it reaches the threshold.

Generated files are skipped as both source and target. That covers `*.g.cs`, `*.g.i.cs`,
`*.Designer.cs`, `*.generated.cs`, and a `<auto-generated` comment in the first lines. Move
`CoverageCheck.IsGenerated` into a shared `Queries/FileFacts`-style helper and extend it. See D6.

### 2. Thresholds (in `Catalog/Thresholds.cs`)

```csharp
internal const int DuplicateTokens = 50;      // tokens in the duplicated region
internal const int DuplicateStatements = 4;   // nested non-block statements in the region
internal const int DuplicateOthersNamed = 3;  // other copies spelled out in the message
```

These were measured, not guessed. On this repo:

| Setting | Groups |
|---|---|
| 30 tokens / 3 statements | 19 (idiomatic 7-line `foreach…Report` loops start to appear) |
| **50 / 4** | **4** |
| 75 / 6 | 0 |

In Newtonsoft production code 75/6 drops 20 genuine groups to 4. A line minimum adds nothing once
statements are counted. The bucket cap (32) stays a private const in the pattern file: it is
plumbing, not a threshold.

### 3. Scope and engine: one pass over the whole run, across Compilations

Clone detection is pure syntax and needs no `SemanticModel`. It should see every loaded tree in the
run, not one Compilation. Copies between `Api/` and `Worker/` projects, or between two services in
a monorepo, are the most valuable to report and the ones a per-csproj scope would miss.

Following ADR 0007 ("a Check that needs a different execution scope gets an engine at that scope"):

- `CheckClass.SourceSet` (new enum member, alongside `Compilation` and `Process`).
- `SourceSetContext` (Catalog). It holds:
  - the trees sorted by normalized path, each with its `SourceFile.Path`;
  - the messages;
  - `ReportCustom(id, path, location, message)` → `Finding.At`.

  It references only Roslyn and `Finding`, so rule L5 holds.
- `SourceSetEngine.Rows`: `SourceSetRow { Id, Class, Apply }`, mirroring `CompilationEngine`.
- `CatalogRun.ExecuteSourceSet(loaded, trees, checks)`: a new method on the existing seam.
  `CSharpLanguage.RunCatalog` calls it **once** after the per-group loop (`CSharpLanguage.cs:71-75`),
  reusing the already-parsed `trees` (`:67`). It does not parse again.
- `CatalogWalker.Implemented` / `Classes` (`CatalogWalker.cs:17,23`) concatenate `SourceSetEngine.Rows`.

### 4. Finding shape

One Finding **per copy**: N copies give N Findings, never N². Each Finding is at the first token of
its copy, so the ID is `duplicate-code:{file}:{line}:{column}`. The message is custom:

```
Lines 82-89 duplicate src/Qlcheck/Languages/CSharp/Catalog/Match/MetricMatch.cs:103-110 and 2 more
(MetricMatch.cs:145-152, MetricMatch.cs:182-189); extract one shared method and call it from each copy.
```

- Paths are the Finding-style repo-relative path (`SourceFile.Path`). Unlike
  `MagicLiteralSharedPattern`'s bare `Path.GetFileName`, they work for files with the same name in
  different folders, and the agent has to open them.
- The message names up to `DuplicateOthersNamed` others, sorted by path then line, then "and K more".
- Static message for `checks.json`: "Duplicated code should be extracted into one shared method."

Why per copy and not one Finding per group:

- `--uncommitted`, disabling and suppression all filter on a Finding's own file and line
  (`QlcheckApp.cs:98`, `CheckRun.cs:42`, `Suppressions.cs:44`). Per copy, all three work with no special case.
- One Finding per group would sit in one arbitrary file. `--uncommitted` would drop it whenever that
  file was committed, and suppressing it would silence every copy.
- The cost is that an agent sees 2+ Findings for one fix. Fixing one group clears all of them, as
  with `magic-literal-shared` today.

### 5. Interaction with existing mechanisms

- **`--uncommitted`.** Nothing to build. The engine sees all loaded files and the post-filter keeps
  only copies in changed files. So "report a clone only when a changed file takes part" falls out,
  and the Finding is on the changed copy. A clone between two committed files is silent. A clone
  whose changed copy is in an untouched part of a changed file is still reported: same file-level
  granularity as every other check.
- **Suppression comment.** It goes on the first line of a copy or the line above, and silences that
  copy only. It is legitimate only for a human-judged "keep both copies" (for example, deliberately
  decoupled bounded contexts).
- **`.qlcheck_disable`.** Copies in a disabled tree are not reported, but they still count as "other
  copies" for Findings elsewhere. This matches the cross-file comment at `CheckRun.cs:42` (D4).
- **Scanned folder.** Running on `src/` alone cannot see a copy under `tests/`. ADR 0010's
  "same Findings regardless of scanned folder" cannot fully hold for any cross-file Check;
  `magic-literal-shared` already has this property. Document it, don't fight it.

### 6. Glossary (CONTEXT.md)

Add these terms:

- **Duplicate code**: a sequence of statements of at least the threshold size that appears in
  another place with the same shape. Identifiers may be renamed consistently; literals must match.
  Boilerplate such as member lists and runs of similar one-line statements doesn't count.
  _Avoid_: clone (in messages), copy-paste.
- **Copy** (of duplicate code): one place where it appears. Each copy gets its own Finding, which names the others.

## Decisions (resolved)

The user accepted all of these on 2026-10-29.

- **D1. Check id and `identical-method`.** A new row `duplicate-code` is added. The same engine
  reports a copy as `identical-method` when it covers the *entire* body of ≥2 methods or accessors,
  with the message "…make one call the other". No copy ever gets both ids. This plan owns
  `identical-method`: plan 01 (placeholder cleanup) leaves the row alone, and step 8 here replaces
  the placeholder (`SyntaxEngine.cs:84`).
- **D2. Default on.** `duplicate-code` is enabled by default, not opt-in.
- **D3. Scope is the whole run.** Clones are detected across every loaded file, including across
  csproj boundaries, through a new `SourceSetEngine` rather than per Compilation.
- **D4. Tests are treated like production code.** There is no built-in test detection and no
  separate thresholds. Opting out is a human `tests/.qlcheck_disable` entry. Copies in disabled
  trees still count as "other copies" for Findings in enabled trees.
- **D5. Clone definition.** Statement-aligned. Identifiers are renamed consistently (bijectively).
  Literals must match exactly. Minimum 50 tokens and 4 nested statements. Runs of a repeated
  statement shape are ignored.
- **D6. Built-in generated-code skip.** Files ending `.g.cs`, `.g.i.cs`, `.Designer.cs` or
  `.generated.cs`, or starting with an `<auto-generated` comment, are skipped as both copies and targets.
- **D7. One Finding per copy.** N copies give N Findings, each naming the others.

## Implementation steps

Test-first, one small commit each. Each step: red test → code → `dotnet test` → `./run --human .`.

1. **Engine seam (no Check yet).**
   - Test: `CatalogWalker.TryClass` returns `CheckClass.SourceSet` for a row in `SourceSetEngine.Rows`.
     Use a test-only row via an internal `Rows` override, or land this together with step 3.
   - Test: `CatalogRun.ExecuteSourceSet` receives trees from two csproj groups in one call.
   - Code: add `CheckClass.SourceSet`, `SourceSetRow`, `SourceSetContext`, `SourceSetEngine`, and
     `CatalogRun.ExecuteSourceSet`. Call it from `CSharpLanguage.RunCatalog` after the group loop.
     Extend `CatalogWalker` sets.
   - Extend the `CatalogTests.Run` helper (`:432`) with a `SourceSet` branch.
2. **Shared generated-file helper (D6).**
   - Tests: suffixes, `<auto-generated` header, a normal file.
   - Code: move `CoverageCheck.IsGenerated` to a Catalog `Queries` helper, extend it, and have coverage call it. No behaviour change for coverage beyond the extra patterns.
3. **Token and unit indexing.** `DuplicateCodePattern` internals, unit-tested on small strings:
   - the unit list skips usings, attributes and members;
   - consistent renaming accepts `a→x` and rejects `a→x, a→y`;
   - different literals give different hashes;
   - prefix-hash range equality.
4. **Type-1 and Type-2 detection, within a file and across files.**
   - Tests, modelled on `MagicLiteralSharedPatternTests`:
     - two files with the same 4-statement block → 2 Findings, each naming the other `path:line-line`;
     - renamed copy (the `InvokeMatch`/`MemberMatch` shape) → reported;
     - inconsistent rename → not reported;
     - literal differs → not reported;
     - 49 tokens or 3 statements → not reported;
     - within-file copy → reported.
   - Code: buckets, left-maximal extension with token verification, filters, `ReportCustom`.
   - Add `Thresholds.Duplicate*`, the `checks.json` row, a `fixtures.json` bad/good entry, and the
     `CatalogTests` counts (510 / 502). Update the README counts and the class table.
5. **Noise filters.** Tests:
   - property list, DTO, record, `Row.*` table, expression-bodied members, switch expression → nothing;
   - periodic `Assert.Equal(a.X, b.X)` ×8 → nothing;
   - overlapping self-repeat → nothing;
   - nested: an outer clone is reported once, with no separate Finding for its inner block;
   - generated file is neither reported nor named.
6. **Grouping and messages (D7).**
   - Tests: 3 copies → 3 Findings, each naming the 2 others; 5 copies → "and K more" after 3.
   - Determinism: shuffled tree order gives identical Findings and IDs.
   - Bucket cap: 5,000 identical statements finish quickly with bounded Findings. Assert the count; no wall-clock assertion.
7. **Run-level behaviour.** `QlcheckAppTests`-style, using the existing temp git repo helpers (`:271-331`):
   - `--uncommitted` reports only the changed copy, with a message naming the committed one;
   - a suppression comment above copy A silences A only;
   - `tests/.qlcheck_disable` hides the test copy but still names it from `src`;
   - two csproj folders → a cross-project copy is found (D3).
8. **`identical-method` (D1).**
   - Tests: two methods with the same full body → `identical-method` only, never also `duplicate-code`.
   - Replace the placeholder fixture with a real one.
   - Code: classify a group whose every copy covers a whole member body.
9. **Docs.** CONTEXT.md terms (§6). A README "What it checks" line. A one-line note in the README
   `--uncommitted` and disable sections that cross-file Checks still see committed and disabled files.
10. **Dogfood.** `./run --human .` shows the 8 expected groups (18 Findings). Fixing qlcheck's own
    duplicates is separate follow-up work, done in separate commits if wanted, not part of this change.
    Re-measure the `./run .` time and expect ≤ +5 %.

## Files touched

- New: `src/Qlcheck/Languages/CSharp/Catalog/SourceSetEngine.cs`, `SourceSetRow.cs`,
  `SourceSetContext.cs`, `DuplicateCodePattern.cs` (plus maybe `Queries/CloneIndex.cs` if the
  pattern grows past one screen), and `Queries/GeneratedFiles.cs` (or an addition to `Queries/FileFacts.cs`).
- Edited:
  - Catalog: `CheckClass.cs`, `CatalogWalker.cs`, `CatalogRun.cs`, `Thresholds.cs`, `checks.json`,
    and `SyntaxEngine.cs` (remove the `identical-method` placeholder row, D1).
  - `Languages/CSharp/CSharpLanguage.cs` and `Languages/CSharp/Checks/Coverage/CoverageCheck.cs`
    (use the shared generated-file helper).
- Tests:
  - New `tests/Qlcheck.Tests/Catalog/DuplicateCodePatternTests.cs` and `SourceSetEngineTests.cs`.
  - Edited `Catalog/CatalogTests.cs` (counts, `Run` branch), `Catalog/CatalogWalkerTests.cs`,
    `Catalog/fixtures.json` (`duplicate-code`, real `identical-method`), `QlcheckAppTests.cs`, and the coverage generated-file tests.
- Docs: `CONTEXT.md` and `README.md` (counts 509→510, 501→502, class table, short check description).
  No ADR.

## ADR needed?

**No.** ADR 0007 already allows "a Check that needs a different execution scope gets an engine at
that scope", and a run-wide syntax scope is one more instance of that. The clone definition,
thresholds and per-copy Findings are easy to reverse: constants and one pattern file, no stored
state, no format change. They are recorded here and in the glossary instead.

## Risks

- **Noise in test-heavy repos (D4).** Newtonsoft's tests give 874 Findings at 50/4. Mitigation: the
  disable file. The README already uses tests/ disabling as its example.
- **Many Findings for one fix.** 4 copies give 4 Findings. The message makes it clear they are one
  group, and fixing one group clears all.
- **Idiomatic shapes just over the threshold.** Seven-line `foreach … if … Report` loops reach about
  50 tokens. Mitigations: the 4-statement minimum, and thresholds that are one constant to tune
  after dogfooding.
- **Results depend on the scanned folder.** Inherent to any cross-file Check. Documented.
- **Pathological input.** Huge generated tables of identical statements. Mitigations: periodic
  filter, bucket cap, left-maximality, and the generated-file skip.
- **Memory.** Flat per-file `int[]` token arrays at about 8 B/token, so about 10 MB per 200k lines,
  on top of the trees qlcheck already holds. The prototype's 300 MB peak at 587k lines came from
  per-nested-statement copies, which the design avoids.
- **Determinism.** Dictionary order must never reach the output. Sort trees, groups and copies explicitly; covered by a test in step 6.
- **Line drift.** As with every check, a Finding's ID moves when lines above it change.

## Effort

About 2-3 focused days: steps 1-2 ≈ 0.5 d, 3-6 ≈ 1-1.5 d, 7-8 ≈ 0.5 d, 9-10 ≈ 0.25 d. The
algorithm is about 300-400 lines in one pattern file, plus about 100 lines of engine plumbing.
