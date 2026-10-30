# URI typing - revival plans

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Part of `../01-placeholder-cleanup.md`. Every check here was removed in that plan because its
`Row.Invoke`/`Row.Attribute`/`Row.Ident` placeholder was DEAD or NOISY (see `../01-placeholder-triage.md`).

Each section is a standalone plan to bring the check back for real. 6 removed checks: 6 to revive, 0 not worth reviving.

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

## `no-hardcoded-uri`

Original message: "URIs should not be hardcoded."  
Removed row: `Row.Invoke` on `Uri` (SyntaxEngine.cs:145).

**Detect.** An absolute URI or absolute file path literal (`https://...`, `C:\\...`, `/var/...`) passed to a method or assigned to a member, outside `const` declarations and configuration/test files.

Positive (finding):
```csharp
var client = new HttpClient { BaseAddress = new Uri("https://prod.example.com") };
```
Negative (no finding):
```csharp
var client = new HttpClient { BaseAddress = new Uri(config["ApiBase"]) };
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `StringLiteralExpression`; scheme/drive-letter/rooted-path shape; skip `const` initializers, attributes, XML namespaces, test files.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Overlaps `no-cleartext-protocol` on `http://` literals; both firing on one literal acceptable? Recommend: this one skips `http://` literals so each literal yields one finding.

## `uri-not-string-argument`

Original message: "`System.Uri` arguments should be used instead of strings."  
Removed row: `Row.Invoke` on `Uri` (SymbolEngine.cs:188).

**Detect.** A call passing a `string` to a method that has an overload taking `System.Uri` at the same position.

Positive (finding):
```csharp
client.GetAsync("https://api.example.com/x");  // when GetAsync(Uri) exists
```
Negative (no finding):
```csharp
client.GetAsync(new Uri("https://api.example.com/x"));
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `InvocationExpression`; resolved method has a `string` parameter whose name contains `uri`/`url`; `ContainingType.GetMembers(name)` has an overload with `Uri` at that index.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Very common with `HttpClient`; useful for agents or just churn? Recommend restricting to methods declared in the same compilation (your own API) - see `uri-overload-calls-uri`.

## `uri-overload-calls-uri`

Original message: "String URI overloads should call `System.Uri` overloads."  
Removed row: `Row.Invoke` on `Uri` (SymbolEngine.cs:189).

**Detect.** A method with a `string` URI parameter, declared next to an overload taking `Uri`, whose body does not call the `Uri` overload.

Positive (finding):
```csharp
public void Open(string url) { Navigate(url); }  public void Open(Uri url) { ... }
```
Negative (no finding):
```csharp
public void Open(string url) => Open(new Uri(url));  public void Open(Uri url) { ... }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `MethodDeclaration` pairs in a type; the string overload's body must contain an invocation resolving to the `Uri` overload.

**Fixtures.** bad = positive, good = negative.

## `uri-parameter-not-string`

Original message: "URI Parameters should not be strings."  
Removed row: `Row.Invoke` on `Uri` (SymbolEngine.cs:190).

**Detect.** A public/protected method parameter of type `string` whose name contains `uri`, `url` or `urn` (case-insensitive, word-boundary).

Positive (finding):
```csharp
public void Fetch(string url) { }
```
Negative (no finding):
```csharp
public void Fetch(Uri url) { }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `Parameter` in non-private methods; predefined `string` type; name split on case boundaries (reuse `Names` helpers).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Exempt when a `Uri` overload exists (then `uri-overload-calls-uri` applies)? Recommend yes.

## `uri-property-not-string`

Original message: "URI properties should not be strings."  
Removed row: `Row.Invoke` on `Uri` (SymbolEngine.cs:191).

**Detect.** A public/protected `string` property named `*Uri`/`*Url`/`*Urn`.

Positive (finding):
```csharp
public string HomepageUrl { get; set; }
```
Negative (no finding):
```csharp
public Uri HomepageUrl { get; set; }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `PropertyDeclaration`; same name/accessibility test.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- DTOs that are serialized often keep strings on purpose; skip `record`/types with `[JsonPropertyName]`? Recommend no skip, but list as known trade-off.

## `uri-return-not-string`

Original message: "URI return values should not be strings."  
Removed row: `Row.Invoke` on `Uri` (SymbolEngine.cs:192).

**Detect.** A public/protected method named `*Uri`/`*Url` (or `Get*Url`) returning `string`.

Positive (finding):
```csharp
public string BuildUrl(int id) => $"/orders/{id}";
```
Negative (no finding):
```csharp
public Uri BuildUrl(int id) => new($"/orders/{id}", UriKind.Relative);
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `MethodDeclaration`; return type `string`; name test.

**Fixtures.** bad = positive, good = negative.

