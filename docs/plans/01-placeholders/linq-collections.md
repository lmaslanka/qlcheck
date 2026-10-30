# LINQ and collections - revival plans

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Part of `../01-placeholder-cleanup.md`. Every check here was removed in that plan because its
`Row.Invoke`/`Row.Attribute`/`Row.Ident` placeholder was DEAD or NOISY (see `../01-placeholder-triage.md`).

Each section is a standalone plan to bring the check back for real. 19 removed checks: 18 to revive, 1 not worth reviving.

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
| `prefer-first-not-or-default` | `FirstOrDefault()` on a collection known to be non-empty. | Requires knowing a collection is non-empty, which needs whole-program reasoning; any heuristic produces bad advice. |

## `collection-size-compare`

Original message: "Collection sizes and array length comparisons should make sense."  
Removed row: `Row.Invoke` on `Count on Enumerable` (SyntaxEngine.cs:35).

**Detect.** A comparison of a size that is always true/false: `x.Count >= 0`, `x.Length < 0`, `x.Count() >= 0`, `0 > x.Count`, etc.

Positive (finding):
```csharp
if (items.Count >= 0) { Run(); }
```
Negative (no finding):
```csharp
if (items.Count > 0) { Run(); }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `GreaterThanOrEqual`/`LessThan` (and mirrored) binary expressions with a `0` literal; other side is `Count`/`Length`/`LongLength` property or `Count()`/`LongCount()` on a collection type.

**Fixtures.** bad = positive, good = negative.

## `filter-before-sort`

Original message: "The collection should be filtered before sorting by using `Where` before `OrderBy`."  
Removed row: `Row.Invoke` on `OrderBy on Enumerable` (SyntaxEngine.cs:67).

**Detect.** `OrderBy/OrderByDescending/ThenBy...` immediately followed by `.Where(...)` in an extension-method chain on `IEnumerable`/`IQueryable`.

Positive (finding):
```csharp
var r = users.OrderBy(u => u.Name).Where(u => u.Active);
```
Negative (no finding):
```csharp
var r = users.Where(u => u.Active).OrderBy(u => u.Name);
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `InvocationExpression` named `Where` whose receiver is an invocation named `OrderBy*`/`ThenBy*`; method symbol in `System.Linq.Enumerable`/`Queryable` (reduced extension).

**Fixtures.** bad = positive, good = negative.

## `generic-collection`

Original message: "Collections should implement the generic interface."  
Removed row: `Row.Invoke` on `ICollection` (SymbolEngine.cs:62).

**Detect.** A public type implementing `IEnumerable`, `ICollection` or `IList` (non-generic) without the generic counterpart.

Positive (finding):
```csharp
public class Bag : IEnumerable { public IEnumerator GetEnumerator() => null; }
```
Negative (no finding):
```csharp
public class Bag : IEnumerable<int> { ... }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `TypeDeclaration`; `INamedTypeSymbol.AllInterfaces` contains the non-generic interface but no `IEnumerable<T>`.

**Fixtures.** bad = positive, good = negative.

## `no-array-covariance`

Original message: "Array covariance should not be used."  
Removed row: `Row.Invoke` on `Copy` (SyntaxEngine.cs:105).

**Detect.** An implicit reference conversion from `Derived[]` to `Base[]` (assignment, argument, return) - writes can throw `ArrayTypeMismatchException`.

Positive (finding):
```csharp
object[] items = new string[3];
```
Negative (no finding):
```csharp
IReadOnlyList<object> items = new string[3];
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over expressions with `model.GetConversion` = implicit reference conversion where both source and destination are array types with different element types.

**Fixtures.** bad = positive, good = negative.

## `no-array-for-params`

Original message: "Arrays should not be created for params parameters."  
Removed row: `Row.Invoke` on `ToArray on Enumerable` (SyntaxEngine.cs:106).

**Detect.** An explicit array (`new T[] { ... }`, `new[] { ... }`, collection expression) passed as the sole argument to a `params` parameter.

Positive (finding):
```csharp
Log("x", new object[] { a, b });  // Log(string, params object[])
```
Negative (no finding):
```csharp
Log("x", a, b);
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `Argument`s that are array creations; `IParameterSymbol.IsParams` for the mapped parameter and the call is in normal (not expanded) form.

**Fixtures.** bad = positive, good = negative.

## `no-collection-copy-property`

Original message: "Properties should not make collection or array copies."  
Removed row: `Row.Invoke` on `ToArray on Enumerable` (SyntaxEngine.cs:114).

**Detect.** A property getter that returns a copy: `.ToArray()`, `.ToList()`, `new List<T>(field)`, `field.Clone()`.

Positive (finding):
```csharp
public int[] Items => _items.ToArray();
```
Negative (no finding):
```csharp
public IReadOnlyList<int> Items => _items;
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `PropertyDeclaration` getters (expression body or single `return`); returned expression is an invocation named `ToArray`/`ToList`/`Clone` or a collection creation from a field.

**Fixtures.** bad = positive, good = negative.

## `no-empty-collection-access`

Original message: "Empty collections should not be accessed or iterated."  
Removed row: `Row.Invoke` on `First on Enumerable` (SyntaxEngine.cs:127).

**Detect.** Reading from a collection that is provably empty at that point: `First()`/indexer/`foreach` right after `new List<T>()` with no intervening add.

Positive (finding):
```csharp
var list = new List<int>(); var first = list[0];
```
Negative (no finding):
```csharp
var list = new List<int> { 1 }; var first = list[0];
```

**Engine / scope.** Flow.  **Size.** L.

**Matcher.** Needs per-method dataflow (local assigned an empty creation, no mutation before the read on any path). Use the `FlowEngine` CFG helpers once they exist.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Worth an L-sized dataflow investment for a rare bug? Recommend park until the flow engine exposes reaching definitions.

## `no-multidimensional-array`

Original message: "Multidimensional arrays should not be used."  
Removed row: `Row.Invoke` on `GetLength` (SyntaxEngine.cs:157).

**Detect.** A rank > 1 array type (`T[,]`) in a declaration or creation.

Positive (finding):
```csharp
var grid = new int[3, 3];
```
Negative (no finding):
```csharp
var grid = new int[3][];
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `ArrayRankSpecifier` with more than one size slot (`Sizes.Count > 1`).

**Fixtures.** bad = positive, good = negative.

## `no-obsolete-base-type`

Original message: "Types should not extend outdated base types."  
Removed row: `Row.Invoke` on `ArrayList` (SymbolEngine.cs:107).

**Detect.** A class deriving from `ArrayList`, `Hashtable`, `CollectionBase`, `DictionaryBase`, `ReadOnlyCollectionBase`, `ApplicationException`.

Positive (finding):
```csharp
class Users : CollectionBase { }
```
Negative (no finding):
```csharp
class Users : Collection<User> { }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `ClassDeclaration`; `BaseType` symbol in the list.

**Fixtures.** bad = positive, good = negative.

## `no-public-multidimensional`

Original message: "Public methods should not have multidimensional array parameters."  
Removed row: `Row.Invoke` on `GetLength` (SyntaxEngine.cs:175).

**Detect.** A public/protected method parameter whose type is a rank > 1 array.

Positive (finding):
```csharp
public void Draw(int[,] cells) { }
```
Negative (no finding):
```csharp
public void Draw(int[][] cells) { }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `Parameter` in non-private methods; `ArrayType` with a rank specifier of rank > 1.

**Fixtures.** bad = positive, good = negative.

## `prefer-any`

Original message: "`Any()` should be used to test for emptiness."  
Removed row: `Row.Invoke` on `Count on Enumerable` (SyntaxEngine.cs:214).

**Detect.** Emptiness tests through `Count()`: `xs.Count() == 0`, `!= 0`, `> 0`, `>= 1`, `< 1` on `IEnumerable<T>` (not on types with a `Count`/`Length` property).

Positive (finding):
```csharp
if (orders.Count() > 0) { Ship(); }  // IEnumerable<Order>
```
Negative (no finding):
```csharp
if (orders.Any()) { Ship(); }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over binary comparisons with literal `0`/`1`; one side is an invocation of `System.Linq.Enumerable.Count` with no predicate (reduced extension or static).

**Fixtures.** bad = positive, good = negative.

## `prefer-find`

Original message: "`Find` method should be used instead of the `FirstOrDefault` extension."  
Removed row: `Row.Invoke` on `FirstOrDefault on Enumerable` (SyntaxEngine.cs:219).

**Detect.** `FirstOrDefault(predicate)` on a `List<T>` (or array) receiver where `List<T>.Find` / `Array.Find` exists.

Positive (finding):
```csharp
var u = users.FirstOrDefault(x => x.Id == id);  // List<User>
```
Negative (no finding):
```csharp
var u = users.Find(x => x.Id == id);
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `FirstOrDefault` invocations with one lambda argument; receiver type is `System.Collections.Generic.List<T>` or an array; method is `Enumerable.FirstOrDefault`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Performance-only; skip in `IQueryable`-adjacent code automatically by receiver type. Fine.

## `prefer-generic`

Original message: "Generics should be used when appropriate."  
Removed row: `Row.Invoke` on `ArrayList` (SymbolEngine.cs:145).

**Detect.** Use of the non-generic `System.Collections` types: `ArrayList`, `Hashtable`, `Queue`, `Stack`, `SortedList`, `CollectionBase` (creations, fields, parameters).

Positive (finding):
```csharp
var list = new ArrayList();
```
Negative (no finding):
```csharp
var list = new List<int>();
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `ObjectCreationExpression` and type syntax in declarations; resolved type's containing namespace is exactly `System.Collections`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Report creations only, or every type reference? Recommend creations + declared member types, not every local `var`.

## `prefer-index`

Original message: "Prefer indexing instead of `Enumerable` methods on types implementing `IList`."  
Removed row: `Row.Invoke` on `ElementAt` (SyntaxEngine.cs:222).

**Detect.** `ElementAt(i)`, `First()`, `Last()` (no predicate) on a receiver implementing `IList<T>`/`IReadOnlyList<T>` or an array.

Positive (finding):
```csharp
var last = list.Last();  // List<int>
```
Negative (no finding):
```csharp
var last = list[^1];
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over those invocations resolving to `System.Linq.Enumerable`; receiver type implements `IList<T>`/`IReadOnlyList<T>`.

**Fixtures.** bad = positive, good = negative.

## `prefer-linked-list-ends`

Original message: "`First` and `Last` properties of `LinkedList` should be used instead of the `First()` and `Last()` extension methods."  
Removed row: `Row.Invoke` on `First on LinkedList` (SyntaxEngine.cs:224).

**Detect.** `First()`/`Last()` (no predicate) on a `LinkedList<T>` receiver; use the `First`/`Last` properties.

Positive (finding):
```csharp
var head = queue.First();  // LinkedList<int>
```
Negative (no finding):
```csharp
var head = queue.First!.Value;
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over those invocations; receiver type is `System.Collections.Generic.LinkedList<T>`.

**Fixtures.** bad = positive, good = negative.

## `readonly-collection-property`

Original message: "Collection properties should be readonly."  
Removed row: `Row.Invoke` on `IList` (SyntaxEngine.cs:235).

**Detect.** A public property of a mutable collection type (`List<T>`, `T[]`, `Dictionary<,>`, `ICollection<T>`, `IList<T>`) with a public setter.

Positive (finding):
```csharp
public List<string> Tags { get; set; }
```
Negative (no finding):
```csharp
public List<string> Tags { get; } = new();
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `PropertyDeclaration`; public `set` accessor (not `init`); type implements `ICollection<T>` or is an array.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- DTOs bound by serializers need setters; exempt `init` and types with only auto-properties (POCOs)? Recommend exempt `init` only.

## `redundant-to-array`

Original message: "`string.ToCharArray()` and `ReadOnlySpan<T>.ToArray()` should not be called redundantly."  
Removed row: `Row.Invoke` on `ToCharArray` (SyntaxEngine.cs:243).

**Detect.** `s.ToCharArray()` used only to iterate or index (`foreach (var c in s.ToCharArray())`, `s.ToCharArray()[i]`), and `span.ToArray()` in the same positions.

Positive (finding):
```csharp
foreach (var c in name.ToCharArray()) { Use(c); }
```
Negative (no finding):
```csharp
foreach (var c in name) { Use(c); }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `ToCharArray()`/`ToArray()` invocations whose parent is a `ForEachStatement` expression or an `ElementAccessExpression`; receiver type `string`/`ReadOnlySpan<T>` via model.

**Fixtures.** bad = positive, good = negative.

## `single-orderby`

Original message: "Multiple `OrderBy` calls should not be used."  
Removed row: `Row.Invoke` on `OrderBy on Enumerable` (SyntaxEngine.cs:258).

**Detect.** Two `OrderBy`/`OrderByDescending` calls in one chain (the second discards the first); use `ThenBy`.

Positive (finding):
```csharp
var r = users.OrderBy(u => u.Last).OrderBy(u => u.First);
```
Negative (no finding):
```csharp
var r = users.OrderBy(u => u.Last).ThenBy(u => u.First);
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `OrderBy*` invocation whose receiver chain (through non-ordering calls like `Where`) contains another `OrderBy*`; Linq symbol check.

**Fixtures.** bad = positive, good = negative.

