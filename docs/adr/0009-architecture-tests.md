# Layering rules are tested, not just documented

`tests/Qlcheck.ArchitectureTests` enforces which part of qlcheck's own source may depend on
which: Cli parses arguments and depends on nothing else; Report only renders what it is handed;
Run orchestrates but never knows about Report or Cli; Scan is the lowest layer and depends on
nothing else in qlcheck; the Catalog inspects a `WalkContext` and never knows who called it; and
exactly two files (`Scan/GitChanges.cs`, `Checks/Coverage/CoverageRunner.cs`) may shell out to
another process (ADR 0002's "in-process" rule, made testable).

qlcheck is one assembly, so these are `using`-directive and syntax rules over the source tree —
the same "syntax trees, not compilation" approach as ADR 0001 — not project-reference or IL
rules. There is no multi-project dependency graph to inspect, so nothing like Mono.Cecil is
needed.

Each rule's violations are checked against a `Baseline/<RuleId>.txt` file: a violation not in
the baseline fails the rule (new code must comply); a baseline entry no longer occurring also
fails the rule (the baseline can only shrink, never silently accumulate). All six rules start
with an empty baseline — qlcheck's own layering was already clean — but the mechanism exists so
a future rule can be adopted incrementally against existing code without one big fix-everything
commit.

This is a separate test project from `Qlcheck.Tests` because it answers a different question
(does the shape of the code hold up?) from the catalog's own unit tests (does each check behave
correctly?), and because it has no reason to reference the main project — it reads `.cs` files
off disk and parses them itself.
