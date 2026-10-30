# Placeholder triage - name-only catalog rows

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Appendix to `01-placeholder-cleanup.md`. One line per catalog row built with a name-only matcher
(`Row.Invoke` 185, `Row.Ident` 6, `Row.Kinds` 3, `Row.Attribute` 2, `Row.Trivia` 2 = 198 rows). The
catalog has no `Row.Invokes`/`Create`/`Creates`/`Member`/`Members`/`Token` rows; those builders are
exercised only by `tests/Qlcheck.Tests/Catalog/RowTests.cs`.

Verdicts:
- **WORKS** - the name match is the whole check (its fixture may still need fixing).
- **DEAD** - cannot fire on compiling code: the target is an attribute, property, field, enum member,
  interface, type, keyword or placeholder (`M`, `C`, `T`, `value`, `get`...), so it only fires on a user
  method that happens to have that name. Includes `Enumerable`/`string`/`Random`-typed rows, which match
  only the receiver *text* (e.g. the static `Enumerable.Count(xs)` form) and never extension or
  instance calls.
- **NOISY** - fires on real code without the condition the message states, often on the compliant code
  itself (`ConfigureAwait(false)`, `double.IsNaN`, `GenerateIV()`).

Size (of a real implementation): S = one syntax/attribute predicate; M = semantic model or several
members; L = dataflow/CFG, other file types or project-wide facts.

Action: KEEP / REMOVE -> theme file under `01-placeholders/` holding the revival plan / OWNED-* (another
plan; not touched here: secrets = plan 05, flow = plan 03, duplication = plan 04).

Evidence (gathered 2026-10-29, before Plan 00): "probe" = the built tool run on a realistic file (47 scratch
probes, not committed). "qlcheck's own source" = all 198 name-only checks run on this repo's `src/` and
`tests/` (18 findings, all false positives: `escape-field-keyword` 13, `indexof-not-positive` 2,
`escape-extension-keyword` 2, `no-delegate-subtraction` 1). 177 of the 185 `Row.Invoke` bad fixtures do
not compile (CS0103: the "call" names something that is not a method).

## Counts

| Verdict | Rows |
|---|---|
| WORKS | 15 |
| DEAD | 132 |
| NOISY | 51 |
| **Total** | **198** |

| Action | Rows |
|---|---|
| KEEP | 14 |
| REMOVE | 176 |
| OWNED-secrets | 4 |
| OWNED-flow | 3 |
| OWNED-duplication | 1 |

REMOVE = 175 DEAD/NOISY rows + `no-suppress-finalize` (WORKS, removed by decision D5). The 8 owned rows
are DEAD/NOISY too but stay in place for their plans.

## Symbol rows

| Id | Matcher | Target | Verdict | Size | Action | Note | Row |
|---|---|---|---|---|---|---|---|
| `assembly-version` | Invoke | `AssemblyVersion` | DEAD | M | REMOVE -> attributes.md (not worth reviving) | Attribute, never invoked. Real check = assembly lacks `[assembly: AssemblyVersion]`; SDK projects generate it, so it is meaningless for modern .NET. | SymbolEngine.cs:16 |
| `attribute-usage` | Invoke | `AttributeUsage` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: class deriving `Attribute` with no `[AttributeUsage]`. | SymbolEngine.cs:21 |
| `begin-invoke-end-invoke` | Invoke | `BeginInvoke` | NOISY | M | REMOVE -> async-threading.md | Fires on any `BeginInvoke` (incl. WinForms `Control.BeginInvoke`). Needs delegate-typed receiver and no `EndInvoke` in scope. | SymbolEngine.cs:24 |
| `blazor-query-param-type` | Invoke | `SupplyParameterFromQuery` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: property with `[SupplyParameterFromQuery]` whose type is outside the supported set. | SymbolEngine.cs:25 |
| `caller-info-last` | Invoke | `CallerMemberName` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: `[CallerMemberName/FilePath/LineNumber]` parameter followed by a non-caller-info parameter. | SymbolEngine.cs:26 |
| `client-evaluated-default` | Invoke | `HasDefaultValue` | NOISY | M | REMOVE -> aspnet-ef.md | Fires on every EF `HasDefaultValue`. Needs non-constant argument (e.g. `DateTime.Now`) where `HasDefaultValueSql` is meant. | SymbolEngine.cs:29 |
| `cls-compliant` | Invoke | `CLSCompliant` | DEAD | M | REMOVE -> attributes.md (not worth reviving) | Assembly attribute absence; needs compilation scope. Low value for modern .NET. | SymbolEngine.cs:30 |
| `com-visible-explicit` | Invoke | `ComVisible` | DEAD | M | REMOVE -> attributes.md (not worth reviving) | Assembly attribute absence; needs compilation scope. SDK sets ComVisible(false). | SymbolEngine.cs:31 |
| `comparable-equals` | Invoke | `CompareTo` | NOISY | M | REMOVE -> types-equality.md | Fires on every `CompareTo` call. Needs type implementing `IComparable(<T>)` without `Equals` override / comparison operators. | SymbolEngine.cs:32 |
| `configure-await-false` | Invoke | `ConfigureAwait` | NOISY | M | REMOVE -> async-threading.md | Fires on `ConfigureAwait(false)` itself - the compliant code (probe). Needs `await` of a Task with no `ConfigureAwait` in library code. | SymbolEngine.cs:35 |
| `constructor-argument-exists` | Invoke | `ConstructorArgument` | DEAD | M | REMOVE -> attributes.md (not worth reviving) | XAML `[ConstructorArgument]` attribute; niche. | SymbolEngine.cs:36 |
| `cookie-httponly` | Invoke | `HttpOnly` | DEAD | M | REMOVE -> security-config.md | `HttpOnly` is a property (probe). Needs `CookieOptions`/`HttpCookie` init with `HttpOnly = false` or never set. | SymbolEngine.cs:39 |
| `cookie-secure` | Invoke | `Secure` | DEAD | M | REMOVE -> security-config.md | `Secure` is a property. Same shape as cookie-httponly. | SymbolEngine.cs:40 |
| `coverage-exclusion-reason` | Invoke | `ExcludeFromCodeCoverage` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: `[ExcludeFromCodeCoverage]` without `Justification =`. | SymbolEngine.cs:41 |
| `csrf-enabled` | Invoke | `IgnoreAntiforgeryToken` | DEAD | S | REMOVE -> security-config.md | Attribute. Real: `[IgnoreAntiforgeryToken]` or `.DisableAntiforgery()` call. | SymbolEngine.cs:42 |
| `debugger-display-member` | Invoke | `DebuggerDisplay` | DEAD | M | REMOVE -> attributes.md | Attribute. Parse `{Member}` placeholders in `[DebuggerDisplay]` and resolve them on the type. | SymbolEngine.cs:43 |
| `default-parameter-value` | Invoke | `DefaultValue` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: parameter with `[DefaultValue]` but no `[DefaultParameterValue]`. | SymbolEngine.cs:44 |
| `default-value-type` | Invoke | `HasDefaultValue` | NOISY | M | REMOVE -> aspnet-ef.md | Fires on every EF `HasDefaultValue`. Needs argument type vs entity property type comparison. | SymbolEngine.cs:45 |
| `durable-entity-interface` | Invoke | `SignalEntity` | NOISY | M | REMOVE -> aspnet-ef.md (not worth reviving) | Fires on any `SignalEntity`. Durable-entity interface rules; niche Azure domain. | SymbolEngine.cs:53 |
| `dynamic-sql` | Invoke | `FromSqlRaw` | NOISY | S | REMOVE -> aspnet-ef.md | Fires on every `FromSqlRaw`, incl. constant SQL. Real: `FromSqlRaw`/`ExecuteSqlRaw` whose SQL arg is interpolated/concatenated. Overlaps sql-injection (taint). | SymbolEngine.cs:54 |
| `equatable-sealed` | Invoke | `IEquatable` | DEAD | S | REMOVE -> types-equality.md | `IEquatable` is an interface (probe). Real: non-sealed class whose base list has `IEquatable<T>`. | SymbolEngine.cs:56 |
| `equatable-value-type` | Invoke | `IEquatable` | DEAD | M | REMOVE -> types-equality.md | Interface. Real: struct not implementing `IEquatable<Self>`. | SymbolEngine.cs:57 |
| `event-args` | Invoke | `EventArgs` | DEAD | M | REMOVE -> types-equality.md | Type name. Real: event delegate whose 2nd parameter is not `EventArgs`-derived. | SymbolEngine.cs:58 |
| `event-handler-signature` | Invoke | `EventHandler` | DEAD | M | REMOVE -> types-equality.md | Type name. Real: event handler not `(object sender, TArgs e)`. | SymbolEngine.cs:59 |
| `field-used-as-local` | Invoke | `field` | DEAD | L | REMOVE -> api-design.md | `field` is a placeholder. Needs per-method dataflow: private field always written before read. | SymbolEngine.cs:61 |
| `generic-collection` | Invoke | `ICollection` | DEAD | M | REMOVE -> linq-collections.md | Interface. Real: type implementing non-generic `ICollection`/`IList`/`IEnumerable` only. | SymbolEngine.cs:62 |
| `generic-event-handler` | Invoke | `EventHandler` | DEAD | M | REMOVE -> types-equality.md | Type name. Real: event of a custom delegate type replaceable by `EventHandler<T>`. | SymbolEngine.cs:63 |
| `hashcode-immutable` | Invoke | `GetHashCode` | NOISY | M | REMOVE -> types-equality.md | Fires on every `GetHashCode` call (probe). Needs `GetHashCode` override that reads non-readonly fields. | SymbolEngine.cs:64 |
| `iserializable-correct` | Invoke | `ISerializable` | DEAD | M | REMOVE -> attributes.md (not worth reviving) | Interface. Legacy `ISerializable` pattern (BinaryFormatter-era). | SymbolEngine.cs:67 |
| `js-invokable-public` | Invoke | `JSInvokable` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: `[JSInvokable]` on a non-public method. | SymbolEngine.cs:68 |
| `logger-generic-matches-type` | Invoke | `ILogger` | DEAD | S | REMOVE -> logging.md | Type name. Real: `ILogger<T>` constructor parameter where `T` is not the containing type. | SymbolEngine.cs:78 |
| `logger-matches-type` | Invoke | `ILogger` | DEAD | M | REMOVE -> logging.md | Type name. Real: `CreateLogger<T>()`/`typeof(T)` mismatch with containing type. | SymbolEngine.cs:79 |
| `logger-private-static` | Invoke | `ILogger` | DEAD | S | REMOVE -> logging.md | Type name. Real: `ILogger` field not `private static readonly`. Conflicts with DI-injected instance loggers; decide before implementing. | SymbolEngine.cs:80 |
| `merge-include` | Invoke | `ThenInclude` | NOISY | L | REMOVE -> aspnet-ef.md (not worth reviving) | Fires on every `ThenInclude`. Needs EF model knowledge of reference vs collection navigations. | SymbolEngine.cs:81 |
| `migration-narrow-column` | Invoke | `AlterColumn` | NOISY | M | REMOVE -> aspnet-ef.md | Fires on every `AlterColumn`. Needs `maxLength` < `oldMaxLength` (or type narrowing) in a migration. | SymbolEngine.cs:82 |
| `neutral-resources-language` | Invoke | `NeutralResourcesLanguage` | DEAD | L | REMOVE -> attributes.md (not worth reviving) | Assembly attribute absence + .resx detection; outside file scope. | SymbolEngine.cs:83 |
| `no-anonymous-unsubscribe` | Invoke | `Unsubscribe` | DEAD | S | REMOVE -> types-equality.md | Unsubscribe is `-=`, not a call. Real: `event -= lambda/anonymous method`. | SymbolEngine.cs:84 |
| `no-cleartext-protocol` | Invoke | `http` | DEAD | S | REMOVE -> security-config.md | `http` placeholder. Real: string literal starting `http://`/`ftp://`/`telnet://` (exclude localhost, XML namespaces). | SymbolEngine.cs:90 |
| `no-conflicting-transparency` | Invoke | `SecurityCritical` | DEAD | S | REMOVE -> attributes.md (not worth reviving) | Attribute. CAS transparency is obsolete in .NET Core. | SymbolEngine.cs:91 |
| `no-custom-crypto` | Invoke | `Create on DES` | NOISY | M | REMOVE -> crypto.md | Fires on `DES.Create()` (a weak algorithm, not a custom one). Real: class deriving `HashAlgorithm`/`SymmetricAlgorithm`/`AsymmetricAlgorithm`. | SymbolEngine.cs:92 |
| `no-dangerous-get-handle` | Invoke | `DangerousGetHandle` | WORKS | S | KEEP | Any `DangerousGetHandle()` call is the finding. Fixture needs a real `SafeHandle` receiver. | SymbolEngine.cs:93 |
| `no-datetime-primary-key` | Invoke | `Key` | DEAD | M | REMOVE -> attributes.md | Attribute. Real: `[Key]` (or `Id` convention) on a `DateTime`/`DateTimeOffset` property. | SymbolEngine.cs:94 |
| `no-explicit-caller-info` | Invoke | `CallerMemberName` | DEAD | M | REMOVE -> attributes.md | Attribute. Real: call site passing an argument to a `[CallerMemberName]` parameter (needs symbol). | SymbolEngine.cs:99 |
| `no-gc-collect` | Invoke | `Collect on GC` | WORKS | S | KEEP | `GC.Collect()` is the finding. Fixture lacks `using System` (CS0103). | SymbolEngine.cs:101 |
| `no-get-executing-assembly` | Invoke | `GetExecutingAssembly` | WORKS | S | KEEP | Any `GetExecutingAssembly()` call. | SymbolEngine.cs:102 |
| `no-insecure-random` | Invoke | `Next on Random` | DEAD | M | REMOVE -> crypto.md | Receiver text is never `Random` for instance `Next` (probe: `new Random().Next()`, `r.Next()`, `Random.Shared.Next()` all silent). Needs `System.Random` use in a security context - judgment; consider an ask-the-user message. | SymbolEngine.cs:103 |
| `no-obsolete-base-type` | Invoke | `ArrayList` | DEAD | S | REMOVE -> linq-collections.md | Type name. Real: base list contains `ArrayList`/`Hashtable`/`CollectionBase`/`DictionaryBase`. | SymbolEngine.cs:107 |
| `no-optional-ref` | Invoke | `Optional` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: `[Optional]` on a `ref`/`out` parameter. | SymbolEngine.cs:108 |
| `no-public-temp-file` | Invoke | `GetTempPath` | WORKS | S | KEEP | `Path.GetTempPath()` is what the rule flags (approximation; could add `TMP`/`TEMP` env reads). | SymbolEngine.cs:111 |
| `no-suppress-finalize` | Invoke | `SuppressFinalize` | WORKS | S | REMOVE -> types-equality.md (not worth reviving) | Matches what the message says, but the advice contradicts the standard Dispose pattern (CA1816). Removed by decision D5. | SymbolEngine.cs:117 |
| `no-thread-static-initializer` | Invoke | `ThreadStatic` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: `[ThreadStatic]` field with an initializer. | SymbolEngine.cs:118 |
| `no-trace-write` | Invoke | `WriteLine on Trace` | WORKS | S | KEEP | Fires on `Trace.WriteLine`; misses `Trace.Write` (probe) - add it. | SymbolEngine.cs:120 |
| `no-trace-write-line-if` | Invoke | `WriteLineIf` | NOISY | S | REMOVE -> logging.md | Fires on any `WriteLineIf` (incl. `Debug`). Real: `Trace.WriteLineIf(sw.TraceX, ...)` where the arg is a `TraceSwitch` level property. | SymbolEngine.cs:121 |
| `no-weak-hash` | Invoke | `Create on MD5` | WORKS | S | KEEP | `MD5.Create()` fires; misses `SHA1`, `MD5.HashData`, `*CryptoServiceProvider` (probe) - extend. | SymbolEngine.cs:124 |
| `no-weak-tls` | Invoke | `Tls` | DEAD | S | REMOVE -> security-config.md | `Tls` is an enum member (probe). Real: member access `SslProtocols.Tls/Tls11/Ssl2/Ssl3`, `SecurityProtocolType.*` (use `Row.Member`). | SymbolEngine.cs:127 |
| `no-world-accessible-file` | Invoke | `SetAccessControl` | NOISY | M | REMOVE -> security-config.md | Fires on every `SetAccessControl`. Needs `Everyone`/`WorldSid` rule or `UnixFileMode.Other*`. | SymbolEngine.cs:128 |
| `no-xxe` | Invoke | `LoadXml` | NOISY | M | REMOVE -> security-config.md | Fires on `LoadXml` even with `XmlResolver = null` (probe); .NET Core is safe by default. Needs a non-null resolver or `DtdProcessing.Parse`. | SymbolEngine.cs:129 |
| `obsolete-needs-reason` | Invoke | `Obsolete` | DEAD | S | REMOVE -> attributes.md | Attribute (probe: `[Obsolete]` silent). Real: `[Obsolete]` with no message argument. | SymbolEngine.cs:131 |
| `one-way-operation-void` | Invoke | `OperationContract` | DEAD | S | REMOVE -> attributes.md (not worth reviving) | WCF attribute; legacy. | SymbolEngine.cs:132 |
| `optional-attribute-pair` | Invoke | `DefaultParameterValue` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: `[DefaultParameterValue]` without `[Optional]`. | SymbolEngine.cs:133 |
| `optional-field-deserialize` | Invoke | `OptionalField` | DEAD | S | REMOVE -> attributes.md (not worth reviving) | Attribute; legacy BinaryFormatter-era serialization. | SymbolEngine.cs:134 |
| `parameter-base-type` | Invoke | `Stream` | DEAD | L | REMOVE -> api-design.md | Type name. Needs usage analysis: parameter only uses members of a base type. | SymbolEngine.cs:137 |
| `prefer-awaitable` | Invoke | `Result` | DEAD | M | REMOVE -> async-threading.md | `Result` is a property. Real: `.Result`/`.Wait()`/`GetAwaiter().GetResult()` on a Task inside an async method. | SymbolEngine.cs:143 |
| `prefer-cancellation-token` | Invoke | `None on CancellationToken` | DEAD | M | REMOVE -> async-threading.md | `CancellationToken.None` is a property. Real: call without a token when a token overload exists and a token is in scope. | SymbolEngine.cs:144 |
| `prefer-generic` | Invoke | `ArrayList` | DEAD | S | REMOVE -> linq-collections.md | Type name. Real: use of `ArrayList`/`Hashtable`/non-generic `Queue`/`Stack` (symbol check on `System.Collections`). | SymbolEngine.cs:145 |
| `pure-returns-value` | Invoke | `Pure` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: `[Pure]` on a `void` method. | SymbolEngine.cs:148 |
| `regex-timeout` | Invoke | `Match on Regex` | NOISY | M | REMOVE -> expressions.md | Fires on static `Regex.Match` even with a timeout (probe); misses instance/ctor use. Needs Regex ctor/static call without `TimeSpan` and without `NonBacktracking`. | SymbolEngine.cs:151 |
| `remove-obsolete` | Invoke | `Obsolete` | DEAD | S | REMOVE -> attributes.md (not worth reviving) | Attribute. Real: any `[Obsolete]` member. Contradicts obsolete-needs-reason on the same code - see Decisions (resolved) in 01-placeholder-cleanup.md. | SymbolEngine.cs:152 |
| `request-size-limit` | Invoke | `DisableRequestSizeLimit` | DEAD | S | REMOVE -> security-config.md | Attribute. Real: `[DisableRequestSizeLimit]`, `[RequestSizeLimit(n > 8 MB)]`, `MaxRequestBodySize = null`. | SymbolEngine.cs:153 |
| `request-validation` | Invoke | `ValidateInput` | DEAD | S | REMOVE -> security-config.md (not worth reviving) | Attribute; ASP.NET classic `[ValidateInput(false)]`. | SymbolEngine.cs:154 |
| `restrict-cors` | Invoke | `AllowAnyOrigin` | WORKS | S | KEEP | Any `AllowAnyOrigin()` call is the finding. | SymbolEngine.cs:155 |
| `restrict-deserialized-types` | Invoke | `Deserialize on BinaryFormatter` | DEAD | M | REMOVE -> security-config.md | Receiver text never `BinaryFormatter` (probe). Needs receiver symbol type + absence of a `SerializationBinder`. | SymbolEngine.cs:156 |
| `restrictive-csp` | Invoke | `ContentSecurityPolicy` | DEAD | M | REMOVE -> security-config.md | No such method in practice. Real: CSP header literal containing `unsafe-inline`/`unsafe-eval`/`*`. | SymbolEngine.cs:157 |
| `secure-cipher-mode` | Invoke | `CreateEncryptor` | NOISY | M | REMOVE -> crypto.md | Fires on every `CreateEncryptor`. Real: `CipherMode.ECB` / RSA `Pkcs1` encryption padding. | SymbolEngine.cs:160 |
| `secure-temp-file` | Invoke | `GetTempFileName` | WORKS | S | KEEP | `Path.GetTempFileName()` is the finding. | SymbolEngine.cs:161 |
| `serialization-callback` | Attribute | `OnDeserialized` | NOISY | S | REMOVE -> attributes.md | `Row.Attribute`: fires on every `[OnDeserialized]` (probe). Real: callback method not `void M(StreamingContext)` / public. | SymbolEngine.cs:162 |
| `service-and-operation-contract` | Invoke | `ServiceContract` | DEAD | S | REMOVE -> attributes.md (not worth reviving) | WCF attributes; legacy. | SymbolEngine.cs:163 |
| `set-locale` | Invoke | `CultureInfo` | DEAD | M | REMOVE -> aspnet-ef.md (not worth reviving) | `CultureInfo` is a creation. Real target is `DataTable`/`DataSet` without `Locale`; legacy. | SymbolEngine.cs:164 |
| `single-key-attribute` | Invoke | `Key` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: more than one `[Key]` property in a type. | SymbolEngine.cs:166 |
| `sta-thread` | Invoke | `STAThread` | DEAD | M | REMOVE -> attributes.md | Attribute. Real: WinForms `Main` (calls `Application.Run`) without `[STAThread]`. | SymbolEngine.cs:167 |
| `strong-crypto-key` | Invoke | `GenerateKey` | NOISY | M | REMOVE -> crypto.md | Fires on every `GenerateKey`. Real: RSA/DSA key size < 2048 (`RSA.Create(1024)`, `KeySize = 1024`). | SymbolEngine.cs:173 |
| `suppress-finalize-needs-destructor` | Invoke | `SuppressFinalize` | NOISY | M | REMOVE -> types-equality.md | Fires on every `SuppressFinalize`. Needs containing type (and source bases) with no finalizer. | SymbolEngine.cs:175 |
| `thread-static-on-static` | Invoke | `ThreadStatic` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: `[ThreadStatic]` on a non-static field. | SymbolEngine.cs:177 |
| `unpredictable-iv` | Invoke | `GenerateIV` | NOISY | M | REMOVE -> crypto.md | Fires on `GenerateIV()` - the compliant call (probe). Real: CBC `IV` set from a constant/non-random buffer. | SymbolEngine.cs:180 |
| `uri-not-string-argument` | Invoke | `Uri` | DEAD | M | REMOVE -> uri.md | Type name. Real: calling a string-URI overload when a `Uri` overload exists. | SymbolEngine.cs:188 |
| `uri-overload-calls-uri` | Invoke | `Uri` | DEAD | M | REMOVE -> uri.md | Type name. Real: string overload that does not delegate to the `Uri` overload. | SymbolEngine.cs:189 |
| `uri-parameter-not-string` | Invoke | `Uri` | DEAD | S | REMOVE -> uri.md | Type name. Real: `string` parameter named `*uri*`/`*url*`. | SymbolEngine.cs:190 |
| `uri-property-not-string` | Invoke | `Uri` | DEAD | S | REMOVE -> uri.md | Type name. Real: `string` property named `*Uri*`/`*Url*`. | SymbolEngine.cs:191 |
| `uri-return-not-string` | Invoke | `Uri` | DEAD | S | REMOVE -> uri.md | Type name. Real: method named `*Uri*`/`*Url*` returning `string`. | SymbolEngine.cs:192 |
| `use-lambda-parameter` | Invoke | `GetOrAdd` | NOISY | M | REMOVE -> async-threading.md | Fires on every `GetOrAdd`. Needs lambda body capturing the outer key argument instead of its own parameter. | SymbolEngine.cs:194 |
| `validate-on-deserialize` | Invoke | `OnDeserialized` | DEAD | L | REMOVE -> attributes.md (not worth reviving) | Attribute; vague ("validate data") legacy-serialization rule. | SymbolEngine.cs:196 |
| `validate-xml-signature` | Invoke | `CheckSignature` | NOISY | M | REMOVE -> crypto.md | Fires on every `CheckSignature`. Real: `SignedXml.CheckSignature()` with no key argument. | SymbolEngine.cs:197 |
| `verify-server-certificate` | Invoke | `ServerCertificateValidationCallback` | DEAD | M | REMOVE -> security-config.md | Property assignment, not a call (probe). Real: `ServerCertificate(Custom)ValidationCallback` assigned a lambda returning constant `true`. | SymbolEngine.cs:198 |
| `wrap-native-method` | Invoke | `DllImport` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: non-private `extern` method with `[DllImport]`/`[LibraryImport]`. | SymbolEngine.cs:199 |

## Syntax rows

| Id | Matcher | Target | Verdict | Size | Action | Note | Row |
|---|---|---|---|---|---|---|---|
| `accessor-expected-field` | Invoke | `get` | DEAD | M | REMOVE -> api-design.md | `get` placeholder. Real: property `A` whose getter/setter touches field `_b` while `_a` exists. | SyntaxEngine.cs:26 |
| `argument-exception-name` | Invoke | `ArgumentException` | DEAD | S | REMOVE -> api-design.md | Creation, not a call. Real: `new Argument*Exception(..., "x")` where `x` is not a parameter (use `Row.Create` + arg check). | SyntaxEngine.cs:27 |
| `argument-order` | Invoke | `M` | DEAD | M | REMOVE -> api-design.md | `M` placeholder; fires only on a user method named `M`. Real: arguments named like parameters but passed in a different order. | SyntaxEngine.cs:28 |
| `auto-property` | Invoke | `get` | DEAD | S | REMOVE -> api-design.md | `get` placeholder. Real: property whose accessors only read/write one backing field. | SyntaxEngine.cs:29 |
| `collection-size-compare` | Invoke | `Count on Enumerable` | DEAD | S | REMOVE -> linq-collections.md | Matches only the static `Enumerable.Count(xs)` form (probe). Real: `Count/Length >= 0` or `< 0`. | SyntaxEngine.cs:35 |
| `concise-declaration` | Invoke | `var` | DEAD | M | REMOVE -> api-design.md (not worth reviving) | `var` placeholder; message too vague to implement as stated. | SyntaxEngine.cs:37 |
| `controller-route` | Invoke | `Route` | DEAD | S | REMOVE -> aspnet-ef.md | Attribute. Real: controller without `[Route]` whose actions carry route templates. | SyntaxEngine.cs:41 |
| `database-password` | Invoke | `Password` | DEAD | M | OWNED-secrets | `Password` placeholder. | SyntaxEngine.cs:43 |
| `datetime-utc` | Invoke | `Now on DateTime` | DEAD | S | REMOVE -> expressions.md | `Now` is a property (probe). Real: `DateTime.Now` member access (`Row.Member`). One of four rows on the same expression - see Decisions (resolved) in 01-placeholder-cleanup.md. | SyntaxEngine.cs:45 |
| `empty-statement` | Kinds | `EmptyStatement` | WORKS | S | KEEP | `Row.Kinds(EmptyStatement)` is the whole check. | SyntaxEngine.cs:51 |
| `enum-not-reserved` | Ident | `Reserved` | NOISY | S | REMOVE -> keywords-enums.md | `Row.Ident`: fires on any identifier `Reserved`, e.g. a property (probe). Restrict to enum member declarations. | SyntaxEngine.cs:55 |
| `escape-extension-keyword` | Ident | `extension` | NOISY | S | REMOVE -> keywords-enums.md | `Row.Ident`: fires on every local/parameter named `extension` (probe, and 2x in qlcheck's own tests). Restrict to type declarations/type references. | SyntaxEngine.cs:56 |
| `escape-field-keyword` | Ident | `field` | NOISY | S | REMOVE -> keywords-enums.md | `Row.Ident`: fires on every `field` identifier (13x in qlcheck's own source). Restrict to uses inside property accessors. | SyntaxEngine.cs:57 |
| `escape-partial-return` | Ident | `partial` | NOISY | S | REMOVE -> keywords-enums.md | `Row.Ident`: any identifier `partial`. Restrict to a method return type named `partial`. | SyntaxEngine.cs:58 |
| `escape-scoped` | Ident | `scoped` | NOISY | S | REMOVE -> keywords-enums.md | `Row.Ident`: any identifier `scoped`. Restrict to parenthesized lambda parameter lists. | SyntaxEngine.cs:59 |
| `filter-before-sort` | Invoke | `OrderBy on Enumerable` | DEAD | S | REMOVE -> linq-collections.md | Static `Enumerable.OrderBy` form only. Real: `.OrderBy(..).Where(..)` extension chain. | SyntaxEngine.cs:67 |
| `flags-attribute-only-flags` | Invoke | `Flags` | DEAD | S | REMOVE -> keywords-enums.md | Attribute. Real: `[Flags]` enum whose values are not powers of two / combinations. | SyntaxEngine.cs:69 |
| `flags-initialized` | Invoke | `Flags` | DEAD | S | REMOVE -> keywords-enums.md | Attribute. Real: `[Flags]` enum member without an initializer. | SyntaxEngine.cs:70 |
| `flags-none` | Invoke | `None` | DEAD | S | REMOVE -> keywords-enums.md | Enum member name (probe). Real: `[Flags]` enum zero member not named `None`. | SyntaxEngine.cs:71 |
| `group-overloads` | Invoke | `M` | DEAD | S | REMOVE -> api-design.md | `M` placeholder. Real: same-name methods not adjacent in a type. | SyntaxEngine.cs:76 |
| `hardcoded-credential` | Invoke | `Password` | DEAD | M | OWNED-secrets | `Password` placeholder. | SyntaxEngine.cs:77 |
| `hardcoded-ip` | Invoke | `Parse on IPAddress` | NOISY | S | REMOVE -> security-config.md | Fires on `IPAddress.Parse(config)` (probe); misses literals. Real: string literal that is an IP (skip loopback/0.0.0.0/versions). | SyntaxEngine.cs:78 |
| `hardcoded-secret` | Invoke | `Secret` | DEAD | M | OWNED-secrets | `Secret` placeholder. | SyntaxEngine.cs:79 |
| `http-verb-attribute` | Invoke | `HttpGet` | DEAD | M | REMOVE -> aspnet-ef.md | Attribute. Real: public controller action without any `Http*` attribute. | SyntaxEngine.cs:80 |
| `identical-method` | Invoke | `M` | DEAD | M | OWNED-duplication | `M` placeholder; fires only on a user method named `M`. Real: two methods with identical normalized bodies. Reimplemented by plan 04. | SyntaxEngine.cs:84 |
| `implement-partial` | Invoke | `partial` | DEAD | M | REMOVE -> api-design.md | `partial` placeholder. Real: partial method declaration with no implementation part (symbol). | SyntaxEngine.cs:86 |
| `indexof-not-positive` | Invoke | `IndexOf` | NOISY | S | REMOVE -> expressions.md | Fires on every `IndexOf` (2x in qlcheck's own source). Real: `IndexOf(..) > 0` comparison. | SyntaxEngine.cs:89 |
| `logger-name` | Invoke | `logger` | DEAD | S | REMOVE -> logging.md | `logger` placeholder. Real: `ILogger` field not named `log`/`_log`. Opinionated vs common `_logger` - decide before implementing. | SyntaxEngine.cs:93 |
| `model-binding` | Invoke | `Form` | DEAD | S | REMOVE -> aspnet-ef.md | `Form` is a property. Real: `Request.Form`/`Request.Query` access in a controller (`Row.Member`). | SyntaxEngine.cs:98 |
| `no-array-covariance` | Invoke | `Copy` | NOISY | M | REMOVE -> linq-collections.md | Fires on every `Copy` incl. `File.Copy`. Real: implicit `Derived[]` -> `Base[]` conversion. | SyntaxEngine.cs:105 |
| `no-array-for-params` | Invoke | `ToArray on Enumerable` | DEAD | M | REMOVE -> linq-collections.md | Static `Enumerable.ToArray` form only. Real: explicit array passed to a `params` parameter. | SyntaxEngine.cs:106 |
| `no-async-identifier` | Ident | `async` | WORKS | S | KEEP | Identifier `async` (the modifier is not flagged - probe). Misses `await` - add it. | SyntaxEngine.cs:108 |
| `no-backslash-route` | Invoke | `HttpGet` | DEAD | S | REMOVE -> aspnet-ef.md | Attribute. Real: route template literal containing `\\`. | SyntaxEngine.cs:109 |
| `no-bitwise-non-flags` | Invoke | `HasFlag` | NOISY | M | REMOVE -> keywords-enums.md | Fires on every `HasFlag` (not a bitwise op). Real: `&`/`/` on an enum type without `[Flags]`. | SyntaxEngine.cs:110 |
| `no-case-only-field-difference` | Invoke | `value` | DEAD | M | REMOVE -> api-design.md | `value` placeholder. Real: field differing from a base-class field only by case. | SyntaxEngine.cs:111 |
| `no-collection-copy-property` | Invoke | `ToArray on Enumerable` | DEAD | S | REMOVE -> linq-collections.md | Static form only. Real: property getter returning `.ToArray()`/`.ToList()`/`new List<>(..)`. | SyntaxEngine.cs:114 |
| `no-datetime-now-timing` | Invoke | `Now on DateTime` | DEAD | S | REMOVE -> expressions.md | `Now` is a property. Real: `DateTime.Now - start` style timing. | SyntaxEngine.cs:119 |
| `no-default-argument` | Invoke | `M` | DEAD | M | REMOVE -> api-design.md | `M` placeholder. Real: argument equal to the parameter's default value. | SyntaxEngine.cs:120 |
| `no-delegate-subtraction` | Invoke | `Remove` | NOISY | M | REMOVE -> types-equality.md | Fires on every `Remove` incl. `List.Remove` (probe; 1x in qlcheck's own source). Real: `-`/`-=` on delegate-typed (non-event) operands. | SyntaxEngine.cs:122 |
| `no-duplicate-cast` | Invoke | `ChangeType` | NOISY | M | REMOVE -> expressions.md | Fires on every `ChangeType`. Real: `x is T` followed by `(T)x`. | SyntaxEngine.cs:124 |
| `no-empty-collection-access` | Invoke | `First on Enumerable` | DEAD | L | REMOVE -> linq-collections.md | Static form only. Needs flow knowledge that a collection is empty. | SyntaxEngine.cs:127 |
| `no-expected-exception` | Invoke | `ExpectedException` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: any `[ExpectedException]` (`Row.Attribute` is the whole check). | SyntaxEngine.cs:135 |
| `no-fixme` | Trivia | `FIXME` | WORKS | S | KEEP | `Row.Trivia(FIXME)`. | SyntaxEngine.cs:139 |
| `no-goto` | Kinds | `GotoStatement on GotoCaseStatement on GotoDefaultStatement` | WORKS | S | KEEP | `Row.Kinds(goto...)`. | SyntaxEngine.cs:141 |
| `no-hardcoded-jwt-secret` | Invoke | `SigningKey` | DEAD | M | OWNED-secrets | `SigningKey` placeholder. | SyntaxEngine.cs:144 |
| `no-hardcoded-uri` | Invoke | `Uri` | DEAD | S | REMOVE -> uri.md | Creation, not call. Real: absolute URI/path string literal assigned or passed. | SyntaxEngine.cs:145 |
| `no-ignored-initial-value` | Invoke | `value` | DEAD | L | REMOVE -> api-design.md | `value` placeholder. Needs dataflow: parameter/catch/foreach variable overwritten before read. | SyntaxEngine.cs:148 |
| `no-inline-suppression` | Invoke | `SuppressMessage` | DEAD | S | REMOVE -> attributes.md | Attribute. Real: `[SuppressMessage]` and `#pragma warning disable`. | SyntaxEngine.cs:151 |
| `no-is-this` | Invoke | `ReferenceEquals` | NOISY | S | REMOVE -> keywords-enums.md | Fires on every `ReferenceEquals`. Real: `this is T` expression. | SyntaxEngine.cs:153 |
| `no-lambda-in-blazor-loop` | Invoke | `onclick` | DEAD | L | REMOVE -> aspnet-ef.md (not worth reviving) | Razor markup; `.razor` files are not scanned. | SyntaxEngine.cs:154 |
| `no-literal-localized-arg` | Invoke | `Localized` | DEAD | M | REMOVE -> api-design.md | `Localized` placeholder. Real: literal passed to a `[Localizable(true)]` parameter. | SyntaxEngine.cs:156 |
| `no-multidimensional-array` | Invoke | `GetLength` | NOISY | S | REMOVE -> linq-collections.md | Fires on every `GetLength`. Real: `T[,]` array type usage. | SyntaxEngine.cs:157 |
| `no-nan-compare` | Invoke | `IsNaN` | NOISY | S | REMOVE -> expressions.md | Fires on `double.IsNaN` - the fix itself (probe). Real: `== / != double.NaN`. | SyntaxEngine.cs:158 |
| `no-new-guid` | Invoke | `Guid` | DEAD | S | REMOVE -> expressions.md | Creation (probe). Real: `new Guid()` with no arguments. | SyntaxEngine.cs:164 |
| `no-optional-parameter` | Invoke | `optional` | DEAD | S | REMOVE -> api-design.md | `optional` placeholder. Real: parameter with a default value (public members). | SyntaxEngine.cs:168 |
| `no-overlapping-overload` | Invoke | `M` | DEAD | M | REMOVE -> api-design.md | `M` placeholder. Real: overloads made ambiguous by default parameter values. | SyntaxEngine.cs:170 |
| `no-public-multidimensional` | Invoke | `GetLength` | NOISY | S | REMOVE -> linq-collections.md | Fires on every `GetLength`. Real: public method parameter of rank > 1 array. | SyntaxEngine.cs:175 |
| `no-public-pointer` | Invoke | `DangerousGetHandle` | NOISY | S | REMOVE -> types-equality.md | Fires on `DangerousGetHandle`. Real: non-private field of `IntPtr`/`UIntPtr`/pointer type. | SyntaxEngine.cs:176 |
| `no-recursive-inheritance` | Invoke | `C` | DEAD | M | REMOVE -> types-equality.md (not worth reviving) | `C` placeholder; recursive generic expansion is very rare. | SyntaxEngine.cs:179 |
| `no-rooted-action-route` | Invoke | `HttpGet` | DEAD | S | REMOVE -> aspnet-ef.md | Attribute. Real: action route template starting with `/`. | SyntaxEngine.cs:186 |
| `no-shadow` | Invoke | `value` | DEAD | M | REMOVE -> api-design.md | `value` placeholder (probe). Real: local named like a field/property of the containing type. | SyntaxEngine.cs:188 |
| `no-shadow-outer-static` | Invoke | `value` | DEAD | M | REMOVE -> api-design.md | `value` placeholder. Real: nested-type member shadowing an outer static member. | SyntaxEngine.cs:189 |
| `no-todo` | Trivia | `TODO` | WORKS | S | KEEP | `Row.Trivia(TODO)`. | SyntaxEngine.cs:195 |
| `no-unsafe` | Kinds | `UnsafeStatement` | WORKS | S | KEEP | `Row.Kinds(UnsafeStatement)`; misses `unsafe` member/type modifiers (probe) - extend. | SyntaxEngine.cs:198 |
| `no-write-only-property` | Invoke | `set` | DEAD | S | REMOVE -> api-design.md | `set` placeholder. Real: property with a setter and no getter. | SyntaxEngine.cs:202 |
| `null-forgiving-needs-nullable` | Invoke | `value` | DEAD | M | REMOVE -> keywords-enums.md | `value` placeholder. Real: `!` where the nullable context is disabled. | SyntaxEngine.cs:203 |
| `omit-redundant-name` | Invoke | `value` | DEAD | S | REMOVE -> api-design.md | `value` placeholder. Real: anonymous object `new { Name = x.Name }`. | SyntaxEngine.cs:204 |
| `parameter-name-matches-base` | Invoke | `value` | DEAD | M | REMOVE -> api-design.md | `value` placeholder. Real: override/implementation parameter renamed vs base. | SyntaxEngine.cs:210 |
| `parameter-name-not-method` | Invoke | `M` | DEAD | S | REMOVE -> api-design.md | `M` placeholder. Real: parameter named like its method. | SyntaxEngine.cs:211 |
| `prefer-any` | Invoke | `Count on Enumerable` | DEAD | S | REMOVE -> linq-collections.md | Static form only (probe: `xs.Count() == 0` silent). Real: `Count() == 0` / `> 0` on IEnumerable. | SyntaxEngine.cs:214 |
| `prefer-char-overload` | Invoke | `StartsWith on string` | DEAD | S | REMOVE -> expressions.md | Receiver text `string` never matches an instance call (probe). Real: `StartsWith/EndsWith` with a 1-char string literal. | SyntaxEngine.cs:215 |
| `prefer-datetimeoffset` | Invoke | `Now on DateTime` | DEAD | M | REMOVE -> expressions.md (not worth reviving) | `Now` is a property. Real target is unclear (every `DateTime` use?) - see Decisions (resolved) in 01-placeholder-cleanup.md. | SyntaxEngine.cs:217 |
| `prefer-find` | Invoke | `FirstOrDefault on Enumerable` | DEAD | M | REMOVE -> linq-collections.md | Static form only. Real: `FirstOrDefault(pred)` on `List<T>` (symbol). | SyntaxEngine.cs:219 |
| `prefer-first-not-or-default` | Invoke | `FirstOrDefault on Enumerable` | DEAD | L | REMOVE -> linq-collections.md (not worth reviving) | Needs knowledge that a collection is non-empty. | SyntaxEngine.cs:220 |
| `prefer-index` | Invoke | `ElementAt` | NOISY | M | REMOVE -> linq-collections.md | Fires on every `ElementAt`. Real: `ElementAt/First/Last` on an `IList<T>` receiver. | SyntaxEngine.cs:222 |
| `prefer-linked-list-ends` | Invoke | `First on LinkedList` | DEAD | M | REMOVE -> linq-collections.md | Static form only. Real: `First()/Last()` on a `LinkedList<T>`. | SyntaxEngine.cs:224 |
| `prefer-params` | Invoke | `ArgIterator` | DEAD | S | REMOVE -> api-design.md | Type name. Real: `__arglist` parameter. | SyntaxEngine.cs:227 |
| `prefer-range` | Invoke | `Substring` | NOISY | M | REMOVE -> expressions.md | Fires on every `Substring`. Real: `Substring(i)` passed where a span/range overload exists. | SyntaxEngine.cs:229 |
| `prefer-string-create` | Invoke | `FormattableString` | DEAD | S | REMOVE -> expressions.md | Type name. Real: `FormattableString.Invariant(..)` / `FormattableString` parameter. | SyntaxEngine.cs:231 |
| `readonly-collection-property` | Invoke | `IList` | DEAD | M | REMOVE -> linq-collections.md | Interface. Real: public settable property of a collection type. | SyntaxEngine.cs:235 |
| `redundant-cast` | Invoke | `ChangeType` | NOISY | M | REMOVE -> expressions.md | Fires on every `ChangeType`. Real: cast to the expression's own type. | SyntaxEngine.cs:239 |
| `redundant-null-forgiving` | Invoke | `value` | DEAD | M | REMOVE -> keywords-enums.md | `value` placeholder. Real: `!` on a non-nullable expression. | SyntaxEngine.cs:241 |
| `redundant-to-array` | Invoke | `ToCharArray` | NOISY | S | REMOVE -> linq-collections.md | Fires on every `ToCharArray`. Real: `foreach`/indexer directly on `s.ToCharArray()`. | SyntaxEngine.cs:243 |
| `regular-number-pattern` | Invoke | `Match on Regex` | NOISY | S | REMOVE -> expressions.md | Wrong target: fires on static `Regex.Match`; the rule is about irregular `_` digit grouping in numeric literals. | SyntaxEngine.cs:245 |
| `required-value-input` | Attribute | `Required` | NOISY | M | REMOVE -> aspnet-ef.md | `Row.Attribute`: fires on every `[Required]` (probe) - inverted. Real: value-type input-model property that is not nullable/`required`/`[JsonRequired]`. | SyntaxEngine.cs:246 |
| `route-constraint-type` | Invoke | `Parameter` | DEAD | M | REMOVE -> aspnet-ef.md (not worth reviving) | Blazor `@page` route constraints live in `.razor` files, which are not scanned. | SyntaxEngine.cs:248 |
| `safe-cast` | Invoke | `ChangeType` | NOISY | M | REMOVE -> expressions.md | Fires on every `ChangeType`. Real: cast between provably unrelated types. | SyntaxEngine.cs:249 |
| `single-orderby` | Invoke | `OrderBy on Enumerable` | DEAD | S | REMOVE -> linq-collections.md | Static form only. Real: `.OrderBy(..).OrderBy(..)` chain. | SyntaxEngine.cs:258 |
| `sql-keyword-whitespace` | Invoke | `FromSqlRaw` | NOISY | S | REMOVE -> aspnet-ef.md | Fires on every `FromSqlRaw`. Real: concatenated SQL pieces with no whitespace at a keyword boundary. | SyntaxEngine.cs:259 |
| `testable-clock` | Invoke | `Now on DateTime` | DEAD | S | REMOVE -> expressions.md | `Now` is a property. Real: `DateTime.Now/UtcNow`, `DateTimeOffset.Now` member access. | SyntaxEngine.cs:268 |
| `timezone-id-direct` | Invoke | `FindSystemTimeZoneById` | NOISY | S | REMOVE -> expressions.md | Fires on `FindSystemTimeZoneById` - the recommended call (probe). Real: `TZConvert.*` use. | SyntaxEngine.cs:270 |
| `type-name-not-namespace` | Invoke | `System` | DEAD | S | REMOVE -> types-equality.md | `System` placeholder. Real: type named like a well-known namespace segment. | SyntaxEngine.cs:272 |
| `type-param-in-signature` | Invoke | `M` | DEAD | S | REMOVE -> api-design.md | `M` placeholder. Real: method type parameter absent from the parameter list. | SyntaxEngine.cs:274 |
| `unassigned-member` | Invoke | `value` | DEAD | M | REMOVE -> api-design.md | `value` placeholder. Real: private field/auto-property never assigned. | SyntaxEngine.cs:275 |
| `unused-type-parameter` | Invoke | `T` | DEAD | S | REMOVE -> api-design.md | `T` placeholder. Real: type parameter never referenced. | SyntaxEngine.cs:277 |
| `use-unix-epoch` | Invoke | `UnixEpoch` | DEAD | S | REMOVE -> expressions.md | `UnixEpoch` is a field and is the fix (probe). Real: `new DateTime(1970, 1, 1, ...)`. | SyntaxEngine.cs:280 |
| `use-value-keyword` | Invoke | `value` | DEAD | S | REMOVE -> api-design.md | `value` placeholder. Real: setter that never reads `value`. | SyntaxEngine.cs:281 |
| `valid-regex` | Invoke | `Match on Regex` | NOISY | S | REMOVE -> expressions.md | Fires on every static `Regex.Match`. Real: constant pattern that fails `new Regex(pattern)` at analysis time. | SyntaxEngine.cs:283 |
| `validate-before-async` | Invoke | `await` | DEAD | S | REMOVE -> async-threading.md | `await` placeholder. Real: async method throwing `Argument*Exception` before its first await. | SyntaxEngine.cs:284 |
| `validate-before-yield` | Invoke | `yield` | DEAD | S | REMOVE -> async-threading.md | `yield` placeholder. Real: iterator method throwing `Argument*Exception`. | SyntaxEngine.cs:285 |
| `validate-public-arguments` | Invoke | `ArgumentNullException` | DEAD | M | REMOVE -> api-design.md (revived by plan 02) | Creation, not a call. Real: public method dereferencing a reference parameter without a null check; overlaps the nullable plan. | SyntaxEngine.cs:286 |

## Flow rows

| Id | Matcher | Target | Verdict | Size | Action | Note | Row |
|---|---|---|---|---|---|---|---|
| `lock-released` | Invoke | `Enter on Monitor` | NOISY | L | OWNED-flow | Fires on every `Monitor.Enter`, incl. a correct try/finally (probe). Needs per-path CFG check that each Enter/`lock`-free acquire has a matching Exit. | FlowEngine.cs:13 |
| `matching-lock-release` | Invoke | `Exit on Monitor` | NOISY | L | OWNED-flow | Fires on every `Monitor.Exit`; message is about `ReaderWriterLock(Slim)` read/write pairing - wrong API entirely. | FlowEngine.cs:18 |
| `release-lock-same-method` | Invoke | `Enter on Monitor` | NOISY | M | OWNED-flow | Fires on every `Monitor.Enter`; needs Enter without Exit in the same method body. | FlowEngine.cs:22 |
