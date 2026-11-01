# Plan 06: architecture conformance as a user-facing Check

Status: Done 2026-10-31 (agreed 2026-10-29) · Depends on: 00 (compilation fidelity); scheduled last

## Progress

- **Done** (2026-10-31, branch `architecture`): steps 1-10. Counts are 334 catalog / 333 enabled; every catalog row is
  implemented. The plan's 509/501 predate plan 01's cleanup.
- **Dogfood** (§1's example file on a scratch copy of this repository): exactly the predicted breach, L4
  (`Scan/SourceScan.cs:5` and `:13` use `Qlcheck.SourceFile`); L1-L3, L5 and L6 hold and `no-cycles Qlcheck.*` is
  clean. Whether to move `SourceFile` or relax the Constraint is still the human call noted under Dogfooding.
- **Cost:** a full run takes about 0.9 s more with the file (11.4 s -> 12.3 s, median of 3), under the ~1 s that would
  call for the prefilter in step 10; without the file nothing is extracted.

## Goal

Let a human state a repository's intended architecture once, in a small repo file, and have qlcheck
emit Findings an agent can act on when code breaks it: "this reference makes `X` use `Y`, which the
architecture forbids, so do this instead." Replace the 7 placeholder `architecture-*` rows with
2 implemented Checks and delete the rest. This plan covers no inference and no config the user did
not ask for. With no file, nothing is reported.

## Current state

- **Placeholder rows.** `src/Qlcheck/Languages/CSharp/Catalog/checks.json:18-45` holds 7 rows
  (`architecture-component`, `-interface`, `-location`, `-move`, `-relationship`, `-rename`, `-tangle`).
  No engine claims them, so `CatalogCheck.EnabledByDefault` (`CatalogCheck.cs:25`) is false.
  Selecting one fails in `CheckDiscovery.RejectUnimplemented` (`Run/CheckDiscovery.cs:78-92`,
  message `NoImplementation` at `:12`), and listing one in `.qlcheck_disable` fails too
  (`DisabledError`, `:50-57`). Both exit 2. ADR 0006 states this as policy ("Architecture rows stay
  discovered and unimplemented; selecting one exits 2").
- **Unused enum value.** `CheckClass.Architecture` exists (`Catalog/CheckClass.cs:13`), but no row uses it.
- **Implemented ids.** These come from the walk engines, `CompilationEngine.Rows` and `RunEngine.Rows`
  (`Catalog/CatalogWalker.cs:17-27`).
- **Tests that pin the placeholders.** `tests/Qlcheck.Tests/Catalog/CatalogTests.cs:11-15` sets the counts
  (509 / 501 / 7 unimplemented). `:82-99` covers "architecture checks are discovered and unimplemented"
  and "selected architecture check exits two". The fixture loop at `:125-149` skips only `CheckClass.Process`,
  and its `Run` helper (`:424-440`) is single-file and knows only the Compilation class.
- **Execution scopes.**
  - `CSharpLanguage.RunCatalog` (`Languages/CSharp/CSharpLanguage.cs:56-78`) groups the loaded files by
    nearest csproj (`:71`) and calls `CatalogRun.Execute` once per group.
  - `CatalogRun.Execute` (`Catalog/CatalogRun.cs:11-38`) builds **one Compilation per csproj group**
    (`:19`), walks the per-file engines, then runs `CompilationEngine.Apply` (`:34`).
  - No step today sees all groups' semantic data together.
  - Coverage is the only run-scoped Check, and it is wired by hand in `CSharpLanguage.Execute:46-51`.
- **Cross-project resolution.** In `ProjectReferences.AddProjectOutput` (`Languages/CSharp/ProjectReferences.cs`),
  a ProjectReference resolves only through the referenced project's built `bin/**.dll`. An unbuilt sibling
  project's types do not resolve.
- **Implicit usings are not modelled yet.**
  - `obj/` is a default ignore (`Scan/IgnorePatterns.cs:38`), so `*.GlobalUsings.g.cs` never enters a Compilation.
  - Nothing reads `<ImplicitUsings>` or `<Using Include>` (I grepped `src/` and found none).
  - In an SDK project, `List<>`, `File`, `HttpClient` and similar resolve only through an explicit `using`.
  - **Plan 00** (`docs/plans/00-compilation-fidelity.md`, ADR 0011) fixes this. This plan assumes it has landed.
- **Placeholder cleanup.** Plan 01 runs before this plan but leaves the `architecture-*` rows alone.
  This plan deletes the 5 unused ones.
- **Repo-file prior art.**
  - `Scan/DisableFiles.cs` is a line-based format with `#` comments and `!` negation. It walks
    `RepoRoot.Chain(boundary, dir)`, and each entry records `File` and `Line` for exit-2 errors.
  - `Scan/RepoRoot.cs` provides `GitRoot`, `Boundary` and `Chain`. `Scan/IgnoreTree.cs` is the same idea.
  - Load and validate happen in `QlcheckApp.Run` (`QlcheckApp.cs:34`, `:42-45`). Any exception there,
    or one thrown from `CheckRun.Execute` (`:54-60`), is printed to stderr and exits 2.
- **Filtering of Findings.**
  - Suppression comments and disabled trees filter Findings centrally in `Run/CheckRun.cs:38-58`, by
    `Finding.File`. This works for any Finding located in a loaded `.cs` file.
  - `--uncommitted` runs every Check on all loaded files and then keeps only the Findings in uncommitted
    files (`QlcheckApp.cs:33`, `:64-67`, `:90-104`).
- **Seams.** `ILanguage.Execute(files, checks)` (`Languages/ILanguage.cs:12`) is the only seam from Run
  into a Language. It has one test fake, `RecordingLanguage` (`tests/Qlcheck.Tests/Run/CheckRunTests.cs:147`).
- **Prior art for the tests.**
  - `tests/Qlcheck.ArchitectureTests` (ADR 0009) defines 6 rules. L1–L5 are `ForbiddenUsings(folder,
    prefixes…)` (`Rules/LayeringTests.cs:83-88`). They match a **folder** source against **`using` directive
    names** (`Support/Sources.cs:31`). L6 forbids `new Process` and `new ProcessStartInfo` outside 2 files.
  - Each rule has a shrink-only `Baseline/<id>.txt` (`Support/Baseline.cs`). All baselines are empty.
- **Glossary.** `CONTEXT.md` already defines **Constraint**: "One declared statement of intended
  architecture, written by a human in the repository's architecture file: a component must not use
  another, a component is only used by certain others, or components under a prefix must not form a
  cycle. A component is a namespace or a type, named by its dotted name." This plan uses that term
  (and avoids "rule").

### Probe evidence

Probe code: `/tmp/claude-1000/-home-lmaslanka-projects-qlcheck/1fadbd8a-84fa-480a-a869-298258cc64bb/scratchpad/probes-architecture/deps/Program.cs`.
It builds one Roslyn Compilation over `src/Qlcheck` and binds every `SimpleNameSyntax` to its type,
the same way the planned extractor would.

- **Semantic extraction finds a dependency that `using` scanning misses.** `src/Qlcheck/Scan/SourceScan.cs:5`
  and `:13` use `Qlcheck.SourceFile`. That type is in the parent namespace, which is in scope without a
  `using`. So L4 ("Scan depends on nothing else in qlcheck") is violated today, and ArchitectureTests
  cannot see it. L1–L3, L5 and L6 match exactly. L6's process users are exactly `GitChanges.cs` and
  `CoverageRunner.cs`.
- **Missing implicit usings break resolution.**
  - As qlcheck builds Compilations today (no implicit usings), the probe got 1128 compile errors and
    747 unresolved names.
  - Adding the generated `obj/.../qlcheck.GlobalUsings.g.cs` brought that to 2 errors and 0 unresolved
    names. Resolved references rose from 10,283 to 12,651. That is why this plan depends on Plan 00.
- **Zero-config namespace cycles are noise.** With every raw namespace as a node, all 9 `Qlcheck*`
  namespaces form **one strongly connected component**. The cause: root `Qlcheck` holds both the
  composition root (`QlcheckApp`) and the shared kernel (`Finding`, `SourceFile`). Parent/child
  namespace pairs that depend on each other are normal C#. The tangle is real, but it is not something
  an agent should be told to fix unasked.
- **A declared tangle constraint is clean.** Collapsing to the declared components `Qlcheck.*` (one
  component per direct child namespace, root types excluded) gives an acyclic graph: Languages→{Checks,Scan},
  Report→{Checks,Cli,Scan}, Run→{Checks,Languages,Scan}.
- **Cost.** Binding every name over 89 files took about 2.4–2.9 s, compile included. A full
  `qlcheck --stats src/Qlcheck` takes 4.7 s today. The marginal cost inside qlcheck should be lower,
  because Compilations and semantic models already exist. It must still be measured (Risks).

## Design

### 1. The declaration file: `.qlcheck_architecture`

- **Location.**
  - The file lives at the git root (`RepoRoot.Boundary`). Outside a git repository it is read from the
    scan root, matching ADR 0010's boundary.
  - Scanning `src/` therefore still uses the root file, so `./run .` and `./run src` report the same thing.
  - Where scan paths span several repositories, each repository's file governs only that repository's
    sources. The lookup goes by the boundary of the source file.
- **No combining in the MVP.** It does not combine like ADR 0010 (decision D1). Architecture is one
  whole-repository statement: Constraints name each other's components, and a tangle needs the whole
  graph. Per-folder override and undo semantics would be hard to read and hard to get right.
  A `.qlcheck_architecture` found below the boundary is an exit-2 error. That makes the limit
  explicit and leaves room to add combining later without silently changing meaning.
- **Format.** The format is line-based, like the sibling files. `#` starts a comment, blank lines are
  ignored, and each line is one **Constraint**. Names are **dotted names**, which can be a namespace or a
  fully-qualified type. A name matches itself and everything under it on `.` boundaries, so
  `Qlcheck.Scan` matches `Qlcheck.Scan` and `Qlcheck.Scan.GitChanges.Run`, but not `Qlcheck.Scanner`.

```
# .qlcheck_architecture: the intended architecture. A human owns this file.
# A name matches itself and everything below it.

Qlcheck.Cli                       must-not-use  Qlcheck
Qlcheck.Report                    must-not-use  Qlcheck.Run Qlcheck.Languages
Qlcheck.Run                       must-not-use  Qlcheck.Report Qlcheck.Cli
Qlcheck.Scan                      must-not-use  Qlcheck
Qlcheck.Languages.CSharp.Catalog  must-not-use  Qlcheck.Run Qlcheck.Report Qlcheck.Cli

System.Diagnostics.Process System.Diagnostics.ProcessStartInfo  only-used-by  Qlcheck.Scan.GitChanges Qlcheck.Languages.CSharp.Checks.Coverage.CoverageRunner

no-cycles Qlcheck.*
```

That example is qlcheck's own L1–L6, plus a tangle constraint. It shows the MVP covers everything
ArchitectureTests enforces.

**MVP Constraint kinds (D3):**

| Constraint | Meaning | Check |
|---|---|---|
| `S… must-not-use T…` | Code whose enclosing name matches any `S` must not reference a type whose name matches any `T`. **References inside `S` itself are exempt.** That is what makes `Qlcheck.Scan must-not-use Qlcheck` mean "nothing else in qlcheck". | `architecture-relationship` |
| `T… only-used-by S…` | A type matching `T` may be referenced only from code whose enclosing name matches some `S`, or from inside `T` itself. This replaces L6, and covers "only Infrastructure may use `DbContext` / `HttpClient` / `Process`". | `architecture-relationship` |
| `no-cycles P.*` | Each direct child of `P` (with everything below it) is one component. Types directly in `P` belong to none. Dependencies between these components must form no cycle. | `architecture-tangle` |

**Not in the MVP:**

- outbound allow-lists (`S may-only-use …`), which are very noisy against `System.*`;
- `*` in the middle of a name (`*.Domain`);
- `except` clauses;
- folder or csproj units;
- `no-cycles` over an explicit component list.

Each can be added as a new keyword without breaking existing files.

**Validation is exit 2 with `file:line`, like an unknown id in `.qlcheck_disable`.** It covers:

- an unknown keyword;
- an empty side;
- a name that is not `Ident(.Ident)*`;
- `no-cycles` without a trailing `.*`;
- a deeper file.

A Constraint whose source matches nothing in the scan is **not** an error. Scanning a subfolder makes
that normal. See Risks for the typo exposure.

**Units (D2).** Both sides are matched against dotted names. Namespaces cover the common layouts:

- project-per-layer solutions, whose namespace usually equals the project or RootNamespace;
- folder-per-layer single projects, where namespace usually equals folder;
- type-level gates such as L6.

The same matcher works for targets that come from metadata (BCL, NuGet, a built sibling project), which
have no folder. Folder units would follow ADR 0010's "folder is the scope". But they would need the
declaring file of every target, which is not known for metadata, and they let a file in the wrong
folder slip through either way. csproj units mostly restate what `ProjectReference` already enforces,
and the Finding would sit in a `.csproj`, which the suppression, disable and `--uncommitted` filters
cannot handle.

### 2. No file means no Findings (D4)

- **Both Checks are enabled by default.** If no `.qlcheck_architecture` applies to a source, the
  architecture step is skipped entirely and does no extraction work.
- **This keeps the zero-config pitch honest.** Nothing is required, and the README can say "the one
  thing qlcheck cannot infer is your intended architecture; write it down and it is checked."
- **Inference was rejected.** One example would be namespace-cycle detection with no file. The probe
  shows it would flag qlcheck itself as one 9-namespace tangle on day one. The fix ("split the root
  namespace kernel") is a design call, not an agent task. That contradicts "findings an agent can act on".
- **Trade-off.** Users who never write the file get nothing from these Checks. That is acceptable,
  because the intended architecture is information only a human has.

### 3. Final ids and messages (D5)

The ids have never been runnable, because selecting one exits 2. Renaming or deleting them therefore
breaks no user.

| Id | Fate | Why |
|---|---|---|
| `architecture-relationship` | **keep**. It covers `must-not-use` and `only-used-by`. | It is the natural "this dependency breaks the declared architecture" check. |
| `architecture-tangle` | **keep**. It covers `no-cycles`. | It is a distinct fix: break a cycle, not one forbidden edge. |
| `architecture-component` | delete | "Component is missing" fires spuriously on subfolder scans and `--uncommitted`. It is better as stats output (D12) than as a Finding. |
| `architecture-interface` | delete | Its message duplicates `-relationship`, and it has no defined meaning. |
| `architecture-location`, `-move`, `-rename` | delete | They would need a declared target location or name per type. That is a refactoring plan, not a Constraint, and too vague for an agent to act on safely. |

The catalog messages are the generic fallbacks. Each Finding carries a specific message through
`ReportCustom`, in repo style: an imperative fix, with the Constraint cited by `file:line`.

- `architecture-relationship`:
  - Catalog message: "Do not depend on a component the intended architecture forbids."
  - `must-not-use`: "`Qlcheck.Scan` must not use `Qlcheck.SourceFile` (.qlcheck_architecture:7). Move this
    code to a component that may use it, or move `SourceFile` into one `Qlcheck.Scan` may use."
  - `only-used-by`: "Only `Qlcheck.Scan.GitChanges` and `Qlcheck.Languages.CSharp.Checks.Coverage.CoverageRunner` may
    use `System.Diagnostics.Process` (.qlcheck_architecture:9). Call one of them instead."
- `architecture-tangle`:
  - Catalog message: "Remove this dependency. It makes components depend on each other in a cycle."
  - Custom: "`Qlcheck.Report` → `Qlcheck.Run` closes the cycle Qlcheck.Run → Qlcheck.Report → Qlcheck.Run
    (.qlcheck_architecture:11); it is the weakest link, 2 references. Remove them, or move what both need
    into a component neither depends back on."
- README and CONTEXT state that `.qlcheck_architecture`, like `.qlcheck_disable`, is a human decision.
  An agent never edits it to make Findings go away. The messages leave that out, to stay short, the
  same way `.qlcheck_disable` handles it.

### 4. Dependency extraction (D6; relies on Plan 00)

- **Semantic first.** The extractor walks every `SimpleNameSyntax` that is not inside a `using` or
  namespace-declaration name, and binds it with `SemanticModel.GetSymbolInfo`. It reduces the result
  to a target type:
  - a named type gives that type's `OriginalDefinition`;
  - a member gives its `ContainingType`;
  - a reduced extension method gives the static class it is declared on;
  - a namespace is skipped.

  The type's dotted name is `namespace.Outer.Inner` without arity. This catches what `using` scanning
  cannot see: fully-qualified names, parent-namespace types (the `SourceFile` case), `global using`,
  aliases, `using static`, attributes, generic arguments, base types and `nameof`. `cref`s in doc comments
  live in trivia and are not walked. Unresolved names produce nothing. That matches ADR 0003: never
  report what can't be proven.
- **The source name** is the dotted name of the innermost enclosing named type from
  `GetEnclosingSymbol`, or its namespace. Code in the global namespace matches nothing. `named-namespace`
  already covers that case.
- **`using` directives also count (D6),** syntactically, as a dependency on the namespace they name,
  attributed to the namespaces the file declares. This keeps parity with ArchitectureTests and still
  works when a referenced project is not built and its namespace does not resolve. A file with
  `using Qlcheck.Run;` plus 3 references reports 4 Findings. Each one is a line the agent must change.
- **Implicit and global usings** come from **Plan 00** (`docs/plans/00-compilation-fidelity.md`),
  not from this plan. Without them, every SDK project silently under-reports `System.*` targets, for
  example `HttpClient` in `only-used-by`. This plan does not start until Plan 00 has landed.
- **Scope is run-wide.**
  - *Extraction* happens per Compilation, inside `CatalogRun.Execute`, reusing the Compilation and
    semantic models it already builds. It runs only when an architecture check is selected and a file
    applies, and only over trees in `IncludedFiles`.
  - *Judgement* happens once per run, in a new `ArchitectureEngine` (`CheckClass.Architecture`) called
    from `CSharpLanguage.RunCatalog` after all groups. That matches ADR 0007: "a Check that needs a
    different execution scope gets an engine at that scope."
  - `must-not-use` and `only-used-by` could be per-Compilation. `no-cycles` needs the union, because a
    namespace split across 2 projects can close a cycle no single Compilation sees. Judging everything
    in one place keeps it uniform.
- **Plumbing.**
  - `QlcheckApp` loads `ArchitectureFiles` (new, in `Scan/`, beside `DisableFiles`) from
    `options.Paths`, and fails with exit 2 on parse errors.
  - It passes the result through `CheckRun.Execute(…, architecture)` into
    `ILanguage.Execute(files, checks, RunInputs)`. `RunInputs` is a small record, so the next repo file
    does not change the interface again.
  - The Scan type holds language-neutral dotted-name Constraints, with no Roslyn. That respects L4 and
    lets Catalog depend on Scan, which L5 allows.

### 5. Finding location, ID, granularity (D10)

- **One Finding per (file, line, target type)**, at the first offending name token on that line, so
  `CheckRun.Execute(...)` is one Finding, not two (`CheckRun` and `Execute`).
- A `using` Finding sits on the directive's name.
- IDs are the standard `{check}:{file}:{line}:{column}` from `Finding.At`, nothing new.
- **Tangle edge choice (D9).**
  1. For each strongly connected component, pick the inter-component edge with the fewest references.
     Break ties by ordinal `(source, target)`.
  2. Remove that edge and repeat until the graph is acyclic.
  3. Report every reference of every removed edge.

  The result is deterministic and points at the smallest change. The chosen edge can move when reference
  counts change elsewhere. That is acceptable, because IDs are location-based for every Check anyway.
- **Suppression comments (ADR 0008) and `.qlcheck_disable` work unchanged.** Every Finding is in a
  loaded `.cs` file, so `CheckRun`'s central filters apply. Disabled trees still contribute edges to
  the tangle graph, the same as other cross-file Checks (`CheckRun.cs:42`).
- **`--uncommitted`.** Extraction and judgement still see all loaded files, and only Findings in
  uncommitted files are kept. A relationship Finding sits on the changed reference, so it is reported.
  A tangle whose weakest edge lies in a committed file is dropped. See Risks.

### 6. Baselines (D8)

- **No baseline in the MVP.** The README pitch is "no suppressing-by-config-file rituals". A
  shrink-only baseline is exactly such a file, and it brings problems of its own:
  - keys must not contain line numbers, so they would have to be `(file, source, target)` plus a count;
  - "stale entry" would have to become a Finding located in a non-`.cs` file, which the disable,
    suppression and `--uncommitted` filters cannot place.
- **Adopting on legacy code** goes through one of:
  - narrowing the Constraint (it is the human's declaration);
  - `.qlcheck_disable` for a legacy tree;
  - dated suppression comments, per reference, re-reviewed yearly.
- ArchitectureTests' `Baseline.cs` is the design to copy if real users ask for one. It would be a
  follow-up with its own ADR.

### 7. Output (D12)

When an architecture file applied:

- the JSON gets `"architecture": [{"file": ".qlcheck_architecture", "constraints": 7}]`, omitted when no
  file applied;
- the `--stats` box gets a line.

This follows the `"disabled"` precedent ("a clean run can't be mistaken for an unchecked one"), and it is
where an unmatched-Constraint hint could go later.

## Implementation steps (test-first, small commits)

Each step is red, then green, then commit. `dotnet test` and `./run --human .` must stay clean, apart
from the intended changes. Before step 1, Plan 00 (compilation fidelity) and Plan 01 (placeholder
cleanup) have landed.

1. **Catalog cleanup (D5).**
   - Delete 5 rows from `checks.json`.
   - Plan 01 has already run but did not touch these rows. Update the `CatalogTests` counts relative to
     the post-01 numbers: the catalog count drops by 5, and unimplemented drops to 2.
   - Retarget the exits-two test at `architecture-tangle` (still unimplemented at this step).
   - Update README counts.
2. **`Scan/ArchitectureFiles` parser and locator.**
   - `tests/Qlcheck.Tests/Scan/ArchitectureFilesTests.cs` covers:
     - grammar, comments and blanks;
     - all 3 kinds;
     - each error with `file:line`;
     - git-root lookup from a subfolder scan;
     - scan root outside git;
     - deeper file is an error;
     - 2 repos, 2 files;
     - dotted-name prefix matching (`Qlcheck.Scan` vs `Qlcheck.Scanner`).
3. **Plumbing.**
   - `RunInputs` record (`Architecture`, room for more).
   - Change `ILanguage.Execute`, `CheckRun.Execute` and `RecordingLanguage`.
   - `QlcheckApp` loads and validates the file. A test checks that a malformed file exits 2 with
     `file:line` on stderr, and that a missing file changes nothing.
4. **`DependencyExtractor`** (`Catalog/Queries/` or `Catalog/`).
   - `DependencyExtractorTests` over in-memory Compilations (like `MagicLiteralSharedPatternTests`) cover:
     - fully-qualified reference without a `using`;
     - parent-namespace type (`SourceFile` shape);
     - global, alias and static usings;
     - extension method resolves to its declaring class;
     - attribute, generic argument, base type and `nameof`;
     - a cref is ignored;
     - an unresolved name yields nothing;
     - a `using` directive is reported syntactically;
     - dedupe per (file, line, target type);
     - only `IncludedFiles` are walked.
5. **`ArchitectureEngine`: `architecture-relationship`.**
   - Add `ArchitectureEngine.Rows`, concat them into `CatalogWalker.Implemented` and `Classes`, and set
     `CheckClass.Architecture`.
   - Have `CatalogRun.Execute` return the extracted dependencies (a `CatalogRunResult`) when asked.
   - `CSharpLanguage.RunCatalog` aggregates them and calls the engine.
   - Unit tests cover:
     - `must-not-use` hit, plus the self-exemption (`Qlcheck.Scan must-not-use Qlcheck` lets Scan use Scan);
     - `only-used-by` from an allowed and a forbidden source, plus the self-exemption;
     - exact message text with `file:line`;
     - no file means no extraction (assert through a counter or a spy).
   - The `CatalogTests` fixture loop skips `CheckClass.Architecture`, as it already skips `Process`.
   - End-to-end tests in `QlcheckAppTests` use a temp git repo with a file:
     - exit 1 with Findings, and exit 0 when clean;
     - a `.qlcheck_disable` tree drops the Findings;
     - a suppression comment drops one;
     - `--uncommitted` keeps only the changed file's Finding.
6. **`architecture-tangle`.**
   - Unit tests cover:
     - a 2-cycle;
     - a 3-cycle;
     - 2 overlapping cycles, where greedy removal breaks both;
     - weakest-edge choice and the ordinal tie-break, deterministic across 10 runs;
     - root-`P` types excluded;
     - a cycle spanning 2 csproj groups (end-to-end, 2 temp projects sharing a namespace).
   - Now 0 rows are unimplemented.
7. **Dead code (D11).**
   - Remove `RejectUnimplemented`, `NoImplementation` and the unimplemented branch of `DisabledError`.
   - Replace the "discovered and unimplemented" and "exits two" tests with "every catalog row is implemented".
8. **Output (D12).** Add the JSON `architecture` key and the `--stats` line. Report tests cover both,
   and assert the key is absent with no file.
9. **Docs.**
    - Add a README section "Declaring your architecture" and a class-table row.
    - **Constraint** (and *component*) is already in CONTEXT.md. Reference it from the README. Add
      **Architecture file** and **Tangle** only if they are still missing.
    - Add ADR 0014, and a "superseded in part by 0014" line on ADR 0006.
10. **Perf check.** Run `--stats` on qlcheck with a `.qlcheck_architecture` in a scratch copy, before
    and after. Record the delta in the PR. If it is over ~1 s, prefilter: bind only names whose
    identifier text matches a type-name segment of some Constraint target, or any name when a target
    is a bare namespace.

## Files touched

- **New:**
  - `src/Qlcheck/Scan/ArchitectureFiles.cs`
  - `src/Qlcheck/Languages/RunInputs.cs` (or `Run/`)
  - `src/Qlcheck/Languages/CSharp/Catalog/DependencyExtractor.cs`
  - `src/Qlcheck/Languages/CSharp/Catalog/ArchitectureEngine.cs` (with the tangle algorithm, or `Catalog/Queries/Tangles.cs`)
  - `docs/adr/0014-intended-architecture-file.md`
  - tests: `Scan/ArchitectureFilesTests.cs`, `Catalog/DependencyExtractorTests.cs`,
    `Catalog/ArchitectureEngineTests.cs`
- **Changed:**
  - `checks.json`
  - `CatalogWalker.cs`
  - `CatalogRun.cs`
  - `CSharpLanguage.cs`
  - `ILanguage.cs`
  - `Run/CheckRun.cs`
  - `Run/CheckDiscovery.cs`
  - `QlcheckApp.cs`
  - `Report/Report.cs`
  - `tests/Qlcheck.Tests/Catalog/CatalogTests.cs`
  - `tests/Qlcheck.Tests/Run/CheckRunTests.cs`
  - `tests/Qlcheck.Tests/QlcheckAppTests.cs`
  - Report tests
  - `README.md`
  - `CONTEXT.md` (only for terms not yet defined; Constraint already is)
  - `docs/adr/0006-catalog-checks-are-rows.md`
- **Not touched:** `tests/Qlcheck.ArchitectureTests` (see Dogfooding).

## ADR needed?

**Yes: ADR 0014, "Intended architecture is declared in one `.qlcheck_architecture` file, in dotted names."**
It meets all 3 bars:

- **Hard to reverse.** It is a user-written file format and its location.
- **Surprising.** It is the first Check that needs config, against the "zero config" README. It does
  not combine like ADR 0010. Its units are namespaces, not ADR 0010's folders. It is semantic, not the
  `using` scanning ADR 0009 chose.
- **Real trade-off.** Inference vs declaration, and baseline vs no baseline.

Implicit usings are covered by Plan 00 and its ADR 0011. Cite it in 0014's consequences.
Keep the amendment to ADR 0006: add a "superseded in part by 0014" line, because its sentence that
architecture rows stay discovered and unimplemented no longer holds.

## Risks

- **Unbuilt sibling projects.** Cross-project types resolve only through `bin/**.dll`. When a project is
  not built, references into it go unresolved and are missed. The `using`-directive fallback (D6)
  catches the common case. Fully-qualified and parent-namespace references are still missed. Document
  this. A later fix could reference sibling projects in the scan as `CompilationReference`s, which would
  help every symbol check.
- **Dependency on Plan 00.** Without compilation fidelity, `System.*` targets in SDK projects go
  unresolved, and Constraints on them silently under-report. This plan is scheduled after Plan 00 for
  that reason.
- **Typos make a Constraint a silent no-op**, because unmatched sources are not an error (subfolder
  scans). Mitigation is D12 output now, with an "unmatched in this scan" hint in the stats later.
- **`--uncommitted` can hide a new tangle** whose weakest edge lies in a committed file. The new edge
  usually *is* the weakest (1 reference), but that is not guaranteed. A future fix would let `ILanguage`
  know the reported set so the engine prefers reported edges.
- **Cost.** Binding every name costs about 1–2 s per 100 files in the probe. It is paid only when a
  file exists. Step 10 measures it and holds the prefilter in reserve.
- **Tangle stability.** The weakest-edge choice can flip as code elsewhere grows, which moves the
  Finding. That is acceptable but surprising, so the message names the cycle so the agent understands.

## Effort

About **3–4 focused days** (implicit usings moved to Plan 00):

| Work | Estimate |
|---|---|
| Steps 1–3 (cleanup, parser, plumbing) | ~1 d |
| Step 4 (extractor) | ~1 d |
| Steps 5–6 (engine and tangles, with end-to-end tests) | ~1–1.5 d |
| Steps 7–10 (cleanup, output, docs and ADR, perf) | ~0.5–1 d |

## Dogfooding (note, not planned in depth)

qlcheck's ArchitectureTests could later be replaced by a root `.qlcheck_architecture`, the example in
§1, enforced by `./run .` or by a thin test that runs `qlcheck --check architecture-relationship
--check architecture-tangle src`. Before doing that:

- **L4 fails semantically today** (`Scan/SourceScan.cs` → `Qlcheck.SourceFile`). Either move
  `SourceFile` into `Qlcheck.Scan`, or relax the Constraint. That is a human call to make at
  dogfood time.
- **Self-checking is circular trust.** A bug in the Check would silently relax qlcheck's own layering.
  Keep ArchitectureTests until the Check has shipped and been stable for a while.
- ADR 0009's baseline ratchet has no equivalent here (D8).

## Decisions (resolved)

The user accepted every recommendation on 2026-10-29.

- **D1 – File.** Declare architecture in **one `.qlcheck_architecture` at the git root** (scan root
  outside git). It is line-based, with keyword Constraints (`must-not-use`, `only-used-by`, `no-cycles`)
  and `#` comments. It does **not** combine like ADR 0010, and a `.qlcheck_architecture` in a deeper
  folder is an exit-2 error.
  - *Alternatives:* combine per folder like ADR 0010; JSON format; arrow syntax (`!->`).
- **D2 – Units.** Both sides of a Constraint are **dotted names**: a namespace or a fully-qualified type,
  prefix-matched on `.` boundaries. Folder-glob and csproj units are deferred.
- **D3 – MVP kinds.** Ship exactly `must-not-use`, `only-used-by` and `no-cycles P.*`. There are no
  outbound allow-lists, no mid-name wildcards and no `except` clauses. A Constraint whose source matches
  nothing in the scan is not an error.
- **D4 – No file, no Findings.** With no `.qlcheck_architecture`, nothing is inferred (no automatic
  namespace-cycle detection) and nothing is reported. Both architecture Checks are still enabled by
  default, so they apply as soon as a file exists.
  - *Alternative:* zero-config cycle detection, which probes as one 9-namespace tangle on qlcheck itself.
- **D5 – Ids.** Keep `architecture-relationship` (for `must-not-use` and `only-used-by`) and
  `architecture-tangle` (for `no-cycles`). Delete `architecture-component`, `-interface`, `-location`,
  `-move` and `-rename`.
- **D6 – Extraction.** Dependencies come from **semantic references** (bound through the SemanticModel).
  `using` directives naming a matching namespace also count, syntactically, so the Check works when a
  referenced project is not built and matches ArchitectureTests.
- **D7 – Implicit usings.** Accepted, and moved to Plan 00 (`docs/plans/00-compilation-fidelity.md`, ADR 0011).
  This plan depends on it.
- **D8 – No baseline in the MVP.** Legacy violations are handled by narrowing the Constraint,
  `.qlcheck_disable`, or dated suppression comments. A shrink-only baseline (ArchitectureTests style)
  waits until users ask.
- **D9 – Tangle location.** Break each cycle greedily at its **weakest edge** (fewest references,
  ordinal tie-break) and report every reference on the removed edges. Accept that `--uncommitted` can
  hide a tangle whose weakest edge is in a committed file.
- **D10 – Granularity.** One Finding per (file, line, target type), at the first offending name token.
  A `using` directive gets its own Finding. Standard `{check}:{file}:{line}:{column}` ids.
- **D11 – Remove the unimplemented-row machinery** once both Checks ship: `RejectUnimplemented`, the
  "has no implementation" errors, and the exits-two test. Replace it with an "every catalog row is
  implemented" test, and amend ADR 0006 ("superseded in part by 0014").
- **D12 – Show the applied file.** When an architecture file applied, add a JSON `"architecture"`
  key (file and Constraint count, omitted when none applied) and a `--stats` line, so a clean run can't
  be mistaken for an unchecked one.
