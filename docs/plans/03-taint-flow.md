# Plan 03: real intra-procedural taint and control-flow analysis

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity), and lands after 01, 05 and 02

Probe corpus, scorer and throwaway prototype (all outside the repo):
`/tmp/claude-1000/-home-lmaslanka-projects-qlcheck/1fadbd8a-84fa-480a-a869-298258cc64bb/scratchpad/probes-taint/`
(`corpus/*.cs` = 64 probes, `manifest.json`, `tools/gen.py`, `tools/score.py`, `tools/score_proto.py`,
`tools/proto/` = prototype, `baseline.txt`, `proto-vs-current.txt`).

## Goal

Make the Taint Checks find what they say they find, and make the Flow Checks use real control flow. Today a Taint
Check fires only when a sink's argument is literally a parameter, and it ignores whether a method name is actually that
API. Afterwards:

- Untrusted data is followed through locals, reassignment, `+=`, `StringBuilder`, `string.Format/Concat/Join`,
  collections, `?:`/`??`, `await`, lambdas and local functions, all inside one member. The analysis uses Roslyn's
  `ControlFlowGraph`.
- Every sink is a symbol (containing type + member + parameter name), declared as data in the Check's row
  (ADR 0006/0007 still hold: checks are rows).
- Sanitizers and guards (allow-list checks, `int.Parse`, `Path.GetFileName`, parameterized queries) stop a Finding.
- A Finding names its source and path, so an agent knows what to change.
- `reachable-branch`, `no-infinite-loop` and the three lock rows answer their question from control flow, not from
  statement shape.

Out of scope (user decision): cross-method and cross-file taint. Fields and properties written in one member and
read in another are out too. `null-dereference` belongs to plan 02 (nullable). It is only mentioned here.

## Current state

**Taint rows.** `TaintEngine.cs:8-41` holds 27 rows:

- 22 are `Row.Taint(id, Taint, "<bare method name>")`, for example `"Start"`, `"Write"`, `"Combine"`, `"Find"`,
  `"Search"`, `"Append"`, `"GetType"`.
- 1 is `Row.TaintOn` (`sql-injection`, `TaintEngine.cs:28-32`).
- 4 are `Row.Handle` (`allocation-dos`, `loop-bound-injection`, `ssrf`, `ssrf-traversal`).

`Row.Taint` and `Row.TaintOn` (`Row.cs:112-126`) both route to `Match/TaintMatch.cs`.

- `TaintMatch.Report` (`TaintMatch.cs:13-19`) matches invocations, creations and simple assignments by
  **name only** (`Names.Invocation`, `Names.Creation`, `Names.Member`).
- `IsTainted` (`TaintMatch.cs:108-131`) counts something as tainted only if it is:
  - an identifier that is a parameter of a method with the `public` modifier (`IsParameter`, `:133-155`, matched by
    name text, no symbol);
  - a call named `ReadLine` (`:115`);
  - an interpolated string that contains such a parameter;
  - a `+` chain of the above.

  No locals, no kills, no sanitizers. Parameter type is ignored: `Guid` and `int?` parameters count as tainted, and
  `TaintMatchTests.cs:165,175` locks that in.
- `ReportOnTypes` (`TaintMatch.cs:21-42`) is the only receiver check. It calls `Symbols.DerivesFrom`
  (`Queries/Symbols.cs:48-59`), which compares the **simple** type name: any user class named `DbCommand` matches.
  The real ADO.NET API takes SQL through `CommandText` and constructors, and `ExecuteReader/ExecuteNonQuery` take no
  string. So on compiling ADO.NET code, `sql-injection` cannot fire at all. Probe `sql_readme_shape` confirms
  this. The README example (`command.ExecuteReader(userId)`) only works because the fixture declares its own fake
  `DbCommand` (`fixtures.json` `sql-injection`).
- `SsrfPatterns.cs` has its own source logic. It requires a fully tainted identifier, has safe types and escape
  functions, and follows taint into **same-file private helpers** through call sites (`MaxForwardingDepth = 2`,
  `:23`, `:225-245`). This is the only cross-method analysis that exists today.
- `Patterns.LoopBound` / `AllocDos` (`Patterns.cs:453-456`, `ForUsesParameter` `:966`) use a third source rule:
  "method not explicitly private/internal". That is why the `loop-bound-injection` fixture's `void M(int input)`
  fires.
- Fixtures (`tests/Qlcheck.Tests/Catalog/fixtures.json`, used by `CatalogTests.cs:125`) mostly use calls that do not
  resolve, such as `Start(input);`, `Combine(input);`, `Write(input);`. Every fixture that uses a sink name will need
  a real API or a stub in the real namespace.

**Flow rows.** `FlowEngine.cs:12-24` holds 11 rows, all syntax-shaped:

- `reachable-branch` is `if (false)` with a literal (`StmtFacts.UnreachableIf`, `Queries/StmtFacts.cs:140-148`).
- `no-infinite-loop` is `while (true)` with no `break/return/throw` anywhere under it. The check includes nested
  switches and lambdas (`StmtFacts.cs:125-138, 214-225`).
- `lock-released` and `release-lock-same-method` are `Row.Invoke("Enter", "Monitor")`: **every** `Monitor.Enter`
  is reported, including a correct `try/finally` (`FlowEngine.cs:13,22`).
- `matching-lock-release` reports every `Monitor.Exit` (`:18`), although its message is about
  `ReaderWriterLock` read/write modes.
- Five `for`-shape rows (`StylePatterns.cs:95-108, 451-495`) use text matching such as
  `Contains($"{bound} =")` and `Contains(counter)`.
- `null-dereference` (`Patterns.cs:451,934`) is owned by plan 02.

**Infrastructure.** `WalkContext.Model` (`WalkContext.cs:38`) already exposes a `SemanticModel`, and
`CatalogRun.cs:39` builds one `Compilation` per project group. Ad-hoc runs reference only the
`Microsoft.NETCore.App` TPA (`CSharpCompilations.cs:15-18,72-84`). Project runs add NuGet `compile` assets from
`project.assets.json` (`ProjectReferences.cs:54-77`). **Shared frameworks such as `Microsoft.AspNetCore.App` are
never referenced**, so in a real web project `ControllerBase`, `HttpResponse`, `[FromQuery]` and `ILogger` are
error types. Verified: in `probes-taint/unresolved/Ctl.cs`, `_log.LogInformation(...)` and `Redirect(u)` bind to
`IInvalidOperation`, and `[FromQuery]` is an error type named `FromQuery`. The ASP.NET ref pack is installed under
`~/.local/share/dotnet/packs/Microsoft.AspNetCore.App.Ref`. ADR 0001 ("no semantic model") is stale since 0003/0007.

**Baseline (current release build).** Probe corpus scored with
`python3 -I tools/score.py <qlcheck> <probes-taint>`:

| Set | TP | FP | FN | TN | Precision | Recall |
|---|---|---|---|---|---|---|
| All 64 probes, current | 7 | 13 | 31 | 16 | 0.35 | 0.18 |
| 51 probes on rows the prototype covers, current | 5 | 5 | 26 | 15 | 0.50 | 0.16 |
| Same 51, prototype | 30 | 1 | 1 | 19 | 0.97 | 0.97 |

The prototype numbers are optimistic: I wrote the probes alongside it.

- **Current FPs:** `Worker.Start(name)`, `Url.Combine(seg)`, `Console.Write`/`StreamWriter.Write` (each counted
  for both `reflected-xss` and `no-stack-trace-disclosure`), allow-list guards, `Regex` with a timeout, `Monitor.Enter`
  inside `try/finally`, `Monitor.Exit`.
- **Current FNs:** every local, `CommandText`, `ProcessStartInfo`, `File.ReadAllText`, `ZipArchiveEntry.FullName`,
  `const bool` conditions, `for(;;)`, `break` inside a switch, `return` inside a lambda.
- **Prototype misses:** `sql_private_helper`, which is cross-method and out of scope. `flow_while_true_cancel` is a
  decision (D9).
- **Self-scan:** current qlcheck reports 1 taint finding,
  `path-injection tests/Qlcheck.ArchitectureTests/Support/Baseline.cs:87`. `rule.Id` flows into `Path.Combine`; this
  is an FP. The prototype instead reports `Baseline.cs:113`: a public `directory` parameter goes through
  `GetFullPath` into `File.WriteAllText`, and the `StartsWith` guard there is a deny-check, not an allow-check. That
  is technically true but noisy for test-support code. It is evidence for D1.

**Performance baseline** (this repo: 131 files, 19,057 lines, 32 cores; release build in the scratchpad):

| Run | Time |
|---|---|
| `./run .` (`dotnet run`, includes build check) | 6.4 s |
| Release binary, full run (501 checks) | 5.62–5.69 s |
| Only the 38 Taint+Flow rows (`--check` ×38) | 0.92 s |
| One trivial check (process + parse + compile floor) | 0.45 s |

So Taint+Flow cost about 0.47 s today. Catalog walking is sequential per compilation; only the scan is parallel
(`Scan/SourceScan.cs:11`).

Prototype cost, measured in a separate process on the same repo (145 `.cs` files, 1,516 bodies):

| Mode | Bind (`GetOperation`) | CFG | Analysis |
|---|---|---|---|
| Cold, all bodies | 1.4 s | 0.1 s | 0.19 s |
| Warm (JIT'd), all bodies | 0.65 s | 0.04 s | 0.12 s |
| Warm, with sink-name prefilter (92 bodies) | 0.22–0.26 s | 0.007 s | 0.03 s |

Binding dominates. In a full qlcheck run, Symbol rows already bind many bodies, and the `SemanticModel` caches that
binding.

## Design

### 1. Shape: one shared analysis per body, rows stay data

```
Catalog/Dataflow/
  BodyGraphs.cs      lazy per-file cache: executable body -> ControlFlowGraph (+ local-function / lambda sub-graphs)
  TaintLabel.cs      [Flags] enum: Untrusted, Numeric, FileName, ContainedPath, ArchiveEntry, ExceptionDetail
  TaintSpec.cs       row data: Sink[] + accepted labels (+ optional row-scoped sources)
  TaintModel.cs      the shared knowledge tables: sources, propagators, sanitizers (label transforms), guards
  TaintFlow.cs       worklist analysis over one CFG -> SinkHit(sink, labels, origin, via[])
  TaintFlowMatch.cs  Row.Flow(...) predicate: filters the cached SinkHits for its own sinks/labels and reports
  LockFlow.cs        held-lock analysis over the CFG for the three lock rows
  Reach.cs           AnalyzeControlFlow helpers for reachable-branch / no-infinite-loop
```

- A Taint row becomes `Row.Flow(id, CheckClass.Taint, TaintSpec)`. The spec is a few lines of C# in
  `TaintEngine.cs`, so "a shared predicate is a line in that engine's table" still holds. `checks.json` keeps only
  `id`/`message`, as `CatalogTests.Catalog_has_only_id_and_message` requires.
- `TaintFlow` runs **once per body for the union of selected rows**. It records every hit on any known sink, and each
  row then filters those hits. Twenty-five rows do not mean twenty-five passes.
- `WalkContext` gets one lazy member, for example `ctx.Dataflow`, that owns `BodyGraphs` and the cached hits.
  Layering rule L5 is unaffected, since everything stays under `Catalog/`.

Example row:

```csharp
Row.Flow("sql-injection", CheckClass.Taint, new TaintSpec(
    Accepts: TaintLabel.Untrusted,
    Sinks:
    [
        Sink.Set("System.Data.Common.DbCommand", "CommandText"),
        Sink.Set("System.Data.IDbCommand", "CommandText"),
        Sink.Ctor("System.Data.Common.DbCommand", "cmdText"),          // any derived ctor with that parameter
        Sink.Call("Dapper.SqlMapper", "Query*|Execute*", "sql"),
        Sink.Call("Microsoft.EntityFrameworkCore.RelationalQueryableExtensions", "FromSqlRaw", "sql"),
    ])),
```

### 2. Sinks are symbols

`Sink.Call/Set/Ctor/Index(type, member, parameterName)` match like this:

- The type is the containing type's fully-qualified **metadata** name, matched against `OriginalDefinition`. It also
  matches derived types and implemented interfaces. A new `Symbols.Is(type, "Ns.Type")` compares qualified names,
  replacing the simple-name `DerivesFrom` for this use.
- The member is a method name with an optional `*` suffix, a property name for setters, or `.ctor`.
- The parameter is matched by **name**, not position, so overloads stay correct. Overload resolution then gives
  barriers for free: `Regex(string, RegexOptions, TimeSpan)` has `matchTimeout`, which the row excludes;
  `FromSql(FormattableString)` is not `FromSqlRaw`; a logger's `params object[] args` are not the `message`
  template.
- Assignment sinks include object-initializer members (`new ProcessStartInfo { FileName = x }`) and nested receivers
  (`p.StartInfo.Arguments = x`).

The proposed sink table for all migrated rows is in **Appendix A** (D4).

**Unresolved code.** When the receiver or method does not bind (`IInvalidOperation`, error-type receiver), fall back
to the row's name plus the error type's simple name. For example, `ILogger.LogInformation` matches when the receiver
is error type `ILogger<T>`. This keeps no-restore and ASP.NET-without-refs runs useful (D2). Referencing the ASP.NET
ref pack in project mode, so that most web sinks resolve properly, is owned by Plan 00
(`docs/plans/00-compilation-fidelity.md`, ADR 0011). This plan relies on it and adds nothing to the compilation.

### 3. Sources

Sources are **seeded into the entry state**, not checked at each reference. The prototype showed why: if a parameter
is re-checked as a source wherever it is used, a guard can never clean it. Proposed source set (D1):

- Parameters of methods with the `public` modifier (today's rule, kept so existing behaviour and fixtures carry over).
  Safe types get **no** label: numeric types, bool, enums, `Guid`, `DateTime*`, `TimeSpan`,
  `CancellationToken`, and `Nullable<>` of those. The exception is that numeric parameters get the `Numeric` label,
  which only `allocation-dos` and `loop-bound-injection` accept.
- ASP.NET entry points, whatever the accessibility:
  - parameters carrying `[FromQuery|FromRoute|FromForm|FromBody|FromHeader]`;
  - action parameters on classes deriving from `ControllerBase` or marked `[ApiController]`;
  - reads of `HttpRequest.Query/Form/Headers/Cookies/RouteValues/Path/QueryString/Body`.
- `Console.ReadLine` / `ReadLineAsync` (today), and `TextReader.ReadLine` on `Console.In`.
- Row-scoped sources: `ZipArchiveEntry.FullName` and `TarEntry.Name` get `ArchiveEntry` (only `zip-slip` accepts it).
  `Exception.StackTrace`, `Exception.ToString()` and `Exception.Message` get `ExceptionDetail` (only
  `no-stack-trace-disclosure` accepts it, which finally gives that row a real meaning).
- Not sources by default: environment variables, file/stream contents, database reads, `Main(string[] args)`.

### 4. Propagation (transfer functions over the CFG)

The state is a map from symbol to `TaintLabel`. Keys are locals, parameters, CFG `CaptureId`s, and `this.field`
inside the same member. At join points the analysis takes the union (may-taint). A worklist runs to a fixpoint; the
lattice is finite, and there is a cap of 50×blocks iterations, after which the body is reported as nothing.

| Construct | Rule |
|---|---|
| `x = e` (local, param, `this.f`) | `x := taint(e)`. A clean right-hand side **kills**. |
| `x += e`, `??=` | `x := x ∪ taint(e)` |
| `a + b` on strings, interpolation holes | union of operands. Holes of safe types are ignored. |
| `?:`, `??`, `switch` expressions | Union of the branches. Roslyn lowers these to `IFlowCapture`/`IFlowCaptureReference`, so capture IDs must be tracked. The prototype does this. |
| `await e` | `taint(e)` (`Task<string>` from a source stays tainted) |
| Conversions | operand taint, except a conversion to a safe type, which goes to ∅ (or to `Numeric` for numeric targets) |
| `string` instance/static methods, `StringBuilder.*`, `string.Format/Concat/Join`, `Path.Combine/Join/GetFullPath`, `Uri` ctor, LINQ `ToList/ToArray/Select` (receiver) | union of receiver and arguments |
| `sb.Append(x)`, `list.Add(x)`, `AddRange`, `Insert`, `dict[k] = x` | taints the **receiver's root local**, even through chains (`sb.Append(a).Append(b)`) |
| `foreach`, `GetEnumerator`, `.Current`, indexers, array elements | element taint = collection taint |
| `new T(...) { P = v }` | checks constructor and initializer sinks. The result is tainted only for `string`, `StringBuilder` or `Uri`. |
| Any other call | **no propagation** (D5) |
| Lambdas and local functions | analyzed as their own sub-CFGs (`GetAnonymousFunctionControlFlowGraph`, `GetLocalFunctionControlFlowGraph`). Entry state = union of the enclosing body's states (flow-insensitive capture). Local-function parameters are seeded from the taint at in-member call sites (D6). |

A same-file private-method summary would catch `sql_private_helper`, the common `BuildQuery(id)` helper and SSRF's
existing forwarding. It is explicitly **not core**. The hook point is "Any other call", where a summary lookup by
`IMethodSymbol` would go.

### 5. Sanitizers and guards (minimum set, D7)

**Sanitizers as label transforms.** One pass serves every row because a sanitizer changes the label instead of
deleting it:

| Call | Label change |
|---|---|
| `int/long/short/byte/decimal/double/Guid/DateTime(.Offset)/TimeSpan/Enum` `.Parse/TryParse(out)`, `Convert.ToInt*`, numeric casts | Untrusted → Numeric |
| `Path.GetFileName` | Untrusted → FileName (path rows don't accept it) |
| `Uri.EscapeDataString`, `WebUtility.UrlEncode/HtmlEncode`, `HtmlEncoder.Encode` | cleared for the URL/XSS rows |
| `Regex.Escape` | cleared for `regex-dos` |
| `Math.Min/Clamp` with a constant bound | Numeric → ∅ |

**Edge guards** come from the CFG's conditional successors. When a `BranchValue` holds or fails on an edge, the
guarded symbol's labels change on that edge only:

- `x == CONST` and `x != CONST` (including chains like `a != "ls" && a != "pwd"`, which Roslyn lowers into separate
  blocks; the prototype cleans these correctly).
- `READONLY_OR_CONST_SET.Contains(x)`.
- `full.StartsWith(root)` where `full` came from `Path.GetFullPath`: Untrusted/ArchiveEntry → ContainedPath, so path
  rows and `zip-slip` are clean.
- `Url.IsLocalUrl(x)`: clean for `open-redirect`.
- `x < CONST` / `x <= CONST`: Numeric → ∅ on the guarded edge.

**Parameterized queries** need nothing special: `DbParameter.Value` is not a sink.

**Not barriers** in the minimum set: user validators (`if (!IsValid(x)) return;`), non-constant regex checks,
`string.IsNullOrEmpty`.

### 6. Flow rows: which use the CFG, which stay syntactic

| Row | Plan |
|---|---|
| `reachable-branch` | `model.AnalyzeControlFlow(if.Statement).StartPointIsReachable == false` while the `if` itself is reachable. Catches `const bool` and folded constants. Prototype: `flow_if_const_false` TP, `flow_if_runtime` TN. |
| `no-infinite-loop` | `AnalyzeControlFlow(loop)`: start reachable, end unreachable, `ExitPoints` empty, and no `throw`/`yield` in the body (lambdas excluded). Catches `for(;;)`, `do..while(true)`, `break` that only leaves a nested `switch`, and `return` inside a lambda. The cancellation/await case is D9. Unconditional recursion ("…and recursions" in the message) comes later: every CFG path from entry reaches a self-call before exit. |
| `lock-released` | `LockFlow`: forward "held locks" set keyed by the lock expression's symbol. `Monitor.Enter/TryEnter(lockTaken)`, `SpinLock`, `ReaderWriterLockSlim.Enter*Lock`, `Mutex.WaitOne`, `SemaphoreSlim.Wait` add to the set; the matching `Exit`/`Release` removes. A lock still held at the `Exit` block or at a `Return` branch is a Finding. `finally` regions are modelled by the CFG, so `try/finally` is clean (D10). |
| `release-lock-same-method` | The same lattice: acquired in this body and released on no path in this body. |
| `matching-lock-release` | The same lattice with a mode (read/write/upgradeable) per `ReaderWriterLock(Slim)` symbol. `Exit<Mode>Lock` while only the other mode is held is a Finding. `Monitor.Exit` is never reported. |
| `invariant-loop-bound`, `loop-must-change-counter` | Stay syntactic, but swap the text `Contains` for `AnalyzeDataFlow(body).WrittenInside` / incrementor symbols. This cheap fix stops `bound =`-in-a-string matches. Optional, in the last phase. |
| `loop-condition-reachable`, `loop-counter-direction`, `loop-more-than-once` | Stay syntactic (the shapes are correct for what they claim). |
| `null-dereference` | Plan 02. If plan 02 needs it, `BodyGraphs` can be shared. |
| `unused-assignment` (Symbol row) | Could later use `AnalyzeDataFlow`. Not in this plan. |

### 7. Finding location and message (D8)

- **Location = the sink** (the invocation, assignment or initializer member). This keeps stable IDs and suppression
  comments on the line the agent edits. One Finding per (row, sink node) however many sources reach it.
- **Message** = the `checks.json` message plus a detail built from the `SinkHit`:
  `` `userId` (parameter of public `DeleteUser`, line 6) reaches `DbCommand.CommandText` via `query`. ``
  followed by a per-row fix hint kept in the row spec, for example
  "Pass it as a `DbParameter` instead of concatenating it", "Use `ProcessStartInfo.ArgumentList` and a fixed
  `FileName`", or "Resolve with `Path.GetFullPath` and check it starts with the base directory".
  This uses the existing `ctx.Report(id, node, detail)` (`WalkContext.cs:464`), so the JSON schema does not change.
- `replacement` stays null for taint (no safe mechanical fix).

### 8. Performance budget

| Run | Budget |
|---|---|
| Full run on this repo | ≤ +10 % (≤ 6.2 s vs 5.65 s) |
| The 38 Taint+Flow rows only | ≤ 1.4 s (vs 0.92 s) |

Measured with the release binary by a scratchpad script, three runs each, median.

Levers, in order:

1. **Syntactic prefilter.** Build or bind a body only if one of its identifier tokens is a sink member name from the
   selected rows, including fallback names. Measured: 1,516 → 92 bodies, warm bind 0.65 s → 0.24 s.
2. Lazy `BodyGraphs`, shared by Taint, Lock and plan 02.
3. Iteration and size caps: skip bodies over N operations, which means no Finding (prefer an FN to a timeout).
4. If still over budget, run per-file catalog walks in parallel in `CatalogRun` (orthogonal, would help every
   engine).

`AnalyzeControlFlow` for the flow rows runs only on `if` and loop statements of the selected rows, and is cheap.

## Implementation steps (test-first, one commit each, value early)

### Phase 0: harness (0.5 d)

1. Add `tests/Qlcheck.Tests/Catalog/Dataflow/taint-probes.json`: the 64 probes as `{name: {expect, checks, source}}`.
   Add a theory `TaintProbeTests` that runs `CatalogWalker.Walk` with the probe's checks and asserts exactly
   `expect`. Mark currently failing probes `Skip = "phase N"`, so the file shows the target and each phase un-skips
   its probes.
2. Add a scratchpad timing script (not in the repo) that records the three numbers above.

### Phase 1: engine core, `sql-injection` and `command-injection` (2–3 d)

These two rows go first because they are the two FNs that were verified.

3. `Symbols.Is(type, qualifiedName)` (qualified, derived and interfaces) and `Symbols.Matches(member, Sink)`, with
   tests in `Queries/SymbolsTests.cs`.
4. `TaintLabel`, `TaintSpec`, `Sink.*` record types and `Row.Flow`. Add a `RowTests` case that a Flow row carries
   its spec; no behaviour yet.
5. `BodyGraphs`: create the CFG for methods, constructors, accessors, operators, expression-bodied members and
   top-level statements, plus sub-graphs. Catch `ArgumentException` and skip. Test: a body count per kind, and that
   the prefilter skips bodies without sink tokens.
6. `TaintFlow` core: seeded public parameters with safe-type exclusion, locals, kill on clean reassignment,
   string `+` and interpolation, property-set, call-argument and constructor sinks. Un-skip `sql_readme_shape`,
   `sql_local_concat`, `sql_local_interp`, `sql_reassign_kill`, `sql_constant_only`, `sql_guid_param`,
   `sql_private_method_only`, `sql_name_lookalike`.
7. Switch the `sql-injection` row to `Row.Flow`. Rewrite its `fixtures.json` entry to real `System.Data.Common`
   (`cmd.CommandText = "..." + input`). Delete `TaintMatch.ReportOnTypes`, `Row.TaintOn` and their tests.
7a. **README example.** Once the real sinks land, update the README's `sql-injection` example. Today it relies on a
   fake `DbCommand` whose `ExecuteReader(string)` does not exist on the real type. Replace the `Sample.cs` code with
   compiling ADO.NET code that flows `userId` through a local into `command.CommandText`. Re-run
   `qlcheck --human Sample.cs` and paste its real output (line/column, the message with its source detail, the
   `--stats` box). Also rewrite the sentence after the example: it should say the receiver resolves to
   `System.Data.Common.DbCommand` and `userId` is traced through `query`.
8. Switch the `command-injection` row, with object-initializer and nested-receiver sinks and the shell-`-c` rule
   from Appendix A. Un-skip the `cmd_*` probes except the guard probe. Rewrite the fixture.
9. Message detail (source, line, via chain, fix hint), with tests on the message text.

### Phase 2: propagation breadth (2 d)

One commit per row of the table in Design §4, each un-skipping its probes:

- compound assignment;
- flow captures (ternary and coalesce);
- `StringBuilder` and receiver mutation;
- `string.Format/Concat/Join` and `Path.*` propagators;
- collections and `foreach`;
- `await`;
- `this.field` within a member;
- lambdas and local functions (if D6 is accepted).

### Phase 3: barriers (1.5 d)

- Label transforms (Parse/Convert, `GetFileName`, encoders) → `sql_int_parse`, `path_getfilename`.
- Edge guards (equality to a constant, readonly-set `Contains`) → `cmd_allowlist_guard`, `sql_allowlist_column`.
- The `StartsWith`-after-`GetFullPath` guard → `path_startswith_guard`, `zip_entry_guarded`.
- `IsLocalUrl` and the numeric upper-bound guard.

### Phase 4: migrate the remaining rows (3–4 d)

One commit per row (or per pair, when the rows share sinks), in this order:

1. `path-injection` and `filesystem-oracle` (Exists-family owned by oracle, so no duplicate Findings);
2. `xpath-injection`, `xml-injection`, `xslt-injection`;
3. `reflection-injection`;
4. `regex-dos`;
5. `untrusted-environment-variable`;
6. `connection-string-injection`;
7. `argument-injection` and `command-argument-injection`;
8. `ldap-injection`, `nosql-injection`, `dynamic-code-injection`, `deserialization-injection`;
9. `log-injection`;
10. `reflected-xss`, `open-redirect`, `untrusted-session-cookie`, `no-stack-trace-disclosure`;
11. `zip-slip`;
12. `allocation-dos` and `loop-bound-injection` (move from `Handle` to `Row.Flow` with the `Numeric` label).

Each commit rewrites that row's fixture to a real API, or to a stub in the real namespace for packages that are not
in the TPA. Then:

13. ASP.NET sources (D1). Plan 00 has already made ASP.NET types resolve in project mode.
14. Unresolved-call fallback (D2), for ad-hoc and no-restore runs.

### Phase 5: Flow rows (1.5–2 d)

This plan owns the three lock rows (`lock-released`, `matching-lock-release`, `release-lock-same-method`). The
placeholder cleanup (Plan 01) keeps them in the catalog for this plan to fix rather than deleting them. Until this
phase lands they keep today's `Row.Invoke` behaviour.


Independent of phases 1–4; it can run in parallel or go first as a quick win.

- `Reach.cs`: `reachable-branch`, then `no-infinite-loop` (with D9 applied). Update the `StmtFacts.UnreachableIf`
  and `InfiniteWhile` callers and keep their unit tests as the syntactic subset.
- `LockFlow` with `lock-released`, then `release-lock-same-method`, then `matching-lock-release`. Rewrite the
  fixtures: today's bad fixtures `Monitor.Enter();` do not even compile, and the good fixtures must be
  `try/finally`.
- Optional: `WrittenInside` for `invariant-loop-bound` and `loop-must-change-counter`.

### Phase 6: cleanup and docs (0.5 d)

- Delete `TaintMatch.cs`, `Row.Taint`, `TaintMatchTests.cs`, and any `Patterns.LoopBound/AllocDos` helpers left
  unused.
- Update README: the claim "Semantic, not textual", the example, and the "Taint" row of the class table (counts
  unchanged).
- Add `Source`, `Sink` and `Barrier` to `CONTEXT.md` (glossary; "Taint label" only if agents will see it in
  messages).
- Add ADR 0013, and mark ADR 0001 superseded.

## Files touched

- **New:** `src/Qlcheck/Languages/CSharp/Catalog/Dataflow/{BodyGraphs,TaintLabel,TaintSpec,TaintModel,TaintFlow,TaintFlowMatch,LockFlow,Reach}.cs`
- **Edited:**
  - `Catalog/TaintEngine.cs` (all rows)
  - `Catalog/FlowEngine.cs` (5 rows)
  - `Catalog/Row.cs` (+`Flow`, −`Taint`/`TaintOn`)
  - `Catalog/WalkContext.cs` (lazy dataflow cache)
  - `Catalog/Queries/Symbols.cs` (+`Is`)
  - `Catalog/Queries/StmtFacts.cs` (callers)
  - `Catalog/Patterns.cs` and `Catalog/StylePatterns.cs` (remove migrated helpers)
- **Deleted:** `Catalog/Match/TaintMatch.cs`
- **Unchanged:** `Catalog/SsrfPatterns.cs`, unless D11 is answered "migrate"
- **Tests:**
  - new `tests/Qlcheck.Tests/Catalog/Dataflow/{TaintProbeTests.cs,taint-probes.json,TaintFlowTests.cs,LockFlowTests.cs,ReachTests.cs}`
  - `Catalog/fixtures.json` (about 27 taint and 5 flow entries)
  - `Queries/SymbolsTests.cs`, `RowTests.cs`
  - delete `Match/TaintMatchTests.cs`
- **Docs:** `README.md`, `CONTEXT.md`, `docs/adr/0013-taint-is-intra-procedural-symbol-dataflow.md`

## ADR needed?

**Yes, one: ADR 0013, "Taint Checks are intra-procedural dataflow over Roslyn's CFG, with sinks resolved by symbol."**

- **Hard to reverse:** all 27 Taint rows, their fixtures and the row format change shape.
- **Surprising:** a Check that used to fire on any method named `Start` now stays silent unless the call resolves to
  `System.Diagnostics.Process`. Results depend on references resolving, which ADR 0001 explicitly avoided.
- **Real trade-off:** precision against recall in no-restore and ad-hoc runs. D2's fallback is the mitigation that
  should be recorded.

The ADR should also say that cross-method taint is out by decision, and why. ADR 0001 gets a "Superseded by 0003/0007/0013" line.

No ADR is needed for the lock or reachability changes. They are plain bug fixes within the existing Flow engine.

## Risks

- **References not resolving** (ad-hoc runs, missing restore, shared frameworks, Dapper/EF/Mongo/Serilog not
  restored). Sinks resolved by symbol go silent. Mitigations: D2 fallback, Plan 00's reference resolution (ASP.NET ref pack), and a probe for each fallback.
- **Fixture churn.** About 30 `fixtures.json` entries use bare names that do not resolve. Some unit tests encode
  behaviour that changes (`TaintMatchTests.cs:165,175` flag `Guid`/`int?`).
- **Probe overfitting.** The corpus was written alongside the prototype. Before switching each row, check real-code
  precision with D12.
- **CFG edge cases.** Flow captures, `using`/`lock` lowering, `foreach` over spans, `yield`, top-level statements,
  invalid code. Mitigations: catch-and-skip in `BodyGraphs`, a probe per construct, iteration caps.
- **Performance.** Binding is the cost (about 0.65 s warm for every body on this repo). The prefilter is necessary,
  not optional. Watch large generated files.
- **Behaviour split.** SSRF keeps its own source rules and private-helper forwarding. It is temporarily inconsistent
  with the rest (D11).
- **Stable-ID changes.** `sql-injection` moves from the `Execute*` call to the `CommandText` assignment. Existing
  suppressions for that check would resurface. Low impact, because it could not fire on real code before.
- **Coordination with plan 02.** That plan removes `null-dereference` from `FlowEngine`. Keep this plan's FlowEngine
  edits row-local to avoid conflicts. `BodyGraphs` is the shared seam if plan 02 needs one.

## Effort

| Phase | Effort |
|---|---|
| 0 | 0.5 d |
| 1 | 2–3 d |
| 2 | 2 d |
| 3 | 1.5 d |
| 4 | 3–4 d |
| 5 | 1.5–2 d |
| 6 | 0.5 d |
| D12 triage | +1 d per external repo |

Total: **about 11–14 focused days (L)**. The first usable value (sql/cmd through locals, by symbol) lands after
phases 0–1, in about 3 days. The flow-row fixes in phase 5 are independent and take about 2 days.

## Decisions (resolved)

The user settled these on 2026-10-29.

- **D1 Sources: (b).** Untrusted means:
  - parameters of methods with the `public` modifier. Safe types (numbers, bool, enums, `Guid`, dates, `TimeSpan`,
    `CancellationToken`) carry no label. Numeric parameters carry `Numeric`, which only the allocation and
    loop-bound rows accept.
  - ASP.NET entry points: `[From*]` parameters, controller-action parameters, and
    `HttpRequest.Query/Form/Headers/Cookies/RouteValues/Path/Body`.
  - `Console.ReadLine`.
  - row-scoped sources: `ZipArchiveEntry.FullName` for `zip-slip`, and `Exception.StackTrace/ToString()` for
    `no-stack-trace-disclosure`.

  Environment variables, files, streams, DB reads and `Main` args are not sources.
- **D2 Unresolved sinks: (b).** Only when a call does not bind, match it by the row's member name plus the error
  type's simple name.
- **D3 ASP.NET reference pack: moved to Plan 00** (`docs/plans/00-compilation-fidelity.md`, ADR 0011). This plan
  depends on it.
- **D4 Sink table: accepted.** Appendix A is the starting table. `Path.Combine` is a propagator. `Exists` belongs to
  `filesystem-oracle`. Shell `-c` arguments are `command-injection`. Only the logger message template is a sink.
- **D5 Unknown calls: (a).** No propagation, with explicit summaries for string, `StringBuilder`, `Path`, `Uri`,
  LINQ and collections.
- **D6 Lambdas and local functions: included** as sub-graphs, with flow-insensitive capture and local-function
  parameters seeded from call sites.
- **D7 Barriers: accepted.**
  - Label sanitizers: Parse/Convert/casts, `Path.GetFileName`, URL/HTML encoders, `Regex.Escape`, `Math.Min/Clamp`.
  - Edge guards: `== const`, readonly/const-set `Contains`, `StartsWith` after `GetFullPath`, `Url.IsLocalUrl`,
    `< const` for numeric rows.
  - Not barriers: user validators and regex allow-lists.
- **D8 Location and guidance: accepted.** The Finding is at the sink. The message gets a detail with source, line,
  via-locals and a per-Check fix hint. The JSON schema does not change.
- **D9 `no-infinite-loop`: (b).** A loop whose body uses a `CancellationToken` or contains `await` is not reported.
- **D10 `lock-released`: (a).** Only normal paths count, so `Enter; try {…} finally {Exit}` is clean.
- **D11 SSRF: unchanged.** `ssrf` and `ssrf-traversal` stay on `SsrfPatterns`.
- **D12 Real-code triage: accepted.** Before switching each Taint row, clone 2–3 public ASP.NET repos into the
  scratchpad, run old against new, and hand-triage the difference. Target: at least 80 % of new Findings are real.

## Appendix A: proposed sink table (symbol-resolved; parameter by name)

All rows accept `Untrusted` unless noted.

| Check | Sinks |
|---|---|
| sql-injection | `DbCommand.CommandText` / `IDbCommand.CommandText` set; `DbCommand`-derived ctor `cmdText`; `DbDataAdapter`-derived ctor `selectCommandText`; `Dapper.SqlMapper.Query*/Execute*` `sql`; EF Core `FromSqlRaw`, `ExecuteSqlRaw(Async)` `sql` (`FromSql`/`ExecuteSql` with `FormattableString` are not sinks) |
| command-injection | `Process.Start` `fileName`; `ProcessStartInfo.FileName` set; `ProcessStartInfo` ctor `fileName`; plus `arguments`/`Arguments` when `FileName` is a constant shell (`sh`, `bash`, `zsh`, `cmd(.exe)`, `powershell`, `pwsh`) |
| command-argument-injection | `ProcessStartInfo.Arguments` set; `Process.Start`/`ProcessStartInfo` ctor `arguments`, when not a shell (shell cases belong to command-injection) |
| argument-injection | `ProcessStartInfo.ArgumentList.Add/Insert`; `Process.Start(string, IEnumerable<string>)` `arguments` |
| connection-string-injection | `DbConnection.ConnectionString` set; `DbConnection`-derived ctor `connectionString`; `DbConnectionStringBuilder.ConnectionString` set |
| path-injection | `System.IO.File.*`, `Directory.*` (except `Exists`) path parameters; `FileStream`/`StreamReader`/`StreamWriter`/`FileInfo`/`DirectoryInfo` ctor `path`/`fileName`; `ControllerBase.PhysicalFile` `physicalPath`. Accepts `Untrusted` only (not `FileName`/`ContainedPath`). |
| filesystem-oracle | `File.Exists`, `Directory.Exists` `path`; `FileInfo.Exists` on a tainted ctor |
| zip-slip | `ZipFileExtensions.ExtractToFile` `destinationFileName`; `File.Create/Open/WriteAll*`, `FileStream` ctor. Accepts `ArchiveEntry` only. |
| xpath-injection | `XmlNode.SelectNodes/SelectSingleNode` `xpath`; `XPathNavigator.Select/Evaluate/Compile`, `XPathExpression.Compile` `xpath` |
| xml-injection | `XmlDocument.LoadXml` `xml`; `XDocument.Parse`/`XElement.Parse` `text`; `XmlWriter.WriteRaw` `data` |
| xslt-injection | `XslCompiledTransform.Load(string stylesheetUri)`; `Load(XmlReader)` from a tainted reader |
| reflection-injection | `Type.GetType` `typeName`; `Assembly.GetType` `name`; `Assembly.Load/LoadFrom(string)`; `Activator.CreateInstance(string, string)`; `Type.GetMethod/GetProperty/GetField/InvokeMember` `name` |
| regex-dos | `Regex` ctor and static `IsMatch/Match/Matches/Replace/Split/Count` `pattern`, only overloads **without** `matchTimeout` |
| deserialization-injection | `IFormatter.Deserialize` (`BinaryFormatter`, `SoapFormatter`, `NetDataContractSerializer`), `LosFormatter`/`ObjectStateFormatter.Deserialize`; `XmlSerializer` ctor `type` from a tainted `Type.GetType` |
| dynamic-code-injection | `CSharpScript.EvaluateAsync/RunAsync/Create` `code`; `DataTable.Compute` `expression`; `System.Linq.Dynamic.Core` `DynamicExpressionParser.ParseLambda` / string-predicate `Where` |
| ldap-injection | `DirectorySearcher.Filter` set; `DirectorySearcher` ctor `filter`; `SearchRequest` ctor `ldapFilter`; `DirectoryEntry` ctor `path` |
| nosql-injection | `MongoDB.Bson.BsonDocument.Parse` `json`; `JsonFilterDefinition<T>` ctor `json`; `IMongoDatabase.RunCommand<T>(string)` |
| log-injection | `Microsoft.Extensions.Logging.LoggerExtensions.Log*` `message`; `ILogger.Log` `state`/formatter; `Serilog.ILogger`/`Serilog.Log` `Verbose..Fatal` `messageTemplate` (the args array is not a sink) |
| reflected-xss | `HttpResponseWritingExtensions.WriteAsync` `text`; `ContentResult.Content` set; `ControllerBase.Content` `content`; `HtmlString` ctor; `IHtmlHelper.Raw` |
| open-redirect | `ControllerBase.Redirect/RedirectPermanent*` `url`; `HttpResponse.Redirect` `location`; `RedirectResult` ctor `url`; `Results.Redirect` `url` (`LocalRedirect` is not a sink; `Url.IsLocalUrl` guard) |
| untrusted-session-cookie | `IResponseCookies.Append` `value`; `CookieOptions`-carrying overloads |
| untrusted-environment-variable | `Environment.SetEnvironmentVariable` `value`; `ProcessStartInfo.Environment[...]`/`EnvironmentVariables[...]` set |
| no-stack-trace-disclosure | the reflected-xss sinks plus `ControllerBase.Ok/BadRequest/Problem(detail)/StatusCode(.., value)` and `ObjectResult` ctor. Accepts `ExceptionDetail` only. |
| allocation-dos | array-creation size; `List<T>`/`StringBuilder`/`Dictionary` ctor `capacity`; `new string(char, count)`; `ArrayPool.Rent` `minimumLength`; `Enumerable.Repeat/Range` `count`. Accepts `Untrusted` and `Numeric`. |
| loop-bound-injection | the bound operand of a `for`/`while` relational condition. Accepts `Untrusted` and `Numeric`. |
| ssrf, ssrf-traversal | unchanged (D11) |
