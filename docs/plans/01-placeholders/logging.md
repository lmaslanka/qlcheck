# Logging and tracing - revival plans

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Part of `../01-placeholder-cleanup.md`. Every check here was removed in that plan because its
`Row.Invoke`/`Row.Attribute`/`Row.Ident` placeholder was DEAD or NOISY (see `../01-placeholder-triage.md`).

Each section is a standalone plan to bring the check back for real. 5 removed checks: 5 to revive, 0 not worth reviving.

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

## `logger-generic-matches-type`

Original message: "Generic logger injection should match enclosing type."  
Removed row: `Row.Invoke` on `ILogger` (SymbolEngine.cs:78).

**Detect.** A constructor or primary-constructor parameter of type `ILogger<T>` where `T` is not the containing type.

Positive (finding):
```csharp
class OrderService(ILogger<UserService> log) { }
```
Negative (no finding):
```csharp
class OrderService(ILogger<OrderService> log) { }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `Parameter`s of constructors/primary constructors; `Microsoft.Extensions.Logging.ILogger<T>` type argument compared with the containing `INamedTypeSymbol` (allow base types? see question).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Allow `T` = a base class of the containing type? Recommend no.
- `Microsoft.Extensions.Logging.Abstractions` is not in the TPA set; fixture needs a stub `ILogger<T>`.

## `logger-matches-type`

Original message: "Loggers should be named for their enclosing types."  
Removed row: `Row.Invoke` on `ILogger` (SymbolEngine.cs:79).

**Detect.** `loggerFactory.CreateLogger<T>()`/`CreateLogger(typeof(T))`/`LogManager.GetLogger(typeof(T))` where `T` is not the containing type, or `CreateLogger("literal")` not equal to the type's name.

Positive (finding):
```csharp
class OrderService { readonly ILogger _log = factory.CreateLogger<UserService>(); }
```
Negative (no finding):
```csharp
class OrderService { readonly ILogger _log = factory.CreateLogger<OrderService>(); }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `InvocationExpression` named `CreateLogger`/`GetLogger`; compare the generic type argument or `typeof` operand symbol with the containing type.

**Fixtures.** bad = positive, good = negative.

## `logger-name`

Original message: "Name the logger field `log` or `_log`."  
Removed row: `Row.Invoke` on `logger` (SyntaxEngine.cs:93).

**Detect.** A field of logger type not named `log` or `_log`.

Positive (finding):
```csharp
private readonly ILogger<C> _logger;
```
Negative (no finding):
```csharp
private readonly ILogger<C> _log;
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `FieldDeclaration` with logger type; compare identifier.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- `_logger` is the dominant .NET convention (templates generate it). Keep the check's `log`/`_log` choice, allow both, or drop the check? Recommend: allow `log`, `_log`, `logger`, `_logger` - or drop it.

## `logger-private-static`

Original message: "Logger fields should be `private static readonly`."  
Removed row: `Row.Invoke` on `ILogger` (SymbolEngine.cs:80).

**Detect.** A field of a logger type (`ILogger`, `ILog`, NLog `Logger`, Serilog `ILogger`) that is static but not `private static readonly`. Instance fields set from DI are exempt.

Positive (finding):
```csharp
public static ILogger Log = LogManager.GetCurrentClassLogger();
```
Negative (no finding):
```csharp
private static readonly ILogger Log = LogManager.GetCurrentClassLogger();
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `FieldDeclaration`; logger type by symbol name list; only `static` fields; require `private` + `readonly`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- The original message says all logger fields; that conflicts with DI-injected `ILogger<T>` instance fields. Recommend restricting to static fields as above.

## `no-trace-write-line-if`

Original message: "`Trace.WriteLineIf` should not be used with `TraceSwitch` levels."  
Removed row: `Row.Invoke` on `WriteLineIf` (SymbolEngine.cs:121).

**Detect.** `Trace.WriteLineIf(sw.TraceError|TraceWarning|TraceInfo|TraceVerbose, ...)` where the condition is a `TraceSwitch` level property - the switch level is already applied by listeners; use `TraceSource`.

Positive (finding):
```csharp
Trace.WriteLineIf(appSwitch.TraceInfo, "started");
```
Negative (no finding):
```csharp
traceSource.TraceEvent(TraceEventType.Information, 0, "started");
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `Trace.WriteLineIf`/`WriteIf` invocations (symbol on `System.Diagnostics.Trace`); first argument is a member access to one of those properties on a `TraceSwitch`.

**Fixtures.** bad = positive, good = negative.

