# Fix: `jwt-strong-signature` matches any `.Sign(...)` call (partial fix — see caveat)

## Reported issue

From `requisitionops/api/QLCHECK_KNOWN_ISSUES.md` (2 findings, confirmed false positives):

> both in `ResendWebhookControllerTests.cs`, testing HMAC webhook signature verification
> (Resend's webhook signing scheme), not JWTs. Looks like a false hit on the word "sign".

## Root cause

- Row: `SymbolEngine.cs:71` — `Row.Invoke("jwt-strong-signature", CheckClass.Symbol, "Sign")`.
- `InvokeMatch.Report` matches any invocation whose method name is `"Sign"` — no receiver-type
  or symbol check at all, despite the row being classified `CheckClass.Symbol` (a
  classification that implies symbol-awareness the implementation never actually has).
- Message (`checks.json`): "JWT should be signed and verified with strong cipher algorithms."
  The real intent is a security check on **JWT-specific** signing (weak algorithm choice, e.g.
  `HmacSha1` or an undersized key) — a `.Sign(...)` name match on any API (HMAC webhook
  verification, certificate signing, anything at all) collides with that entirely.

## Proposed fix — two parts

**Part A (fixes the reported false positive, doable now):** restrict the match to actual JWT
construction/signing APIs by resolved symbol, rather than by bare method name:

```csharp
// SymbolEngine.cs
Row.Handle("jwt-strong-signature", CheckClass.Symbol, Patterns.JwtSigning),
```

```csharp
private static readonly HashSet<string> JwtTypeNames = new(StringComparer.Ordinal)
{
    "JwtSecurityTokenHandler", "JwtSecurityToken", "SigningCredentials",
};

public static void JwtSigning(WalkContext ctx, string id)
{
    foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
    {
        if (node is not InvocationExpressionSyntax invocation)
        {
            continue;
        }

        var symbol = Symbols.SymbolOf(ctx, invocation.Expression);
        if (symbol?.ContainingType is { } type && JwtTypeNames.Contains(type.Name))
        {
            ctx.Report(id, invocation);
        }
    }
}
```

This stops matching arbitrary `.Sign(...)` calls (HMAC webhook verification included), which
directly resolves the reported false positive.

**Part B — not fixed here, flagged explicitly:** Part A only narrows *what counts as JWT
signing*; it does **not** implement the check's actual stated purpose — judging whether the
algorithm/cipher used is "strong." Doing that properly needs to inspect the algorithm argument
passed to `SigningCredentials`'s constructor (e.g. flag `SecurityAlgorithms.HmacSha1` or a
symmetric key under some minimum length) or the `TokenValidationParameters` used for
verification. This is meaningfully more design work than a bug fix — it needs a decision on
which algorithms/key-size thresholds count as "weak" for this codebase, which isn't something
to infer unilaterally. See `docs/fixes/no-fix-yet.md` for this half of the issue.

## Test plan (Part A only)

- Update `tests/Qlcheck.Tests/Catalog/fixtures.json["jwt-strong-signature"]`:
  - `bad`: a `SigningCredentials` construction, e.g.
    `var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);`
  - `good`: leave as a non-JWT `.Sign(...)` call, e.g. `hmac.ComputeHash(data);` or similar,
    confirming it does not fire.
- Add a unit test reproducing the exact reported shape: a class with a method calling
  `hasher.Sign(payload)` where `hasher` is some non-JWT type — must produce `Empty` findings.
- Add a true-positive test using `JwtSecurityTokenHandler.CreateToken(...)` or
  `new SigningCredentials(...)` — must fire (this is the first real test this check would have,
  since the current fixture is tautological — see the note below).

## Confidence

Part A: clean, low-risk — same shape as the `prefer-set-min-max` fix, reusing
`Symbols.SymbolOf`. Part B (actual "strong cipher" detection): not attempted, needs its own
scoped design — see `docs/fixes/no-fix-yet.md`.
