# qlcheck false-positive fix plans

Source: `requisitionops/api/QLCHECK_KNOWN_ISSUES.md` §2 ("Confirmed checker false positives"),
where the requisitionops team empirically verified 11 findings across 10 check ids as real bugs
in qlcheck's own check logic, not codebase problems. Each file below is a standalone fix plan
for one check: root cause (file:line), a concrete proposed code change, a test plan, and a
confidence/risk rating. Nothing in this directory has been implemented yet — these are plans.

| Check | Findings | Root cause | Confidence |
|---|---|---|---|
| [`no-self-assignment`](no-self-assignment.md) | 23 | Matches `Foo = Foo` textually, including inside object initializers between two different objects | Clean, low-risk |
| [`one-statement-per-line`](one-statement-per-line.md) | 24 | Counts raw `;` characters in line text, not aware of string literals | Clean, low-risk |
| [`sealed-no-protected`](sealed-no-protected.md) | 4 | Flags `protected override` on a sealed class, ignoring that overrides must match base accessibility | Clean, trivial |
| [`empty-class`](empty-class.md) | 1 of 2 | "Zero members" includes classes relying on the synthesized default ctor — flags marker exception types | Clean, scoped |
| [`datetime-kind`](datetime-kind.md) | 3 | Row is inverted: matches `DateTime.SpecifyKind(...)` (the fix) instead of un-kinded `DateTime` construction (the problem) | Clean, minimal |
| [`prefer-set-min-max`](prefer-set-min-max.md) | 7 | Matches any `.Min()`/`.Max()` call by name; no receiver-type check; `"Max"` isn't even wired | Needs `ctx.Model`, narrow scope |
| [`jwt-strong-signature`](jwt-strong-signature.md) | 2 | Matches any `.Sign(...)` call by name; false-positive fixable now, "strong cipher" detection itself unimplemented (see `no-fix-yet.md`) | Partial — see file |
| [`sql-injection`](sql-injection.md) | 36 | Matches any `.Execute(...)` call by name; no receiver-type check | Clean, sink-precision fix |
| [`ssrf` / `ssrf-traversal`](ssrf-and-ssrf-traversal.md) | 3 + 4 | Taint treats any non-string parameter (e.g. `Guid`) as attacker input | Clean but touches a function shared by ~19 taint ids — needs full regression pass |
| [`loop-bound-injection`](loop-bound-injection.md) | 1 | "Attacker-reachable" proxy is "not `private`"; doesn't account for `internal`/framework-required accessibility | Trivial (Option A); precise version deferred (Option B) |

See [`no-fix-yet.md`](no-fix-yet.md) for the two things that came up during this investigation
with **no** fix proposed: `jwt-strong-signature`'s actual algorithm-strength detection, and a
systemic risk that ~140 other bare-name-match rows across `SyntaxEngine.cs`/`SymbolEngine.cs`
may share the same false-positive shape as `datetime-kind`/`prefer-set-min-max`/
`jwt-strong-signature` and haven't been audited.

## Suggested sequencing

Land the four "clean, low-risk" fixes first (`no-self-assignment`, `one-statement-per-line`,
`sealed-no-protected`, `empty-class`) — each is a self-contained, single-predicate change with
no shared blast radius. Then `datetime-kind` (also self-contained, but changes the row's
matched shape entirely, so its fixture/tests need a full rewrite, not just a tightened guard).
Then `sql-injection` and `loop-bound-injection` (each touches one row only). Land
`prefer-set-min-max` and `jwt-strong-signature` together since both need the same new
`ctx.Model`-based receiver/symbol pattern — a good pair to review as one PR. Land
`ssrf`/`ssrf-traversal` last and separately, since the `IsParameter` type-check change is shared
by all ~19 `Row.Taint` ids and needs the full `fixtures.json` regression pass called out in that
plan before merging.
