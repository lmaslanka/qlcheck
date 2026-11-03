# Plans

Agreed 2026-10-29. Each plan states its dependencies and records its resolved decisions. Land them in this order.

| # | Plan | Depends on | ADR |
|---|---|---|---|
| 00 | [Compilation fidelity](00-compilation-fidelity.md): global usings, csproj `Nullable`/`OutputType`, ASP.NET reference pack | none | 0011 |
| 01 | [Placeholder cleanup](01-placeholder-cleanup.md): remove 176 name-only checks (509 → 333) and add guards. [Triage](01-placeholder-triage.md) and [revival plans by theme](01-placeholders/) | 00 | none |
| 05 | [Secrets](05-secrets.md): C# detectors, then a `config` Language (phase 2) | 01 | 0012 |
| 02 | [Nullable](02-nullable.md): the compiler's nullable warnings as Checks | 00 | none |
| 03 | [Taint and flow](03-taint-flow.md): taint followed within each method over the control-flow graph | 00 | 0013 |
| 04 | [Duplication](04-duplication.md): `duplicate-code` and a real `identical-method` | none | none |
| 06 | [Architecture](06-architecture.md): `.qlcheck_architecture` Constraints | 00 | 0014 |
| 07 | [Redundant null checks](07-redundant-null-check.md): `redundant-null-check` on never-null method results | 02 | none |
| 08 | [Public API visibility](08-public-api-visibility.md): `no-list-in-public-api`, `no-public-const` and `exception-public` judge reachability like the compiler | none | none |
| 09 | [Output review fixes](09-output-review-fixes.md): one handler per row, precise matchers, honest `identical-method` advice, case-sensitive built-in ignores, runtime-only references | 08 | none |

Out of scope by decision: taint across methods or files, abstract interpretation, symbolic execution, dependency (SCA) and licence scanning, cohesion metrics, scanning git history for secrets.
