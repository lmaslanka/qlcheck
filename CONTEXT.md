# Qlcheck

A CLI that reports C# code-state Findings so an AI agent can put a repository into a proper state.

## Language

**Check**:
A named inspection of C# source that emits Findings.
_Avoid_: Rule, lint, analyzer

**Finding**:
One reported problem for an agent to fix. It has a stable ID.
_Avoid_: Violation, diagnostic, issue, error

**Inline SQL**:
A SQL string that reaches a Dapper or ADO execution call.

**Magic literal**:
A numeric or string literal that should be a named const or enum member.

**String concatenation**:
Joining strings with `+` or `string.Concat`. Use interpolation instead.
