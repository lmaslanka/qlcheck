# A dated suppression comment defers a finding, not silences it forever

Some findings are a judgment call qlcheck cannot make itself — `jwt-strong-signature` is the
first example: whether a signing algorithm is "strong enough" is a security policy decision,
not a lint fact. That check's message tells the agent to ask the user rather than change the
code. Once the user has weighed in and accepted the current choice, a `// qlcheck-ignore:
<rule-id> checked-on:<YYYY-MM-DD>` comment on the flagged line or the line above suppresses that
one finding — but only for one year from `checked-on` (`Run/Suppressions.cs`). After a year the
finding resurfaces even though the comment is still there, forcing a fresh look rather than a
permanent exemption; the code doesn't silently rot on a decision that might no longer hold
(a cipher considered fine today may not be in five years). This differs from
`no-inline-suppression`, which forbids permanently silencing a Check via `[SuppressMessage]` —
a dated comment is a deferral with a forced re-review, not a silence.

Filtering happens once, in `CheckRun.Execute`, against the flat `Finding` list and each file's
raw text — not inside individual Check predicates, not Roslyn-trivia-based — so it applies
uniformly regardless of which engine or language produced the finding. This mechanism is
general (any Check id can be used in the comment), not `jwt-strong-signature`-specific; other
Checks whose "fix" is actually a judgment call can use their own message to point at it the same
way.
