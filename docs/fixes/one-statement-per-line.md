# Fix: `one-statement-per-line` false positive on semicolons inside string literals

## Reported issue

From `requisitionops/api/QLCHECK_KNOWN_ISSUES.md` (24 findings, confirmed false positives):

> the checker miscounts statement boundaries when a semicolon appears *inside a string
> literal* on the same statement (SQL text like `"LOCK TABLE users IN EXCLUSIVE MODE;"`,
> connection strings with `Host=x;Database=y;`, content-type strings like
> `"text/csv; charset=utf-8"`). Confirmed on every instance.

## Root cause

- Row: `SyntaxEngine.cs:226` — `Row.Handle("one-statement-per-line", CheckClass.Syntax, Patterns.OneStatement)`.
- `Patterns.OneStatement` (`Patterns.cs:297`) calls `ReportLines(ctx, id, HasTwoStatements)`.
- `ReportLines` (`Patterns.cs:456-465`) splits the file's raw text (`ctx.Text`) on `'\n'` and
  runs `HasTwoStatements` against each **raw text line** — no syntax tree involved for the
  matching itself.
- `HasTwoStatements` (`Patterns.cs:714-734`) excludes lines containing the substrings
  `"for ("`, `"get;"`, or `"set;"`, then counts raw `;` characters in the line (skipping one
  immediately preceded by `'`, a crude char-literal guard), flagging when the count exceeds 1.
- This is a pure text scan with no concept of "inside a string literal." Any line containing a
  string with two or more semicolons trips the same counter as two real statements. The
  existing `for (`/`get;`/`set;` substring guards confirm this was always a text heuristic,
  patched incrementally for known false positives — string literals never got the same
  treatment.

## Proposed fix

Replace the text-scan with a token-based count using tokens Roslyn has already classified.
Semicolons inside a string/char/raw-string literal are never separate `SemicolonToken`s (they
are part of the literal token's text), so counting `SyntaxKind.SemicolonToken` tokens per line
eliminates the false positive entirely while preserving the intended "≥2 statement-terminating
semicolons on one line" semantics — no semantic model needed, staying consistent with
ADR 0001 (syntax-only Roslyn).

```csharp
public static void OneStatement(WalkContext ctx, string id)
{
    ctx.Tree.GetRoot().DescendantTokens()
        .Where(token => token.IsKind(SyntaxKind.SemicolonToken) && !IsLoopHeaderSemicolon(token))
        .GroupBy(token => token.GetLocation().GetLineSpan().StartLinePosition.Line)
        .Where(group => group.Count() > 1)
        .Select(group => group.First())
        .ToList()
        .ForEach(token => ctx.Report(id, token.GetLocation()));
}

private static bool IsLoopHeaderSemicolon(SyntaxToken token) =>
    token.Parent is ForStatementSyntax;
```

Two details carried over from the old text-based guards need explicit syntax-aware handling
instead of substring matching:

- **`for (init; cond; incr)` headers**: a `for` statement's own two semicolons are structurally
  `ForStatementSyntax.FirstSemicolonToken`/`SecondSemicolonToken` — i.e. `token.Parent` is the
  `ForStatementSyntax` itself, not a statement. Excluding by `token.Parent is ForStatementSyntax`
  (as above) replaces the old `"for ("` text guard precisely, without accidentally excluding
  unrelated code that merely contains the substring `"for ("` inside a string.
- **`{ get; set; }` on one line**: needs verification — trace whether the existing dedicated
  test (`PatternsTests.cs:103-124`, `OneStatement_ignores_lines_matching_for_get_and_set_accessors`)
  still passes with pure token counting. An auto-property's `get;`/`set;` accessor bodies are
  each their own `AccessorDeclarationSyntax`, and their semicolons are real
  `SemicolonToken`s distinct from statement terminators — if both accessors land on the same
  line, the new token-based count would (correctly-by-old-behavior) need the same exclusion the
  old text guard had. Add a check alongside the `for`-header exclusion: skip semicolons whose
  parent is `AccessorDeclarationSyntax` (covers `{ get; set; }` regardless of formatting), i.e.
  `token.Parent is ForStatementSyntax or AccessorDeclarationSyntax`.

## Test plan

- `tests/Qlcheck.Tests/Catalog/fixtures.json["one-statement-per-line"]`: existing bad/good pair
  (`return; return;` vs `return;`) is a legitimate true positive — keep it, confirm it still
  fires under the new implementation.
- `PatternsTests.cs:103-124` (`OneStatement_ignores_lines_matching_for_get_and_set_accessors`)
  must keep passing unchanged.
- Add new regression cases for exactly the reported false-positive shapes:
  ```csharp
  const string Sql = "LOCK TABLE users IN EXCLUSIVE MODE;";
  const string ConnectionString = "Host=x;Database=y;";
  const string ContentType = "text/csv; charset=utf-8";
  ```
  each on its own line, asserting `Empty` findings.
- Add a `for` statement with a body statement on the same line as the header, e.g.
  `for (var i = 0; i < 10; i++) DoWork();`, asserting `Empty` (only one real statement,
  `DoWork();`, plus the loop-header semicolons which must not be double-counted as separate
  statements).

## Confidence

Clean, minimal, no ADR conflict. The one design point requiring care is precisely replicating
the `for`-loop and same-line-accessor exclusions with syntax-aware checks instead of the old
text substring checks — both are straightforward given `SyntaxToken.Parent`.
