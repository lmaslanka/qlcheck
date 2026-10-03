# Fix: `ssrf` / `ssrf-traversal` treat any non-string-shaped parameter as taint

## Reported issue

From `requisitionops/api/QLCHECK_KNOWN_ISSUES.md` (3 + 4 findings, confirmed false positives):

> all on `HttpClient.SendAsync`/typed-client calls where the `HttpClient.BaseAddress` is fixed
> from server-side configuration (`ControlPlaneClient`, `ResendClient`, the Azure Document
> Intelligence / OpenAI clients) and path segments are either literal or
> `Uri.EscapeDataString`-encoded typed values (`Guid`, server config). No attacker-controlled
> destination.

## Root cause

- Rows: `TaintEngine.cs:29-30` — `Row.Taint("ssrf", CheckClass.Taint, "GetAsync")` and
  `Row.Taint("ssrf-traversal", CheckClass.Taint, "SendAsync")`.
- Both go through the same shared `TaintMatch.IsTainted` (`Match/TaintMatch.cs:85-108`), which
  treats **any** public-method parameter as taint regardless of its type. A `Guid recordId`
  parameter is not attacker-controlled, URL-shaped input the way an arbitrary `string` is, but
  `IsParameter` (`TaintMatch.cs:110-132`) only checks the parameter's *name*, never its
  declared type.
- There is also no sanitizer recognition: a value wrapped in `Uri.EscapeDataString(...)` isn't
  a case `IsTainted` even inspects (it only special-cases direct identifiers, `Console.ReadLine()`,
  interpolated strings, and `+` concatenation) — an escaped value passed as a bare argument
  would currently fall through to `return false` already, so this isn't the cause of the
  reported false positives, but is worth hardening explicitly so a future change to
  `IsTainted` doesn't accidentally start flagging escaped values.
- The reported false positives are almost certainly the "non-string parameter passed directly"
  case (e.g. a `Guid` or a strongly-typed config value reaching `SendAsync`/`GetAsync` on a
  client whose `BaseAddress` is fixed).

## Proposed fix

**Primary fix — type-aware taint.** In `IsParameter`, additionally require the parameter's
declared type to be `string` (or `string?`) before treating it as taint:

```csharp
private static bool IsParameter(IdentifierNameSyntax identifier)
{
    var method = Shapes.Enclosing(identifier, SyntaxKind.MethodDeclaration) as MethodDeclarationSyntax;
    if (method is null || !Shapes.HasModifier(method, SyntaxKind.PublicKeyword))
    {
        return false;
    }

    foreach (var parameter in method.ParameterList.Parameters)
    {
        if (parameter.Identifier.Text == identifier.Identifier.Text)
        {
            return IsStringTyped(parameter.Type);
        }
    }

    return false;
}

private static bool IsStringTyped(TypeSyntax? type) =>
    type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.StringKeyword)
    || type is NullableTypeSyntax nullable && IsStringTyped(nullable.ElementType);
```

This is a syntax-only check (matches the type as spelled — `string`/`string?` — no semantic
model needed), directly clears the reported `Guid`-argument false positives for `ssrf`,
`ssrf-traversal`, and `sql-injection` alike, and needs no change to the `Row.Taint` DSL or
`TaintEngine.cs` at all — it's a one-function change shared by all ~19 taint ids.

**Secondary hardening (optional, not required to close the reported issue) — explicit
sanitizer recognition.** Add an explicit short-circuit in `IsTainted` for known encoders, so a
future change to taint sources doesn't regress this:

```csharp
private static readonly HashSet<string> Sanitizers = new(StringComparer.Ordinal)
{
    "EscapeDataString", "EscapeUriString", "UrlEncode",
};

// inside IsTainted, before the existing checks:
if (expression is InvocationExpressionSyntax sanitizerCall && Sanitizers.Contains(Names.Invocation(sanitizerCall)))
{
    return false;
}
```

## Impact and required verification

`IsParameter`/`IsTainted` back **all ~19** `Row.Taint(...)` ids in `TaintEngine.cs` (`argument-injection`,
`command-injection`, `connection-string-injection`, `deserialization-injection`,
`dynamic-code-injection`, `filesystem-oracle`, `ldap-injection`, `log-injection`,
`no-stack-trace-disclosure`, `nosql-injection`, `open-redirect`, `path-injection`,
`reflected-xss`, `reflection-injection`, `regex-dos`, `sql-injection`, `ssrf`, `ssrf-traversal`,
`untrusted-environment-variable`, `untrusted-session-cookie`, `xml-injection`, `xpath-injection`,
`xslt-injection`, `zip-slip`). Restricting taint to `string`-typed parameters is very likely a
no-op for the true-positive fixtures of most of these (most already use `string` parameters in
their `fixtures.json` bad case), but this **must be verified, not assumed** — run
`Each_implemented_check_has_a_failing_and_clean_fixture` after the change and manually check
any id whose fixture's tainted argument isn't a plain `string` (e.g. anything taking a
`Stream`, `byte[]`, or similar that legitimately needs different handling).

## Test plan

- Add a regression test reproducing the reported shape:
  ```csharp
  class C
  {
      public Task M(HttpClient client, Guid recordId) =>
          client.GetAsync($"https://fixed.example.com/{recordId}");
  }
  ```
  Note: this specific example uses an interpolated string, which `IsTainted` already inspects
  per-identifier (line 97-100) — the fix must also apply the type check inside that branch, not
  just the direct-identifier branch. Confirm the interpolated-string case: only flag if an
  interpolated segment references a `string`-typed tainted parameter, not a `Guid`-typed one
  used via `{recordId}` interpolation (which auto-converts to string but isn't itself
  string-typed at the parameter level) — this is deliberately still excluded per the type check,
  matching the reported false positive exactly.
- Add a true-positive regression case: `client.GetAsync(userSuppliedUrl)` where
  `userSuppliedUrl` is a `string` parameter — must still fire.
- Re-run the full `fixtures.json`-driven test (`Each_implemented_check_has_a_failing_and_clean_fixture`)
  across all 19 taint ids listed above and confirm none regress.

## Confidence

The type-check part is clean, low-risk, and reuses existing syntax-only conventions — but it
changes a function shared by 19 ids, so the blast radius is wider than the other fixes in this
set and needs the full regression pass described above before landing. This is **not** full
dataflow/taint-flow analysis (it doesn't trace a value through assignments or track whether a
`BaseAddress` is attacker-influenced) — it's a one-hop, type-level tightening, consistent with
the project's existing (syntax-only, one-hop) taint model.
