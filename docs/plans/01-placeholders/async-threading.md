# Async and threading - revival plans

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Part of `../01-placeholder-cleanup.md`. Every check here was removed in that plan because its
`Row.Invoke`/`Row.Attribute`/`Row.Ident` placeholder was DEAD or NOISY (see `../01-placeholder-triage.md`).

Each section is a standalone plan to bring the check back for real. 7 removed checks: 7 to revive, 0 not worth reviving.

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

## `begin-invoke-end-invoke`

Original message: "Calls to delegate's method `BeginInvoke` should be paired with calls to `EndInvoke`."  
Removed row: `Row.Invoke` on `BeginInvoke` (SymbolEngine.cs:24).

**Detect.** `BeginInvoke` on a delegate-typed receiver with no matching `EndInvoke` on the same delegate in the method or in a callback passed as the `AsyncCallback` argument. (Delegate `BeginInvoke` throws `PlatformNotSupportedException` on .NET Core.)

Positive (finding):
```csharp
Func<int> work = Compute; work.BeginInvoke(null, null);
```
Negative (no finding):
```csharp
Func<int> work = Compute; var r = work.BeginInvoke(null, null); work.EndInvoke(r);
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `InvocationExpression` named `BeginInvoke`; receiver `TypeKind.Delegate`; search the enclosing body and lambda arguments for `EndInvoke` on the same symbol. Skip `Control.BeginInvoke`/`Dispatcher.BeginInvoke` (non-delegate receivers).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- On .NET Core this API always throws - reword as "do not use delegate BeginInvoke"? Recommend: yes, simpler and always correct.

## `configure-await-false`

Original message: "`ConfigureAwait(false)` should be used."  
Removed row: `Row.Invoke` on `ConfigureAwait` (SymbolEngine.cs:35).

**Detect.** In library code, `await` of a `Task`/`ValueTask` with no `.ConfigureAwait(...)` call.

Positive (finding):
```csharp
public async Task<string> Load() => await File.ReadAllTextAsync(path);
```
Negative (no finding):
```csharp
public async Task<string> Load() => await File.ReadAllTextAsync(path).ConfigureAwait(false);
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `AwaitExpression`; awaited type is `Task`/`ValueTask`(`<T>`) (not `ConfiguredTaskAwaitable`); skip when the project is an app (has `Main`/top-level statements/ASP.NET SDK) or a test file.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- How to detect "library code"? Options: OutputKind from the csproj, absence of an entry point, or always fire. Recommend: only when the compilation has no entry point and is not a test project.

## `prefer-awaitable`

Original message: "Awaitable method should be used."  
Removed row: `Row.Invoke` on `Result` (SymbolEngine.cs:143).

**Detect.** Inside an `async` method, blocking on a task: `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, or calling a sync method when an `...Async` overload exists on the same type.

Positive (finding):
```csharp
public async Task Run() { var text = client.GetStringAsync(url).Result; }
```
Negative (no finding):
```csharp
public async Task Run() { var text = await client.GetStringAsync(url); }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle`: `SimpleMemberAccessExpression` `Result` and invocations `Wait`/`GetResult` with a `Task`-typed receiver, within an `async` method/lambda. Async-overload lookup via `GetMembers(name + "Async")` as a second phase.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Include the "sync overload when Async exists" half in v1? Recommend phase 2.

## `prefer-cancellation-token`

Original message: "The overload accepting a `CancellationToken` should be used."  
Removed row: `Row.Invoke` on `None on CancellationToken` (SymbolEngine.cs:144).

**Detect.** A call that does not pass a `CancellationToken` when (a) an overload of the same method accepts one and (b) a `CancellationToken` parameter/local is in scope.

Positive (finding):
```csharp
public Task<string> Load(CancellationToken ct) => File.ReadAllTextAsync(path);
```
Negative (no finding):
```csharp
public Task<string> Load(CancellationToken ct) => File.ReadAllTextAsync(path, ct);
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `InvocationExpression` in members with a `CancellationToken` in scope; resolved method has no CT parameter (or it is defaulted and omitted); `ContainingType.GetMembers(name)` has an overload with one.

**Fixtures.** bad = positive, good = negative.

## `use-lambda-parameter`

Original message: "The lambda parameter should be used instead of capturing arguments in `ConcurrentDictionary` methods."  
Removed row: `Row.Invoke` on `GetOrAdd` (SymbolEngine.cs:194).

**Detect.** `ConcurrentDictionary.GetOrAdd/AddOrUpdate/TryUpdate` lambda that captures the outer key variable instead of using its own key parameter (allocates a closure).

Positive (finding):
```csharp
cache.GetOrAdd(key, k => Load(key));
```
Negative (no finding):
```csharp
cache.GetOrAdd(key, k => Load(k));
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over those invocations on `System.Collections.Concurrent.ConcurrentDictionary<,>`; take the key argument's symbol; report when the lambda body references that symbol.

**Fixtures.** bad = positive, good = negative.

## `validate-before-async`

Original message: "Parameter validation in `async`/`await` methods should be wrapped."  
Removed row: `Row.Invoke` on `await` (SyntaxEngine.cs:284).

**Detect.** An `async` method that throws `ArgumentException`/`ArgumentNullException`(or calls `ArgumentNullException.ThrowIfNull`) before its first `await` - the exception surfaces only when awaited. Wrap in a non-async method that validates then returns the inner task.

Positive (finding):
```csharp
public async Task Save(string path) { ArgumentNullException.ThrowIfNull(path); await File.WriteAllTextAsync(path, ""); }
```
Negative (no finding):
```csharp
public Task Save(string path) { ArgumentNullException.ThrowIfNull(path); return SaveCore(path); }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `MethodDeclaration` with `async`; statements before the first `await` contain `throw new Argument*Exception` or `ThrowIf*` calls on `Argument*Exception`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Modern guidance (and analyzers like CA2007) consider this optional; still worth having? Recommend yes, public members only.

## `validate-before-yield`

Original message: "Parameter validation in yielding methods should be wrapped."  
Removed row: `Row.Invoke` on `yield` (SyntaxEngine.cs:285).

**Detect.** An iterator method (`yield return`) that throws `Argument*Exception`/`ThrowIf*` - validation is deferred until enumeration.

Positive (finding):
```csharp
public IEnumerable<int> Take(int n) { if (n < 0) throw new ArgumentOutOfRangeException(nameof(n)); for (var i = 0; i < n; i++) yield return i; }
```
Negative (no finding):
```csharp
public IEnumerable<int> Take(int n) { ArgumentOutOfRangeException.ThrowIfNegative(n); return TakeCore(n); }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over methods containing `YieldStatement`; same argument-throw detector as `validate-before-async` (share a `Queries` helper).

**Fixtures.** bad = positive, good = negative.

