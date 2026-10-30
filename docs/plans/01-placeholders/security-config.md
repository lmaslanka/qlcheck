# Security configuration - revival plans

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Part of `../01-placeholder-cleanup.md`. Every check here was removed in that plan because its
`Row.Invoke`/`Row.Attribute`/`Row.Ident` placeholder was DEAD or NOISY (see `../01-placeholder-triage.md`).

Each section is a standalone plan to bring the check back for real. 13 removed checks: 12 to revive, 1 not worth reviving.

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
| `request-validation` | `[ValidateInput(false)]` on a controller or action. | `[ValidateInput(false)]` exists only in ASP.NET (classic) MVC; not applicable to ASP.NET Core. |

## `cookie-httponly`

Original message: "Cookies should have the `HttpOnly` flag."  
Removed row: `Row.Invoke` on `HttpOnly` (SymbolEngine.cs:39).

**Detect.** A cookie options object (`Microsoft.AspNetCore.Http.CookieOptions`, `System.Web.HttpCookie`, `System.Net.Cookie`) created with `HttpOnly = false`, or created and appended without ever setting `HttpOnly = true`.

Positive (finding):
```csharp
Response.Cookies.Append("sid", id, new CookieOptions { Secure = true });
```
Negative (no finding):
```csharp
Response.Cookies.Append("sid", id, new CookieOptions { Secure = true, HttpOnly = true });
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `ObjectCreationExpression` of those types (symbol); inspect the initializer for `HttpOnly`; flag literal `false`, or absence when the object does not escape to a later `.HttpOnly = true` assignment in the same method.

**Fixtures.** bad = positive, good = negative. Stub `CookieOptions` in the fixture (ASP.NET is not in the TPA set).

**Open questions.**
- Treat absence as a finding (ASP.NET Core defaults to false), or only explicit `false`? Recommend absence counts; explicit non-constant values are skipped.

## `cookie-secure`

Original message: "Cookies should have the `secure` flag."  
Removed row: `Row.Invoke` on `Secure` (SymbolEngine.cs:40).

**Detect.** Same shape as `cookie-httponly` for the `Secure` property.

Positive (finding):
```csharp
new CookieOptions { HttpOnly = true }
```
Negative (no finding):
```csharp
new CookieOptions { HttpOnly = true, Secure = true }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** Share the predicate with `cookie-httponly`, parameterised by property name.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- `CookieSecurePolicy` set globally via `CookiePolicyOptions` makes per-cookie checks noisy; skip when `MinimumSameSitePolicy`/`Secure = CookieSecurePolicy.Always` is configured in the same compilation? Recommend: not in v1.

## `csrf-enabled`

Original message: "CSRF protections should not be disabled."  
Removed row: `Row.Invoke` on `IgnoreAntiforgeryToken` (SymbolEngine.cs:42).

**Detect.** `[IgnoreAntiforgeryToken]` on a controller/action/page, or `.DisableAntiforgery()` on an endpoint builder.

Positive (finding):
```csharp
[IgnoreAntiforgeryToken] public IActionResult Post(Form f) => Ok();
```
Negative (no finding):
```csharp
[ValidateAntiForgeryToken] public IActionResult Post(Form f) => Ok();
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` reporting `Attribute` named `IgnoreAntiforgeryToken` and `InvocationExpression` named `DisableAntiforgery` (name match is the whole check for both).

**Fixtures.** bad = positive, good = negative.

## `hardcoded-ip`

Original message: "IP addresses should not be hardcoded."  
Removed row: `Row.Invoke` on `Parse on IPAddress` (SyntaxEngine.cs:78).

**Detect.** A string literal that parses as an IPv4/IPv6 address, excluding loopback, `0.0.0.0`, `::`, broadcast, documentation ranges (192.0.2.0/24, 2001:db8::/32) and version-like strings in version contexts (`new Version("1.2.3.4")`, `[AssemblyVersion]`).

Positive (finding):
```csharp
var db = IPAddress.Parse("10.12.0.7");
```
Negative (no finding):
```csharp
var db = IPAddress.Parse(config["DbHost"]);
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `StringLiteralExpression`; `IPAddress.TryParse` plus a dotted-quad/colon shape regex; exclusions above; skip test files (`ctx.IsTestFile`).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Skip test files? Recommend yes.

## `no-cleartext-protocol`

Original message: "Clear-text protocols should not be used."  
Removed row: `Row.Invoke` on `http` (SymbolEngine.cs:90).

**Detect.** A string literal URL using `http://`, `ftp://` or `telnet://` (and `new SmtpClient { EnableSsl = false }`), excluding `localhost`, `127.0.0.1`, `[::1]`, `*.local`, and well-known XML namespace URIs (`http://www.w3.org/`, `http://schemas.`).

Positive (finding):
```csharp
var api = new Uri("http://payments.example.com/charge");
```
Negative (no finding):
```csharp
var api = new Uri("https://payments.example.com/charge");
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `StringLiteralExpression`; prefix check plus an exclusion list; skip attribute arguments and `const` namespace declarations.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Exclusion list contents (XML namespaces, schemas.microsoft.com, example.com in tests?).

## `no-weak-tls`

Original message: "Weak SSL/TLS protocols should not be used."  
Removed row: `Row.Invoke` on `Tls` (SymbolEngine.cs:127).

**Detect.** Member access `SslProtocols.Ssl2|Ssl3|Tls|Tls11` or `SecurityProtocolType.Ssl3|Tls|Tls11`.

Positive (finding):
```csharp
handler.SslProtocols = SslProtocols.Tls11;
```
Negative (no finding):
```csharp
handler.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `SimpleMemberAccessExpression`; resolve to `IFieldSymbol` on `System.Security.Authentication.SslProtocols`/`System.Net.SecurityProtocolType` and match the weak names. (`Row.Member("Tls", "SslProtocols")` per name would also do.)

**Fixtures.** bad = positive, good = negative.

## `no-world-accessible-file`

Original message: "File permissions should not be set to world-accessible values."  
Removed row: `Row.Invoke` on `SetAccessControl` (SymbolEngine.cs:128).

**Detect.** Granting file permissions to everyone: `FileSystemAccessRule` built with `WellKnownSidType.WorldSid` or the literal `"Everyone"`, or `File.SetUnixFileMode`/`UnixFileMode` including `OtherRead|OtherWrite|OtherExecute`.

Positive (finding):
```csharp
File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.OtherWrite);
```
Negative (no finding):
```csharp
File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `ObjectCreationExpression` of `FileSystemAccessRule`/`SecurityIdentifier` and member accesses `UnixFileMode.Other*` (symbol on `System.IO.UnixFileMode`).

**Fixtures.** bad = positive, good = negative.

## `no-xxe`

Original message: "Do not parse XML that can load external entities."  
Removed row: `Row.Invoke` on `LoadXml` (SymbolEngine.cs:129).

**Detect.** XML parsing with DTD processing enabled: `XmlReaderSettings.DtdProcessing = DtdProcessing.Parse`, `XmlDocument`/`XmlTextReader` given a non-null `XmlResolver` (`new XmlUrlResolver()`), or `XmlTextReader` with `DtdProcessing.Parse`. Plain `XmlDocument.LoadXml` is safe on .NET Core.

Positive (finding):
```csharp
var s = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = new XmlUrlResolver() };
```
Negative (no finding):
```csharp
var s = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over assignments/initializers to `DtdProcessing` (value `Parse`) and `XmlResolver` (non-null object creation), receiver typed in `System.Xml`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Target .NET Framework too (where `XmlDocument` resolves by default)? Needs project TFM; recommend .NET Core semantics only.

## `request-size-limit`

Original message: "Limit the HTTP request body to 8 MB."  
Removed row: `Row.Invoke` on `DisableRequestSizeLimit` (SymbolEngine.cs:153).

**Detect.** `[DisableRequestSizeLimit]`, `[RequestSizeLimit(n)]` with constant `n > 8_388_608`, or `MaxRequestBodySize = null` / a constant above 8 MB.

Positive (finding):
```csharp
[RequestSizeLimit(100_000_000)] public IActionResult Upload() => Ok();
```
Negative (no finding):
```csharp
[RequestSizeLimit(8_000_000)] public IActionResult Upload() => Ok();
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `Attribute` (names above, constant via `model.GetConstantValue`) and `SimpleAssignmentExpression` whose left name is `MaxRequestBodySize`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Is 8 MB the right threshold for qlcheck's audience? (Sonar default; recommend keep, defined in `Thresholds.cs`.)

## `restrict-deserialized-types`

Original message: "Types allowed to be deserialized should be restricted."  
Removed row: `Row.Invoke` on `Deserialize on BinaryFormatter` (SymbolEngine.cs:156).

**Detect.** Deserialization through `BinaryFormatter`, `NetDataContractSerializer`, `SoapFormatter`, `LosFormatter`, `ObjectStateFormatter`, or `JavaScriptSerializer` with a `SimpleTypeResolver`, where no `Binder` (`SerializationBinder`) is assigned on that formatter.

Positive (finding):
```csharp
var f = new BinaryFormatter(); return f.Deserialize(stream);
```
Negative (no finding):
```csharp
var f = new BinaryFormatter { Binder = new AllowListBinder() }; return f.Deserialize(stream);
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `InvocationExpression` named `Deserialize`; receiver type via `model.GetTypeInfo`; look back in the method for a `Binder` assignment on the same local (local-symbol match).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Should plain `BinaryFormatter` use be reported regardless of a binder (it is unsafe and removed in .NET 9)? Recommend: separate check, not this one.

## `restrictive-csp`

Original message: "Content Security Policies should be restrictive."  
Removed row: `Row.Invoke` on `ContentSecurityPolicy` (SymbolEngine.cs:157).

**Detect.** A `Content-Security-Policy` header value (string literal passed to `Headers.Add`/`Append`/indexer for that header name, or a CSP builder) containing `'unsafe-inline'`, `'unsafe-eval'`, or a bare `*` source.

Positive (finding):
```csharp
context.Response.Headers["Content-Security-Policy"] = "default-src *; script-src 'unsafe-inline'";
```
Negative (no finding):
```csharp
context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
```

**Engine / scope.** Syntax.  **Size.** M.

**Matcher.** `Row.Handle` over string literals whose sibling/indexer key is the literal `Content-Security-Policy` (case-insensitive); scan the value tokens.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Also inspect NWebsec/`NetEscapades` fluent builders (`.UnsafeInline()`)? Recommend later.

## `verify-server-certificate`

Original message: "Server certificates should be verified during SSL/TLS connections."  
Removed row: `Row.Invoke` on `ServerCertificateValidationCallback` (SymbolEngine.cs:198).

**Detect.** Assigning a certificate validation callback that accepts everything: `ServicePointManager.ServerCertificateValidationCallback`, `HttpClientHandler.ServerCertificateCustomValidationCallback`, `SslClientAuthenticationOptions.RemoteCertificateValidationCallback`, or `HttpClientHandler.DangerousAcceptAnyServerCertificateValidator`, with a lambda/method whose every return is constant `true`.

Positive (finding):
```csharp
handler.ServerCertificateCustomValidationCallback = (m, c, ch, e) => true;
```
Negative (no finding):
```csharp
handler.ServerCertificateCustomValidationCallback = (m, c, ch, e) => e == SslPolicyErrors.None;
```

**Engine / scope.** Syntax.  **Size.** M.

**Matcher.** `Row.Handle` over `SimpleAssignmentExpression` whose left member name is one of the callbacks; right side is a lambda with expression body `true` or a block whose returns are all `true`; also any `DangerousAcceptAnyServerCertificateValidator` member access.

**Fixtures.** bad = positive, good = negative.

