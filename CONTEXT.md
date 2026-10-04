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

**Magic literal**:
A numeric or string literal that should be a named const or enum member.

**String concatenation**:
Joining strings with `+` or `string.Concat`. Use interpolation instead.

**Suppression comment**:
A `// qlcheck-ignore: <rule-id> checked-on:<YYYY-MM-DD>` comment on the flagged line or the line
above it. Silences that one Finding for one year from `checked-on`, then it resurfaces. Use it
only after a human has made the judgment call a Check's message asked for (e.g.
`jwt-strong-signature`) — never add it unilaterally to make a Finding disappear.
