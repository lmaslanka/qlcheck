# ASP.NET Core, Blazor and EF Core - revival plans

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Part of `../01-placeholder-cleanup.md`. Every check here was removed in that plan because its
`Row.Invoke`/`Row.Attribute`/`Row.Ident` placeholder was DEAD or NOISY (see `../01-placeholder-triage.md`).

Each section is a standalone plan to bring the check back for real. 16 removed checks: 11 to revive, 5 not worth reviving.

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
| `durable-entity-interface` | Entity interface methods with > 1 parameter or non-`void`/`Task` returns. | Niche Azure Durable Entities rule; restrictions are enforced at runtime with clear errors. |
| `merge-include` | `Include(a => a.B).ThenInclude(b => b.C)` on reference navigations that could be `Include(a => a.B.C)`. | Needs EF model metadata to tell reference from collection navigations; a syntax-level guess would be wrong either way. |
| `no-lambda-in-blazor-loop` | Lambda event handlers inside `@for`/`@foreach` in Razor markup. | Razor markup (`.razor`) is not parsed by qlcheck's C# Language. |
| `route-constraint-type` | Component `[Parameter]` type that does not match the route constraint type. | Blazor `@page "/x/{id:int}"` routes live in `.razor` files, which qlcheck does not scan. |
| `set-locale` | `new DataTable()`/`new DataSet()` without setting `Locale`. | Targets `DataTable`/`DataSet` locale, a legacy ADO.NET concern. |

## `client-evaluated-default`

Original message: "Client-evaluated default values should use database functions."  
Removed row: `Row.Invoke` on `HasDefaultValue` (SymbolEngine.cs:29).

**Detect.** EF Core `HasDefaultValue(expr)` whose argument is not a compile-time constant (e.g. `DateTime.Now`, `Guid.NewGuid()`), so the value is computed once at model build; `HasDefaultValueSql` was meant.

Positive (finding):
```csharp
b.Property(e => e.CreatedAt).HasDefaultValue(DateTime.UtcNow);
```
Negative (no finding):
```csharp
b.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `InvocationExpression` named `HasDefaultValue`; method containing type in `Microsoft.EntityFrameworkCore` (or name fallback when unresolved); argument has no `model.GetConstantValue` and is not a `new` of a value type.

**Fixtures.** bad = positive, good = negative. Stub `PropertyBuilder<T>.HasDefaultValue(object)` in the fixture.

## `controller-route`

Original message: "A Route attribute should be added to the controller when a route template is specified at the action level."  
Removed row: `Row.Invoke` on `Route` (SyntaxEngine.cs:41).

**Detect.** A controller (`[ApiController]` or deriving `ControllerBase`) with no class-level `[Route]` while one or more actions carry a route template (`[HttpGet("x")]`, `[Route("x")]`).

Positive (finding):
```csharp
public class OrdersController : ControllerBase { [HttpGet("orders/{id}")] public IActionResult Get(int id) => Ok(); }
```
Negative (no finding):
```csharp
[Route("orders")] public class OrdersController : ControllerBase { [HttpGet("{id}")] public IActionResult Get(int id) => Ok(); }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `ClassDeclaration` whose name ends in `Controller` or has `ApiController`/a `ControllerBase` base; inspect method attributes for a string-literal template.

**Fixtures.** bad = positive, good = negative. Stub `ControllerBase`, `HttpGetAttribute`, `RouteAttribute` in the fixture.

## `default-value-type`

Original message: "Default values should be compatible with their entity property type."  
Removed row: `Row.Invoke` on `HasDefaultValue` (SymbolEngine.cs:45).

**Detect.** EF Core `Property(e => e.X).HasDefaultValue(value)` where the constant's type is not convertible to `X`'s type.

Positive (finding):
```csharp
b.Property(e => e.Count).HasDefaultValue("0");  // Count is int
```
Negative (no finding):
```csharp
b.Property(e => e.Count).HasDefaultValue(0);
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `HasDefaultValue` invocations; walk the receiver chain to the `Property(lambda)` call; compare the lambda body's type with the argument's type via `model.ClassifyConversion`.

**Fixtures.** bad = positive, good = negative.

## `dynamic-sql`

Original message: "SQL queries should not be dynamically formatted."  
Removed row: `Row.Invoke` on `FromSqlRaw` (SymbolEngine.cs:54).

**Detect.** `FromSqlRaw`/`ExecuteSqlRaw(Async)`/`SqlQueryRaw` whose SQL argument is an interpolated string, a `+` concatenation, or `string.Format` - use `FromSql`/`FromSqlInterpolated`/parameters. Constant SQL is fine.

Positive (finding):
```csharp
ctx.Users.FromSqlRaw($"SELECT * FROM Users WHERE Name = '{name}'");
```
Negative (no finding):
```csharp
ctx.Users.FromSql($"SELECT * FROM Users WHERE Name = {name}");
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over invocations with those names; first SQL argument is `InterpolatedStringExpression`, `AddExpression` with a non-constant operand, or `string.Format`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Overlaps `sql-injection` (taint) when the data comes from a parameter; keep both? Recommend yes - this one is about the API choice and fires without taint.

## `http-verb-attribute`

Original message: "REST API actions should be annotated with an HTTP verb attribute."  
Removed row: `Row.Invoke` on `HttpGet` (SyntaxEngine.cs:80).

**Detect.** A public non-static action method on an `[ApiController]` class with no `Http*`/`AcceptVerbs` attribute and no `[NonAction]`.

Positive (finding):
```csharp
[ApiController] public class Orders : ControllerBase { public IActionResult List() => Ok(); }
```
Negative (no finding):
```csharp
[ApiController] public class Orders : ControllerBase { [HttpGet] public IActionResult List() => Ok(); }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `MethodDeclaration` in such a type; `public`, not `override`, not `static`, not a constructor; attribute names checked (inherited attributes via symbol).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Only `[ApiController]` classes, or every `Controller` subclass (MVC views default to GET)? Recommend `[ApiController]` only.

## `migration-narrow-column`

Original message: "Migrations should not narrow column types without converting existing data."  
Removed row: `Row.Invoke` on `AlterColumn` (SymbolEngine.cs:82).

**Detect.** In an EF migration (`Migration` subclass `Up`), `AlterColumn` where `maxLength`/`precision` is lower than `oldMaxLength`/`oldPrecision`, or the type narrows (`nvarchar(max)` -> `nvarchar(n)`, `bigint` -> `int`) with no preceding `Sql(...)` data conversion.

Positive (finding):
```csharp
migrationBuilder.AlterColumn<string>(name: "Code", table: "T", maxLength: 10, oldMaxLength: 50);
```
Negative (no finding):
```csharp
migrationBuilder.AlterColumn<string>(name: "Code", table: "T", maxLength: 100, oldMaxLength: 50);
```

**Engine / scope.** Syntax.  **Size.** M.

**Matcher.** `Row.Handle` over `AlterColumn` invocations inside a class deriving `Migration`; compare named constant arguments.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Treat any narrowing as a finding even with a prior `Sql("UPDATE ...")`? Recommend: exempt when a `Sql` call precedes it in the same `Up`.

## `model-binding`

Original message: "Use model binding instead of reading raw request data."  
Removed row: `Row.Invoke` on `Form` (SyntaxEngine.cs:98).

**Detect.** In a controller action, reading `Request.Form`, `Request.Query`, `Request.Headers[...]` (except well-known ones), `Request.Body` or `Request.RouteValues` directly instead of binding parameters.

Positive (finding):
```csharp
public IActionResult Save() { var name = Request.Form["name"]; return Ok(name); }
```
Negative (no finding):
```csharp
public IActionResult Save([FromForm] string name) => Ok(name);
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `SimpleMemberAccessExpression` `Request.<Form|Query|Body|RouteValues>` inside a method of a controller class (name/base heuristic as in `controller-route`).

**Fixtures.** bad = positive, good = negative.

## `no-backslash-route`

Original message: "Backslash should be avoided in route templates."  
Removed row: `Row.Invoke` on `HttpGet` (SyntaxEngine.cs:109).

**Detect.** A route template literal (in `Route`, `Http*`, `Map*` endpoint calls) containing `\\`.

Positive (finding):
```csharp
[HttpGet("orders\\{id}")]
```
Negative (no finding):
```csharp
[HttpGet("orders/{id}")]
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over attribute arguments of those attributes and first string argument of `MapGet/MapPost/...`; literal contains a backslash.

**Fixtures.** bad = positive, good = negative.

## `no-rooted-action-route`

Original message: "ASP.NET controller actions should not have a route template starting with `/`."  
Removed row: `Row.Invoke` on `HttpGet` (SyntaxEngine.cs:186).

**Detect.** An action-level route template starting with `/` (or `~/`) inside a controller that has a class-level `[Route]` (it silently ignores the prefix).

Positive (finding):
```csharp
[Route("api/orders")] class OrdersController : ControllerBase { [HttpGet("/all")] public IActionResult All() => Ok(); }
```
Negative (no finding):
```csharp
[Route("api/orders")] class OrdersController : ControllerBase { [HttpGet("all")] public IActionResult All() => Ok(); }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `MethodDeclaration` attributes in a class with `[Route]`; template literal `StartsWith('/')`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Is a rooted template sometimes deliberate (override the prefix)? Yes - recommend message wording that asks to confirm intent.

## `required-value-input`

Original message: "Value type property used as input in a controller action should be nullable, required or annotated with the JsonRequiredAttribute to avoid under-posting."  
Removed row: `Row.Attribute` on `Required` (SyntaxEngine.cs:246).

**Detect.** A value-type property (non-nullable `int`, `bool`, `DateTime`, enum, ...) on a type used as a controller action parameter that is neither nullable, nor `required`, nor `[JsonRequired]`/`[BindRequired]` - a missing field silently becomes `default`. `[Required]` alone does not help for value types.

Positive (finding):
```csharp
public record Order(int Quantity);  // used as: public IActionResult Post(Order o)
class Order { [Required] public int Quantity { get; set; } }
```
Negative (no finding):
```csharp
class Order { [Required] public int? Quantity { get; set; } }  // or: public required int Quantity { get; init; }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** Two-step: collect action parameter types in controllers in the file/compilation, then for each property of those types check `IPropertySymbol.Type.IsValueType`, nullability, `IsRequired`, attributes. Needs cross-file lookup -> `CompilationEngine` row.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Report only when the type is used as a controller input in the same compilation (recommended), or every DTO-like type?

## `sql-keyword-whitespace`

Original message: "SQL keywords should be delimited by whitespace."  
Removed row: `Row.Invoke` on `FromSqlRaw` (SyntaxEngine.cs:259).

**Detect.** Concatenated SQL where adjacent string pieces join without whitespace across a keyword boundary (`"SELECT *" + "FROM t"` -> `*FROM`).

Positive (finding):
```csharp
var sql = "SELECT id" + "FROM users" + "WHERE id = @id";
```
Negative (no finding):
```csharp
var sql = "SELECT id " + "FROM users " + "WHERE id = @id";
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `AddExpression` chains of string literals (and interpolation pieces); when the left piece ends in a non-space and the right begins with a SQL keyword (reuse `SqlText.cs`) report it.

**Fixtures.** bad = positive, good = negative.

