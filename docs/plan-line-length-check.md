# Plan: line-length Check

**Scope:** new default-on Check `line-length`. No config file. No plugin loading.

## Behaviour

- Id: `line-length`
- Finding when a physical line’s character count (no tab expansion, no `\r`/`\n`) is **> max**
- Default max **120**, overridable with `--max-line-length <n>` (`n >= 1`)
- On by default (unlike `inline-sql`). `--check` still filters as today
- Skip `.g.cs` / `.Designer.cs` (same as `one-type-per-file`)
- Message: `Line is {n} characters; max is {max}.`
- Location: overflow column (`max + 1`)
- Id: `line-length:{file}:{line}:{column}`

**Replacement** only if wrap is valid C#, idiomatic, and every result line is `<= max`. Otherwise Finding with `Replacement: null`.

## Idiomatic wrap (`CSharpLineWrap`)

Helper like `SqlText` — not an `ICheck` (discovery would pick it up).

`TryWrap(SyntaxTree tree, int lineNumber, int maxLength) -> string?`

Innermost overflowing construct, then format it:

| Construct | Wrap |
|---|---|
| Argument / parameter / attribute / type-arg lists (2+ items) | `Foo(\n    a,\n    b)` |
| Member/invocation chain | break before `.` |
| Binary / conditional | break before operator |
| Initializer | members one per line |
| Regular / interpolated string | `"a " +\n    "b"` at a word break |
| `//` comment | word-wrap with `// ` continuations |

Do not wrap: `#` directives, raw-string content, a single non-string token longer than max.

Continuation indent = that line’s leading whitespace + 4 spaces. Recurse until every line fits or give up.

Safety: substitute the rewritten line into the file text, reparse; if the tree has errors, return null.

## CLI / construction

`ICheck` stays parameterless. `LineLengthCheck(int maxLength = 120)` so `Activator.CreateInstance` still works.

After `CheckDiscovery.All()`, replace the instance:

`new LineLengthCheck(options.MaxLineLength)`

Parse `--max-line-length` like `--check`. Missing / non-integer / `< 1` → `ExitCode.Error` + usage.

Usage becomes:

`qlcheck [--human] [--stats] [--check <id>] [--inline-sql] [--max-line-length <n>] <path>...`

## Files

- `src/Qlcheck/LineLengthCheck.cs`
- `src/Qlcheck/CSharpLineWrap.cs`
- `tests/Qlcheck.Tests/LineLengthCheckTests.cs` (detection + Replacement via `ICheck.Run`; use max `40` in fixtures)
- `src/Qlcheck/QlcheckApp.cs` — flag, reconstruct check
- `tests/Qlcheck.Tests/QlcheckAppTests.cs` — flag errors; stats `Checks run` 6 → 7
- `tests/Qlcheck.Tests/CheckDiscoveryTests.cs`
- `CONTEXT.md` — **Line length** term
- `docs/adr/0004-line-length-idiomatic-wrap.md` — wrap is idiomatic C#, not a whitespace split

## Tests (tracer)

1. Line of 121 chars at default 120 → one Finding, no wrap if unwrappable identifier
2. `Foo(a, b, c, d)` over a small max → parenthesized one-arg-per-line Replacement
3. Fluent `.A().B().C()` → break before dots
4. Long `"hello world …"` → concatenated string literals
5. `#nullable enable` padded past max → Finding, null Replacement
6. `--max-line-length 40` vs default; bad flag values
7. `--check magic-literal` does not report long lines

No new packages. Roslyn syntax tree only.
