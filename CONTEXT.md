# Qlcheck

A CLI that reports code-state Findings so an AI agent can put a repository into a proper state.

## Language

**Check**:
A named inspection of source that emits Findings. It belongs to one Language.
_Avoid_: Rule, lint, analyzer

**Finding**:
One reported problem for an agent to fix. It has a stable ID.
_Avoid_: Violation, diagnostic, issue, error

**Language**:
A programming language qlcheck can inspect. A Check runs only on files of its Language.
_Avoid_: dialect, plugin

**Inline SQL**:
A SQL string that reaches a Dapper or ADO execution call. The inline-sql check is opt-in (`--inline-sql` or `--check inline-sql`).

**Magic literal**:
A numeric or string literal that should be a named const or enum member.

**String concatenation**:
Joining strings with `+` or `string.Concat`. Use interpolation instead.
