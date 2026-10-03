# Issues with no fix proposed here

Everything reported as a false positive in `requisitionops/api/QLCHECK_KNOWN_ISSUES.md` §2 has
a concrete fix proposed in this directory. Two things came up during that investigation that
are **not** fixed by any of those plans and need their own follow-up:

## 1. `jwt-strong-signature`'s actual "strong cipher" detection

See `docs/fixes/jwt-strong-signature.md` Part A for the false-positive fix (restrict matching to
real JWT APIs by symbol). That only stops the check from firing on unrelated `.Sign(...)` calls
— it does not implement what the check's own message claims: judging whether the
signing/verification algorithm is actually strong.

Doing that properly needs:
- Resolving the algorithm argument passed to `SigningCredentials`'s constructor, or the
  algorithm(s) accepted in `TokenValidationParameters.ValidAlgorithms` — a semantic lookup on
  `System.IdentityModel.Tokens.Jwt`/`Microsoft.IdentityModel.Tokens` constructs.
- A decision on which algorithms/key sizes count as "weak" for this project's purposes (e.g.
  flag `HmacSha1` and undersized symmetric keys; allow `RS256`/`ES256`/`HmacSha256`+) — this is
  a policy call, not something to infer from the codebase.

This is a new, scoped design task, not a bug fix — flagging it here rather than guessing at a
policy unilaterally.

## 2. Systemic risk: other `Row.Invoke`/`Row.Member`/`Row.Ident` rows with no receiver filter

Three of the confirmed false positives (`datetime-kind`, `prefer-set-min-max`,
`jwt-strong-signature`) share the exact same defect shape: a `Row.Invoke(id, class, "MethodName")`
call with no `type` argument, matching on a bare method/identifier name with zero regard for
the receiver's actual type. `checks.json`'s fixture for all three was also shaped as a literal,
context-free call to the row's own method-name parameter (e.g. `"SpecifyKind();"`,
`"Min();"`, `"Sign();"`) rather than anything derived from the check's stated intent — meaning
`CatalogTests.Each_implemented_check_has_a_failing_and_clean_fixture` was tautologically
"passing" for all three without ever exercising the real detection logic.

There are roughly 140 `Row.Invoke`/`Row.Member`/`Row.Ident` rows across `SyntaxEngine.cs` and
`SymbolEngine.cs` with no `type`/receiver argument. Some of these are legitimately fine as bare
name matches (e.g. matching a well-known static method or a name that's essentially unambiguous
in context); others may share this exact false-positive shape and simply haven't been reported
yet because no downstream project has hit them.

**No fix proposed here** — this needs a dedicated audit pass, not a blanket change:
1. Enumerate every `Row.Invoke(...)`/`Row.Member(...)`/`Row.Ident(...)` call with no `type`
   argument.
2. For each, cross-check the `checks.json` message against what a bare-identifier match
   actually catches, and check whether the corresponding `fixtures.json` entry is a real
   true-positive case or a tautological one (mirrors the method-name parameter with no
   receiver/context).
3. Triage: rows whose fixture is tautological are the highest-priority candidates for the same
   class of false positive found here.

This is a multi-day audit, not a fix — recorded here so a future pass doesn't have to re-derive
why it matters.

## Addendum: outcome of the 53-row false-positive pass

Of the 76 rows the audit above flagged as "matches a too-common name," 53 got a real
receiver-type/symbol-based fix (see `SymbolPatterns.cs`/`ResourcePatterns.cs`). The rest were
explicitly deferred, not guessed at:

- **~20 rows turned out to belong to the wrong-node-kind bucket already described above**, not
  this false-positive class: `argument-order`, `group-overloads`, `identical-method`,
  `no-default-argument`, `no-overlapping-overload`, `parameter-name-not-method`,
  `type-param-in-signature`, `no-recursive-inheritance`, `flags-none`, `property-not-get-method`,
  `prefer-property`, `check-model-state`, `hashcode-immutable`, `no-is-this`,
  `no-array-covariance`, `no-delegate-subtraction`, `prefer-awaitable`, `task-not-null`,
  `route-constraint-type`, `no-anonymous-unsubscribe`. Each needs a `Row.Handle` rewrite over a
  different node kind (method/class/enum declarations, operator expressions, property accesses)
  rather than a receiver-type filter on an invocation.
- **`database-password`, `hardcoded-credential`, `hardcoded-secret`** need a new "literal string
  argument to a Password/Secret-named setter" matcher — a new `Row` DSL entry, not the
  receiver-type recipe used for the other 53. Still unimplemented; still its own follow-up.
- **`no-recursive-inheritance`**: unclear what this means for plain C# classes (the compiler
  already rejects literal circular inheritance); likely CRTP-related or not applicable — needs a
  human decision on intent before any fix, guessed or otherwise.
- **`check-model-state`**: unclear whether the check should fire on *presence* of an
  `IsValid`-shaped call outside `ModelState.IsValid`, or on *absence* of a
  `ModelState.IsValid` check in a controller action — these are different algorithms; left
  unimplemented rather than guess which.
- **`no-weak-lock` vs `no-weak-lock-object`**: resolved, not deferred — implemented as two
  distinct checks (`no-weak-lock` flags `Monitor.Enter(this)`; `no-weak-lock-object` flags
  `Monitor.Enter` on `string`/`Type`/`Thread`/array/`MarshalByRefObject`/`ExecutionContext`
  arguments — CA2002's weak-identity set), since the two messages describe genuinely different
  anti-patterns once read closely.
