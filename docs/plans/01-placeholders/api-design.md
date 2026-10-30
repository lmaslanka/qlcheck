# API design: members, parameters, naming - revival plans

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Part of `../01-placeholder-cleanup.md`. Every check here was removed in that plan because its
`Row.Invoke`/`Row.Attribute`/`Row.Ident` placeholder was DEAD or NOISY (see `../01-placeholder-triage.md`).

Each section is a standalone plan to bring the check back for real. 27 removed checks: 25 to revive, 1 not worth reviving, 1 revived by another plan.

`identical-method` is not here: it stays in the catalog and the duplication plan (`../04-duplication.md`) reimplements it.

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
| `concise-declaration` | Unclear - e.g. `var x = new T()` vs `T x = new()`. | The message ("as concise as possible") does not define a decidable condition; existing style rows already cover the concrete cases. |

## `validate-public-arguments` - revived by plan 02 (nullable)

Removed here (the `Row.Invoke` placeholder on `ArgumentNullException` was DEAD). Its real implementation overlaps `null-dereference`, so the nullable plan (`../02-nullable.md`) owns its revival. What it should detect: A public method of a public type that dereferences a reference-type parameter (member access, indexer, `foreach`) with no prior null check, in a `#nullable disable` context.

## `accessor-expected-field`

Original message: "Getters and setters should access the expected fields."  
Removed row: `Row.Invoke` on `get` (SyntaxEngine.cs:26).

**Detect.** A property `Foo` whose getter returns, or setter assigns, a field whose name matches a *different* property of the type (`_bar`) while a field matching `Foo` (`_foo`/`foo`) exists.

Positive (finding):
```csharp
int _width, _height; public int Width { get => _height; set => _width = value; }
```
Negative (no finding):
```csharp
int _width, _height; public int Width { get => _width; set => _width = value; }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `PropertyDeclaration` with simple accessors; normalize names (strip `_`/`m_`, case-insensitive); report when the accessed field's normalized name differs from the property's and a matching field exists.

**Fixtures.** bad = positive, good = negative.

## `argument-exception-name`

Original message: "The name passed to `ArgumentException` must be a real parameter."  
Removed row: `Row.Invoke` on `ArgumentException` (SyntaxEngine.cs:27).

**Detect.** `new ArgumentException(...)`/`ArgumentNullException`/`ArgumentOutOfRangeException` whose `paramName` argument is a string literal that is not a parameter of the enclosing method (or is a parameter - then suggest `nameof`).

Positive (finding):
```csharp
void Pay(int amount) { throw new ArgumentOutOfRangeException("amout"); }
```
Negative (no finding):
```csharp
void Pay(int amount) { throw new ArgumentOutOfRangeException(nameof(amount)); }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `ObjectCreationExpression` of those types (symbol); find the `paramName` argument (by `IParameterSymbol.Name == "paramName"`); literal not among the enclosing method/lambda/property-setter (`value`) parameter names.

**Fixtures.** bad = positive, good = negative.

## `argument-order`

Original message: "Arguments should be passed in the same order as the method parameters."  
Removed row: `Row.Invoke` on `M` (SyntaxEngine.cs:28).

**Detect.** Call arguments that are plain identifiers named exactly like *other* parameters of the target method (e.g. `Move(y, x)` for `Move(int x, int y)`).

Positive (finding):
```csharp
void Move(int x, int y) { }  Move(y, x);
```
Negative (no finding):
```csharp
Move(x, y);
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `InvocationExpression`; resolve method; for positional arguments that are `IdentifierName`s, report when the name equals a different parameter's name (case-insensitive) and the types are compatible.

**Fixtures.** bad = positive, good = negative.

## `auto-property`

Original message: "Trivial properties should be auto-implemented."  
Removed row: `Row.Invoke` on `get` (SyntaxEngine.cs:29).

**Detect.** A property whose accessors only `return _f;` and `_f = value;` for one private field not used elsewhere with `ref`/`out`.

Positive (finding):
```csharp
private int _age; public int Age { get { return _age; } set { _age = value; } }
```
Negative (no finding):
```csharp
public int Age { get; set; }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `PropertyDeclaration`; getter is a single return of an `IdentifierName`, setter a single assignment of `value` to the same name; field is private in the type. (A Symbol pass excludes fields passed by `ref`.)

**Fixtures.** bad = positive, good = negative.

## `field-used-as-local`

Original message: "Private fields only used as local variables in methods should become local variables."  
Removed row: `Row.Invoke` on `field` (SymbolEngine.cs:61).

**Detect.** A private instance field that, in every method using it, is written before being read (so its value never crosses method boundaries).

Positive (finding):
```csharp
class C { int _tmp; int M(int a) { _tmp = a * 2; return _tmp; } }
```
Negative (no finding):
```csharp
class C { int M(int a) { var tmp = a * 2; return tmp; } }
```

**Engine / scope.** Flow.  **Size.** L.

**Matcher.** Collect all references of a private field in the type; for each containing method run `model.AnalyzeDataFlow` on the body to confirm the field is assigned before first read on all paths and not captured.

**Fixtures.** bad = positive, good = negative.

## `group-overloads`

Original message: "Method overloads should be grouped together."  
Removed row: `Row.Invoke` on `M` (SyntaxEngine.cs:76).

**Detect.** Methods (or constructors) with the same name in a type that are not adjacent - another member sits between them.

Positive (finding):
```csharp
class C { void A() { } void B() { } void A(int x) { } }
```
Negative (no finding):
```csharp
class C { void A() { } void A(int x) { } void B() { } }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `TypeDeclaration`; walk `Members` in order; report a method whose name appeared earlier but not in the immediately preceding member.

**Fixtures.** bad = positive, good = negative.

## `implement-partial`

Original message: "Implementations should be provided for `partial` methods."  
Removed row: `Row.Invoke` on `partial` (SyntaxEngine.cs:86).

**Detect.** A `partial void` method declaration without an implementing part anywhere in the compilation (calls to it are silently removed).

Positive (finding):
```csharp
partial class C { partial void OnLoaded(); }
```
Negative (no finding):
```csharp
partial class C { partial void OnLoaded(); partial void OnLoaded() { Log(); } }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `MethodDeclaration` with `partial` and no body; `IMethodSymbol.PartialImplementationPart is null`. Needs the full compilation (other files) - already provided to the walk.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Source generators often supply the implementation; generated trees are not in qlcheck's compilation, so this would false-positive. Recommend: skip when the type has any `[Generator]`-related attribute or is in a project with analyzers - or accept the risk?

## `no-case-only-field-difference`

Original message: "Child class fields should not differ from parent class fields only by capitalization."  
Removed row: `Row.Invoke` on `value` (SyntaxEngine.cs:111).

**Detect.** A field whose name differs from a field of a base class (accessible to it) only by case.

Positive (finding):
```csharp
class B { protected int count; }  class D : B { int Count; }
```
Negative (no finding):
```csharp
class D : B { int total; }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `FieldDeclaration`; walk `BaseType` chain members; compare `OrdinalIgnoreCase` equal but `Ordinal` different.

**Fixtures.** bad = positive, good = negative.

## `no-default-argument`

Original message: "Default parameter values should not be passed as arguments."  
Removed row: `Row.Invoke` on `M` (SyntaxEngine.cs:120).

**Detect.** An argument whose constant value equals the corresponding optional parameter's default.

Positive (finding):
```csharp
void Fetch(int retries = 3) { }  Fetch(3);
```
Negative (no finding):
```csharp
Fetch();
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over arguments; mapped `IParameterSymbol.HasExplicitDefaultValue` and `model.GetConstantValue(arg)` equals `ExplicitDefaultValue`; skip when a later argument is positional (cannot omit).

**Fixtures.** bad = positive, good = negative.

## `no-ignored-initial-value`

Original message: "Method parameters, caught exceptions and foreach variables' initial values should not be ignored."  
Removed row: `Row.Invoke` on `value` (SyntaxEngine.cs:148).

**Detect.** A parameter, `catch` variable or `foreach` variable whose incoming value is overwritten before it is ever read.

Positive (finding):
```csharp
void Save(string path) { path = "/tmp/x"; Write(path); }
```
Negative (no finding):
```csharp
void Save(string path) { Write(path); }
```

**Engine / scope.** Flow.  **Size.** L.

**Matcher.** Per method: `model.AnalyzeDataFlow` / CFG to check whether the first access on every path is a write; parameters only (not `out`/`ref`).

**Fixtures.** bad = positive, good = negative.

## `no-literal-localized-arg`

Original message: "Literals should not be passed as localized parameters."  
Removed row: `Row.Invoke` on `Localized` (SyntaxEngine.cs:156).

**Detect.** A string literal passed to a parameter marked `[Localizable(true)]` (or to `Text`/`Caption`/`Message`-named parameters of UI types).

Positive (finding):
```csharp
MessageBox.Show("Saved!");  // parameter is [Localizable(true)]
```
Negative (no finding):
```csharp
MessageBox.Show(Resources.Saved);
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over arguments that are string literals; mapped `IParameterSymbol.GetAttributes()` contains `System.ComponentModel.LocalizableAttribute(true)`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Most projects are not localized; would this be noise? Recommend: revive with attribute-only detection (no name heuristic).

## `no-optional-parameter`

Original message: "Optional parameters should not be used."  
Removed row: `Row.Invoke` on `optional` (SyntaxEngine.cs:168).

**Detect.** A parameter with a default value on a public/protected member of a public type (changing the default is a binary-breaking change for callers).

Positive (finding):
```csharp
public void Connect(int timeout = 30) { }
```
Negative (no finding):
```csharp
public void Connect() => Connect(30);  public void Connect(int timeout) { }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `Parameter` with `Default`; containing method/ctor is public/protected; skip `CancellationToken cancellationToken = default` and caller-info parameters.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Optional parameters are idiomatic in modern C#; is this rule wanted at all? Recommend: revive but only for public members of public types in library projects.

## `no-overlapping-overload`

Original message: "Method overloads with default parameter values should not overlap."  
Removed row: `Row.Invoke` on `M` (SyntaxEngine.cs:170).

**Detect.** Two overloads where calling one with its optional parameters omitted is ambiguous with or shadowed by another (`M(int a, int b = 0)` vs `M(int a)`).

Positive (finding):
```csharp
void Save(string p) { }  void Save(string p, bool force = false) { }
```
Negative (no finding):
```csharp
void Save(string p, bool force = false) { }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `TypeDeclaration`; group methods by name; for each with optional params, compare its required-parameter prefix types with another overload's full parameter types.

**Fixtures.** bad = positive, good = negative.

## `no-shadow`

Original message: "Local variables should not shadow class fields or properties."  
Removed row: `Row.Invoke` on `value` (SyntaxEngine.cs:188).

**Detect.** A local variable or lambda/local-function parameter with the same name as a field or property of the containing type (not a constructor parameter assigning to it).

Positive (finding):
```csharp
class C { int count; void M() { int count = 1; Use(count); } }
```
Negative (no finding):
```csharp
class C { int count; void M() { int local = 1; Use(local); } }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `VariableDeclarator` in locals and `foreach`; name lookup in `ContainingType.GetMembers(name)` for fields/properties (instance or static).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Primary-constructor parameters and `record` positional parameters overlap with properties by design - exempt them.

## `no-shadow-outer-static`

Original message: "Inner class members should not shadow outer class `static` or type members."  
Removed row: `Row.Invoke` on `value` (SyntaxEngine.cs:189).

**Detect.** A member of a nested type with the same name as a static member (or nested type) of an enclosing type.

Positive (finding):
```csharp
class Outer { static int Limit; class Inner { int Limit; } }
```
Negative (no finding):
```csharp
class Outer { static int Limit; class Inner { int InnerLimit; } }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over members of nested types; `ContainingType.ContainingType` chain `GetMembers(name)` with `IsStatic` or type members.

**Fixtures.** bad = positive, good = negative.

## `no-write-only-property`

Original message: "Write-only properties should not be used."  
Removed row: `Row.Invoke` on `set` (SyntaxEngine.cs:202).

**Detect.** A property (or indexer) with a `set`/`init` accessor and no `get`.

Positive (finding):
```csharp
public string Password { set { _hash = Hash(value); } }
```
Negative (no finding):
```csharp
public void SetPassword(string value) => _hash = Hash(value);
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `PropertyDeclaration`/`IndexerDeclaration`; `AccessorList` has set/init and no get.

**Fixtures.** bad = positive, good = negative.

## `omit-redundant-name`

Original message: "Redundant property names should be omitted in anonymous classes."  
Removed row: `Row.Invoke` on `value` (SyntaxEngine.cs:204).

**Detect.** An anonymous-object member whose explicit name equals the inferred name: `new { Name = x.Name }`, `new { id = id }`.

Positive (finding):
```csharp
var dto = new { Name = user.Name, Id = user.Id };
```
Negative (no finding):
```csharp
var dto = new { user.Name, user.Id };
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `AnonymousObjectMemberDeclarator` with `NameEquals`; expression is `IdentifierName` or member access whose last name equals the declared name (ordinal).

**Fixtures.** bad = positive, good = negative.

## `parameter-base-type`

Original message: "Method parameters should be declared with base types."  
Removed row: `Row.Invoke` on `Stream` (SymbolEngine.cs:137).

**Detect.** A parameter declared as a derived type (`FileStream`, `List<T>`) when the method only uses members available on a base type/interface (`Stream`, `IEnumerable<T>`).

Positive (finding):
```csharp
int Count(List<int> items) { var n = 0; foreach (var i in items) n++; return n; }
```
Negative (no finding):
```csharp
int Count(IEnumerable<int> items) { ... }
```

**Engine / scope.** Symbol.  **Size.** L.

**Matcher.** Collect every use of the parameter in the body; resolve accessed members; find the most general base/interface declaring all of them. Non-trivial: must also consider arguments passed on and overload resolution.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Large analysis for modest value; consider dropping or limiting to `FileStream`/`MemoryStream` -> `Stream`. Recommend the limited version.

## `parameter-name-matches-base`

Original message: "Parameter names should match base declaration and other partial definitions."  
Removed row: `Row.Invoke` on `value` (SyntaxEngine.cs:210).

**Detect.** An override, interface implementation or second partial declaration whose parameter names differ from the base/interface/defining declaration.

Positive (finding):
```csharp
class B { public virtual void Run(int count) { } }  class D : B { public override void Run(int n) { } }
```
Negative (no finding):
```csharp
class D : B { public override void Run(int count) { } }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `MethodDeclaration`; compare `IMethodSymbol.Parameters[i].Name` with `OverriddenMethod` / `FindImplementationForInterfaceMember` counterpart / `PartialDefinitionPart`.

**Fixtures.** bad = positive, good = negative.

## `parameter-name-not-method`

Original message: "Parameter names should not duplicate the names of their methods."  
Removed row: `Row.Invoke` on `M` (SyntaxEngine.cs:211).

**Detect.** A parameter named the same as its method (case-insensitive).

Positive (finding):
```csharp
void Delay(int delay) { }
```
Negative (no finding):
```csharp
void Delay(int milliseconds) { }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `MethodDeclaration`; compare identifier with each `Parameter` identifier.

**Fixtures.** bad = positive, good = negative.

## `prefer-params`

Original message: "`params` should be used instead of `varargs`."  
Removed row: `Row.Invoke` on `ArgIterator` (SyntaxEngine.cs:227).

**Detect.** A method using `__arglist` (varargs) instead of `params`.

Positive (finding):
```csharp
void Log(string fmt, __arglist) { }
```
Negative (no finding):
```csharp
void Log(string fmt, params object[] args) { }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `Parameter` with `ArgListKeyword`, plus `ArgIterator` creations.

**Fixtures.** bad = positive, good = negative.

## `type-param-in-signature`

Original message: "All type parameters should be used in the parameter list to enable type inference."  
Removed row: `Row.Invoke` on `M` (SyntaxEngine.cs:274).

**Detect.** A generic method type parameter that does not appear in any parameter type, so callers must always spell it out (`Create<T>()` aside, see question).

Positive (finding):
```csharp
T Read<T>(string key, int id) { ... }
```
Negative (no finding):
```csharp
T Read<T>(string key, T fallback) { ... }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `MethodDeclaration` with `TypeParameterList`; search parameter type syntax for each type parameter name.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Factory methods like `Create<T>()` and `GetService<T>()` are deliberate; exempt methods with zero parameters? Recommend yes.

## `unassigned-member`

Original message: "Unassigned members should be removed."  
Removed row: `Row.Invoke` on `value` (SyntaxEngine.cs:275).

**Detect.** A private field or private auto-property that is never assigned anywhere in the type (only read), so it is always default.

Positive (finding):
```csharp
class C { private string _name; public string Name => _name; }
```
Negative (no finding):
```csharp
class C { private readonly string _name = "x"; public string Name => _name; }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over private fields/auto-properties; search the type's syntax for writes (`assignment left`, `ref`/`out` arguments, initializers, ctor assignments) resolving to the symbol. Partial types need all parts (`DeclaringSyntaxReferences`).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Fields set via reflection/serializers ([JsonInclude], [DataMember]) - exempt attributed fields.

## `unused-type-parameter`

Original message: "Unused type parameters should be removed."  
Removed row: `Row.Invoke` on `T` (SyntaxEngine.cs:277).

**Detect.** A type parameter of a method or non-public type that is never referenced in the signature or body.

Positive (finding):
```csharp
void Log<T>(string message) { Console.WriteLine(message); }
```
Negative (no finding):
```csharp
void Log(string message) { Console.WriteLine(message); }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over methods/types with type parameters; no `IdentifierName` with that name in the declaration (excluding the list itself).

**Fixtures.** bad = positive, good = negative.

## `use-value-keyword`

Original message: "`value` contextual keyword should be used."  
Removed row: `Row.Invoke` on `value` (SyntaxEngine.cs:281).

**Detect.** A property/indexer/event `set`/`add`/`remove` accessor body that never references `value` (and does not just throw).

Positive (finding):
```csharp
public int Size { get => _size; set => _size = 10; }
```
Negative (no finding):
```csharp
public int Size { get => _size; set => _size = value; }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over setter accessors; no `IdentifierName` `value` descendant; skip bodies that are only `throw`.

**Fixtures.** bad = positive, good = negative.

