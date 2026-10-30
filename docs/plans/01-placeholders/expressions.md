# Expressions: casts, strings, regex, dates - revival plans

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Part of `../01-placeholder-cleanup.md`. Every check here was removed in that plan because its
`Row.Invoke`/`Row.Attribute`/`Row.Ident` placeholder was DEAD or NOISY (see `../01-placeholder-triage.md`).

Each section is a standalone plan to bring the check back for real. 18 removed checks: 17 to revive, 1 not worth reviving.

Conventions for every section (not repeated below):
- **Revival = three edits:** restore the `{id, message}` entry in `checks.json` (original message
  quoted below; reword if the plan changes scope), add the row to the named engine, add the fixture.
- **Fixtures:** `bad` = the positive example, `good` = the negative example, wrapped in a namespace
  and type with the `using`s they need. Both must compile with zero errors (guard G1 in the main plan);
  framework types missing from the platform reference set (ASP.NET Core, EF Core, Logging, WinForms,
  test frameworks) are declared as small stubs inside the fixture.
- **Tests first:** add the fixture, watch `Each_implemented_check_has_a_failing_and_clean_fixture` fail,
  add the row, plus focused unit tests in the matching `*PatternsTests.cs` for each edge case listed.
- **Engine:** Syntax = `SyntaxEngine.Rows` (syntax-tree only), Symbol = `SymbolEngine.Rows` (needs
  `ctx.Model`), Compilation = `CompilationEngine` (needs other files), Flow = `FlowEngine` (CFG/dataflow).
- **Size:** S = one predicate, under a day. M = semantic model or several members, 1-2 days. L = dataflow,
  cross-file or new infrastructure.

## Not worth reviving

| Check | Would detect | Reason |
|---|---|---|
| `prefer-datetimeoffset` | Member declarations of type `DateTime` that hold instants. | Every `DateTime` declaration would be a candidate, so it is enormous noise; `datetime-utc` covers the concrete bug (decision D6). |

## `datetime-utc`

Original message: "Use UTC when recording DateTime instants."  
Removed row: `Row.Invoke` on `Now on DateTime` (SyntaxEngine.cs:45).

**Detect.** `DateTime.Now` (member access) where an instant is recorded - assigned to a field/property, stored, serialized, or compared with another instant. Use `DateTime.UtcNow`.

Positive (finding):
```csharp
order.CreatedAt = DateTime.Now;
```
Negative (no finding):
```csharp
order.CreatedAt = DateTime.UtcNow;
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `SimpleMemberAccessExpression` resolving to `System.DateTime.Now` (`Row.Member("Now", "DateTime")` is the syntax-only fallback).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Resolved by decision D6: `datetime-utc` and `no-datetime-now-timing` come back default-on, `testable-clock` comes back opt-in, `prefer-datetimeoffset` stays dropped.

## `indexof-not-positive`

Original message: "`IndexOf` checks should not be for positive numbers."  
Removed row: `Row.Invoke` on `IndexOf` (SyntaxEngine.cs:89).

**Detect.** `IndexOf(...) > 0` (misses a match at index 0); also `>= 1`.

Positive (finding):
```csharp
if (name.IndexOf("admin") > 0) { Grant(); }
```
Negative (no finding):
```csharp
if (name.Contains("admin")) { Grant(); }  // or IndexOf(..) >= 0
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `GreaterThanExpression`/`GreaterThanOrEqual` where one side is an `IndexOf` invocation and the other a literal `0` (resp. `1`).

**Fixtures.** bad = positive, good = negative.

## `no-datetime-now-timing`

Original message: "Avoid using `DateTime.Now` for benchmarking or timing operations."  
Removed row: `Row.Invoke` on `Now on DateTime` (SyntaxEngine.cs:119).

**Detect.** Elapsed-time measurement with the wall clock: `DateTime.Now`/`UtcNow` captured into a local and later subtracted from another `DateTime.Now`/`UtcNow` read.

Positive (finding):
```csharp
var start = DateTime.Now; Work(); var elapsed = DateTime.Now - start;
```
Negative (no finding):
```csharp
var sw = Stopwatch.StartNew(); Work(); var elapsed = sw.Elapsed;
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `SubtractExpression` where one operand is a `DateTime.Now/UtcNow` read and the other a local initialised from one (local-symbol lookup in the method).

**Fixtures.** bad = positive, good = negative. See docs/fixes/datetime-kind.md, which already scoped `datetime-kind` away from this expression.

## `no-duplicate-cast`

Original message: "Duplicate casts should not be made."  
Removed row: `Row.Invoke` on `ChangeType` (SyntaxEngine.cs:124).

**Detect.** A type test followed by a cast of the same expression to the same type: `if (x is T) { ((T)x)... }`, or `x as T` then `(T)x`.

Positive (finding):
```csharp
if (shape is Circle) { var r = ((Circle)shape).Radius; }
```
Negative (no finding):
```csharp
if (shape is Circle c) { var r = c.Radius; }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `IsExpression` with a type; search the `if` true-branch for `CastExpression` to the same type symbol on an expression with the same symbol.

**Fixtures.** bad = positive, good = negative.

## `no-nan-compare`

Original message: "`NaN` should not be used in comparisons."  
Removed row: `Row.Invoke` on `IsNaN` (SyntaxEngine.cs:158).

**Detect.** An equality/relational comparison against `double.NaN`/`float.NaN` (always false).

Positive (finding):
```csharp
if (value == double.NaN) { Reset(); }
```
Negative (no finding):
```csharp
if (double.IsNaN(value)) { Reset(); }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over binary comparison expressions with an operand resolving to `System.Double.NaN`/`System.Single.NaN` field.

**Fixtures.** bad = positive, good = negative.

## `no-new-guid`

Original message: "`new Guid()` should not be used."  
Removed row: `Row.Invoke` on `Guid` (SyntaxEngine.cs:164).

**Detect.** `new Guid()` with no arguments (or `default(Guid)` used as an id) - yields `Guid.Empty`.

Positive (finding):
```csharp
var id = new Guid();
```
Negative (no finding):
```csharp
var id = Guid.NewGuid();  // or Guid.Empty if that is meant
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `ObjectCreationExpression` with type `Guid`/`System.Guid` and empty argument list (also target-typed `new()` when the target is `Guid` - needs model).

**Fixtures.** bad = positive, good = negative.

## `prefer-char-overload`

Original message: "`StartsWith` and `EndsWith` overloads that take a `char` should be used instead of the ones that take a `string`."  
Removed row: `Row.Invoke` on `StartsWith on string` (SyntaxEngine.cs:215).

**Detect.** `StartsWith`/`EndsWith`/`IndexOf`/`Contains`/`Split` on a string with a single-character string literal argument where a `char` overload exists (and no `StringComparison` argument).

Positive (finding):
```csharp
if (path.StartsWith("/")) { }
```
Negative (no finding):
```csharp
if (path.StartsWith('/')) { }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over those invocations with receiver type `string`; sole argument is a 1-char string literal.

**Fixtures.** bad = positive, good = negative.

## `prefer-range`

Original message: "Start index should be used instead of calling Substring."  
Removed row: `Row.Invoke` on `Substring` (SyntaxEngine.cs:229).

**Detect.** `s.Substring(start)` passed directly to a method that has an overload taking `ReadOnlySpan<char>` (e.g. `int.Parse`, `StringBuilder.Append`) - use `s.AsSpan(start)` / `s[start..]`.

Positive (finding):
```csharp
var n = int.Parse(text.Substring(3));
```
Negative (no finding):
```csharp
var n = int.Parse(text.AsSpan(3));
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `Substring` invocations used as an argument; the outer method's containing type has an overload with `ReadOnlySpan<char>` at that position.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- The message mentions "start index"; is the intent span overloads (CA1846) or range syntax (IDE0057)? Recommend CA1846 semantics.

## `prefer-string-create`

Original message: "`string.Create` should be used instead of `FormattableString`."  
Removed row: `Row.Invoke` on `FormattableString` (SyntaxEngine.cs:231).

**Detect.** `FormattableString.Invariant($"...")` or `((FormattableString)$"...").ToString(culture)` - use `string.Create(CultureInfo.InvariantCulture, $"...")`.

Positive (finding):
```csharp
var s = FormattableString.Invariant($"{x}");
```
Negative (no finding):
```csharp
var s = string.Create(CultureInfo.InvariantCulture, $"{x}");
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over invocations resolving to `System.FormattableString.Invariant`/`CurrentCulture`.

**Fixtures.** bad = positive, good = negative.

## `redundant-cast`

Original message: "Redundant casts should not be used."  
Removed row: `Row.Invoke` on `ChangeType` (SyntaxEngine.cs:239).

**Detect.** A cast `(T)e` where `e` already has type `T` (identity conversion), excluding casts that select an overload or change nullability on purpose.

Positive (finding):
```csharp
int n = 5; var m = (int)n;
```
Negative (no finding):
```csharp
long m = (long)n;
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `CastExpression`; `model.GetTypeInfo(e).Type` equals the target type symbol (`SymbolEqualityComparer.IncludeNullability`) and `model.GetConversion` is identity.

**Fixtures.** bad = positive, good = negative.

## `regex-timeout`

Original message: "Regular expressions should be executed with a timeout."  
Removed row: `Row.Invoke` on `Match on Regex` (SymbolEngine.cs:151).

**Detect.** A `Regex` constructor or static `Regex.*` call with no `TimeSpan` timeout argument and no `RegexOptions.NonBacktracking` - when the pattern or input is not constant, or always (Sonar S6444).

Positive (finding):
```csharp
var ok = Regex.IsMatch(input, pattern);
```
Negative (no finding):
```csharp
var ok = Regex.IsMatch(input, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `ObjectCreationExpression`/invocations resolving to `System.Text.RegularExpressions.Regex`; overload lacks a `TimeSpan` parameter; options argument does not include `NonBacktracking`. `[GeneratedRegex]` without timeout too.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Fire on constant patterns with constant input? Recommend: only when the input is not a constant (patterns are usually constant).

## `regular-number-pattern`

Original message: "Number patterns should be regular."  
Removed row: `Row.Invoke` on `Match on Regex` (SyntaxEngine.cs:245).

**Detect.** A numeric literal with `_` digit separators in irregular groups (e.g. `1_00_000`, `0x_FF_F`).

Positive (finding):
```csharp
const int Max = 1_00_000;
```
Negative (no finding):
```csharp
const int Max = 100_000;
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `NumericLiteralToken`s containing `_`; split the integer part; groups (after the first) must all be the same width (3 for decimal, 2/4 for hex/binary).

**Fixtures.** bad = positive, good = negative.

## `safe-cast`

Original message: "Invalid casts should be avoided."  
Removed row: `Row.Invoke` on `ChangeType` (SyntaxEngine.cs:249).

**Detect.** A cast or `as` between types that can never be related: sealed classes with no inheritance relation, struct to unrelated interface the struct does not implement, etc. (compiles via `object`/interfaces but always fails).

Positive (finding):
```csharp
IDisposable d = GetDisposable(); var s = (string)(object)d;  // string never implements IDisposable
```
Negative (no finding):
```csharp
if (d is Stream s) { ... }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `CastExpression`/`as`; source and target types; report when target is sealed and does not implement/derive from source (or the reverse for interfaces).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Sonar's original also covers `Cast<T>()` on collections; include? Recommend v2.

## `testable-clock`

Original message: "Use a testable date/time provider."  
Removed row: `Row.Invoke` on `Now on DateTime` (SyntaxEngine.cs:268).

**Detect.** Direct reads of the system clock - `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now/UtcNow` - in non-test code, instead of an injected `TimeProvider`.

Positive (finding):
```csharp
if (DateTime.UtcNow > token.Expires) { Reject(); }
```
Negative (no finding):
```csharp
if (clock.GetUtcNow() > token.Expires) { Reject(); }  // TimeProvider clock
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over member accesses resolving to those four properties; skip test files and `Program`/composition-root files.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Very high volume in typical code; opt-in rather than default? Recommend default-off (opt-in like `coverage`) - needs the opt-in list in `CatalogCheck.cs`.

## `timezone-id-direct`

Original message: "Use `TimeZoneInfo.FindSystemTimeZoneById` without converting the timezones with `TimezoneConverter`."  
Removed row: `Row.Invoke` on `FindSystemTimeZoneById` (SyntaxEngine.cs:270).

**Detect.** Use of `TimeZoneConverter` (`TZConvert.GetTimeZoneInfo`, `TZConvert.IanaToWindows`, ...) on .NET 6+, where `TimeZoneInfo.FindSystemTimeZoneById` accepts both ID kinds.

Positive (finding):
```csharp
var tz = TZConvert.GetTimeZoneInfo("Europe/Warsaw");
```
Negative (no finding):
```csharp
var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over invocations with receiver text `TZConvert` (or symbol in namespace `TimeZoneConverter`).

**Fixtures.** bad = positive, good = negative. Stub `TimeZoneConverter.TZConvert` in the fixture.

## `use-unix-epoch`

Original message: "Use the `UnixEpoch` field instead of creating `DateTime` instances that point to the beginning of the Unix epoch."  
Removed row: `Row.Invoke` on `UnixEpoch` (SyntaxEngine.cs:280).

**Detect.** `new DateTime(1970, 1, 1, ...)` / `new DateTimeOffset(1970, 1, 1, ...)` with all time components zero.

Positive (finding):
```csharp
var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
```
Negative (no finding):
```csharp
var epoch = DateTime.UnixEpoch;
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `ObjectCreationExpression` of `System.DateTime`/`DateTimeOffset`; constant arguments 1970, 1, 1 and zeros.

**Fixtures.** bad = positive, good = negative.

## `valid-regex`

Original message: "Regular expressions should be syntactically valid."  
Removed row: `Row.Invoke` on `Match on Regex` (SyntaxEngine.cs:283).

**Detect.** A constant regex pattern (Regex ctor, static `Regex.Match/IsMatch/Replace/Split`, `[GeneratedRegex]`, `[RegularExpression]`) that fails to parse.

Positive (finding):
```csharp
var r = new Regex("[a-z");
```
Negative (no finding):
```csharp
var r = new Regex("[a-z]");
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over those call sites; constant pattern via `model.GetConstantValue`; `try { new Regex(pattern, options) } catch (ArgumentException)` at analysis time.

**Fixtures.** bad = positive, good = negative.

