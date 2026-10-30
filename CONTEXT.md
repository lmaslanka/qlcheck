# Qlcheck

A CLI that reports code-state Findings so an AI agent can put a repository into a proper state.

## Language

**Check**:
A named inspection of source that emits Findings. It belongs to one Language.
_Avoid_: Rule, lint, analyzer

**Constraint**:
One declared statement of intended architecture, written by a human in the repository's architecture file: a component must not use another, a component is only used by certain others, or components under a prefix must not form a cycle. A component is a namespace or a type, named by its dotted name. A Finding reports a reference that breaks a Constraint.
_Avoid_: Rule, policy, layer rule

**Disabled check**:
A Check turned off for a directory tree (up to the whole repository), so it emits no Findings for files there unless it is asked for by name.
Only a human disables a Check, as a lasting policy. An agent never disables one to make its Findings go away.
_Avoid_: Skipped check, excluded check, ignored check

**Finding**:
One reported problem for an agent to fix. It has a stable ID.
_Avoid_: Violation, diagnostic, issue, error

**Language**:
A kind of file qlcheck can parse and inspect: a programming language (C#) or a configuration format (JSON settings, `.env`, YAML, XML config). A Check runs only on files of its Language.
_Avoid_: dialect, plugin, file kind

**Magic literal**:
A numeric or string literal used two or more times in one file that should be a named const or enum member. Messages, long strings, SQL, and literals in attributes, consts or collections don't count.
A **shared** magic literal is the same value fed to the same target (parameter, field, property or indexer) in another file, or a value that already has a const used for that target.

**Never-null result**:
The result of a method that cannot return null. Either the method is declared, with nullable annotations, to return a non-nullable value; or it has no nullable annotations, cannot be overridden, and is an iterator or returns only new objects, empty collections and other never-null results. A method annotated as possibly returning null never has a never-null result, whatever its body does today. Checking a never-null result for null is redundant.
_Avoid_: non-null return, safe result

**Nullable finding**:
A Finding from the compiler's own nullable-reference warnings (`null-dereference`, `nullable-argument`, `nullable-return`, `nullable-assignment`, `non-nullable-uninitialized`). Reported only where the compiler would warn and the file has no compile errors.
_Avoid_: null warning, nullability issue

**String concatenation**:
Joining strings with `+` or `string.Concat`. Use interpolation instead.

**Suppression comment**:
A `// qlcheck-ignore: <rule-id> checked-on:<YYYY-MM-DD>` comment on the flagged line or the line
above it. Silences that one Finding for one year from `checked-on`, then it resurfaces. Use it
only after a human has made the judgment call a Check's message asked for (e.g.
`jwt-strong-signature`) — never add it unilaterally to make a Finding disappear.
