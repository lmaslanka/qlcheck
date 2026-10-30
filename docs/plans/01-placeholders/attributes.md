# Attribute-shape checks - revival plans

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Part of `../01-placeholder-cleanup.md`. Every check here was removed in that plan because its
`Row.Invoke`/`Row.Attribute`/`Row.Ident` placeholder was DEAD or NOISY (see `../01-placeholder-triage.md`).

Each section is a standalone plan to bring the check back for real. 33 removed checks: 21 to revive, 12 not worth reviving.

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
| `assembly-version` | Compilation with no `[assembly: AssemblyVersion]`. | SDK-style projects generate `AssemblyVersion` from MSBuild; the source-level absence is the normal state. |
| `cls-compliant` | Compilation with no `[assembly: CLSCompliant]`. | CLS compliance is irrelevant for nearly all modern .NET code and would fire once on every project. |
| `com-visible-explicit` | Compilation with no `[assembly: ComVisible]`. | The SDK already emits `ComVisible(false)`; absence in source is not a defect. |
| `constructor-argument-exists` | `[ConstructorArgument("name")]` naming a constructor parameter that does not exist. | XAML-only `[ConstructorArgument]`; no target users in a CLI-for-agents context. |
| `iserializable-correct` | `ISerializable` implementation missing the serialization ctor, `[Serializable]` or a base `GetObjectData` call. | `ISerializable` is the BinaryFormatter-era pattern (removed in .NET 9). |
| `neutral-resources-language` | Assembly with resources but no `[assembly: NeutralResourcesLanguage]`. | Needs `.resx` discovery outside the C# file scope; low value. |
| `no-conflicting-transparency` | Members with both `[SecurityCritical]` and `[SecuritySafeCritical]`. | Code Access Security transparency attributes have no effect on .NET Core. |
| `one-way-operation-void` | `[OperationContract(IsOneWay = true)]` on a non-void method. | WCF server is not part of modern .NET. |
| `optional-field-deserialize` | Type with `[OptionalField]` members but no `[OnDeserializing]`/`[OnDeserialized]` method. | `[OptionalField]` versioning only matters for BinaryFormatter/SoapFormatter, which are removed in .NET 9. |
| `remove-obsolete` | Any member marked `[Obsolete]`. | Fires on every `[Obsolete]`, i.e. on deliberate deprecation, and directly contradicts `obsolete-needs-reason` on the same line; an agent cannot act on "remove" without knowing callers are gone. |
| `service-and-operation-contract` | `[ServiceContract]` interface without `[OperationContract]` members, or the reverse. | WCF server is not part of modern .NET. |
| `validate-on-deserialize` | Serializable type whose deserialization ctor/callback performs no validation. | Message ("validate data") is not decidable statically and targets legacy formatter callbacks. |

## `attribute-usage`

Original message: "Custom attributes should be marked with `System.AttributeUsageAttribute`."  
Removed row: `Row.Invoke` on `AttributeUsage` (SymbolEngine.cs:21).

**Detect.** A non-abstract class deriving (directly or indirectly) from `System.Attribute` with no `[AttributeUsage]`.

Positive (finding):
```csharp
class TagAttribute : Attribute { }
```
Negative (no finding):
```csharp
[AttributeUsage(AttributeTargets.Class)]
class TagAttribute : Attribute { }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `ClassDeclaration`; `INamedTypeSymbol` inherits `System.Attribute`; no `AttributeUsageAttribute` in `GetAttributes()` (inherited usage from a source base counts as present).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Should an abstract attribute base be exempt? (recommend yes)

## `blazor-query-param-type`

Original message: "This Blazor query parameter type is not supported. Change it."  
Removed row: `Row.Invoke` on `SupplyParameterFromQuery` (SymbolEngine.cs:25).

**Detect.** A property with `[SupplyParameterFromQuery]` whose type is not one Blazor binds: `bool, DateTime, DateTimeOffset, decimal, double, float, Guid, int, long, string`, their nullable forms, or arrays of them.

Positive (finding):
```csharp
[SupplyParameterFromQuery] public Customer Filter { get; set; }
```
Negative (no finding):
```csharp
[SupplyParameterFromQuery] public int? Page { get; set; }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `PropertyDeclaration` with the attribute (match by symbol name `Microsoft.AspNetCore.Components.SupplyParameterFromQueryAttribute`); check `IPropertySymbol.Type` against the allowed set.

**Fixtures.** bad = positive, good = negative. Fixture declares a stub `namespace Microsoft.AspNetCore.Components { class SupplyParameterFromQueryAttribute : System.Attribute { } }`.

## `caller-info-last`

Original message: "Caller information parameters should come at the end of the parameter list."  
Removed row: `Row.Invoke` on `CallerMemberName` (SymbolEngine.cs:26).

**Detect.** A parameter with `[CallerMemberName]`/`[CallerFilePath]`/`[CallerLineNumber]`/`[CallerArgumentExpression]` followed by a parameter that has none of those attributes (and is not `params`).

Positive (finding):
```csharp
void Log([CallerMemberName] string caller = "", string message = "") { }
```
Negative (no finding):
```csharp
void Log(string message, [CallerMemberName] string caller = "") { }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `ParameterList`; attribute names via `Names.Attribute`; report the first caller-info parameter that is not in the trailing run.

**Fixtures.** bad = positive, good = negative.

## `coverage-exclusion-reason`

Original message: "`ExcludeFromCodeCoverage` attributes should include a justification."  
Removed row: `Row.Invoke` on `ExcludeFromCodeCoverage` (SymbolEngine.cs:41).

**Detect.** `[ExcludeFromCodeCoverage]` with no `Justification = "..."` named argument (or an empty one).

Positive (finding):
```csharp
[ExcludeFromCodeCoverage] class Generated { }
```
Negative (no finding):
```csharp
[ExcludeFromCodeCoverage(Justification = "generated")] class Generated { }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `Attribute` where `Names.Attribute == "ExcludeFromCodeCoverage"` and no `AttributeArgument` with `NameEquals.Name == Justification` holding a non-empty literal.

**Fixtures.** bad = positive, good = negative.

## `debugger-display-member`

Original message: "`DebuggerDisplayAttribute` strings should reference existing members."  
Removed row: `Row.Invoke` on `DebuggerDisplay` (SymbolEngine.cs:43).

**Detect.** A `[DebuggerDisplay("... {Expr} ...")]` whose `{...}` placeholder names a member that does not exist on the type.

Positive (finding):
```csharp
[DebuggerDisplay("{Nmae}")] class P { public string Name { get; set; } }
```
Negative (no finding):
```csharp
[DebuggerDisplay("{Name}")] class P { public string Name { get; set; } }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `Attribute`; parse the literal's `{...}` segments; take the leading identifier (strip `,nq`, method calls, member chains); look it up with `INamedTypeSymbol.GetMembers` including bases.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- How far to parse expressions (`{Items.Count}`, `{ToString(),nq}`)? Recommend: only the first identifier segment; skip anything with operators.

## `default-parameter-value`

Original message: "``DefaultValue`` should not be used when ``DefaultParameterValue`` is meant."  
Removed row: `Row.Invoke` on `DefaultValue` (SymbolEngine.cs:44).

**Detect.** A parameter carrying `[DefaultValue(x)]` (System.ComponentModel) and no `[DefaultParameterValue]`: the author meant an optional-parameter default, which `DefaultValue` does not provide.

Positive (finding):
```csharp
void Run([DefaultValue(5)] int retries) { }
```
Negative (no finding):
```csharp
void Run([Optional, DefaultParameterValue(5)] int retries) { }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `Parameter`; attribute set contains `DefaultValue` and not `DefaultParameterValue`.

**Fixtures.** bad = positive, good = negative.

## `js-invokable-public`

Original message: "`JSInvokable` attribute should only be used on public methods."  
Removed row: `Row.Invoke` on `JSInvokable` (SymbolEngine.cs:68).

**Detect.** A method with `[JSInvokable]` whose declared accessibility is not `public`.

Positive (finding):
```csharp
[JSInvokable] private static int Add(int a, int b) => a + b;
```
Negative (no finding):
```csharp
[JSInvokable] public static int Add(int a, int b) => a + b;
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `MethodDeclaration`; `Names.Attribute == "JSInvokable"`; no `public` modifier (explicit interface impls excluded).

**Fixtures.** bad = positive, good = negative. Fixture declares a stub `JSInvokableAttribute`.

## `no-datetime-primary-key`

Original message: "Date and time should not be used as a type for primary keys."  
Removed row: `Row.Invoke` on `Key` (SymbolEngine.cs:94).

**Detect.** An entity key property of type `DateTime`/`DateTimeOffset`: marked `[Key]`, or named `Id`/`<Type>Id` by EF convention.

Positive (finding):
```csharp
class Reading { [Key] public DateTime TakenAt { get; set; } }
```
Negative (no finding):
```csharp
class Reading { [Key] public int Id { get; set; } public DateTime TakenAt { get; set; } }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `PropertyDeclaration`; `[Key]` (symbol) or convention name; `IPropertySymbol.Type` is `System.DateTime`/`System.DateTimeOffset` (unwrap nullable).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Should the `Id` naming convention count without `[Key]`? It may flag non-EF types; recommend only when the type is referenced from a `DbSet<T>` in the same file, else `[Key]` only.

## `no-expected-exception`

Original message: "``ExpectedException`` should not be used."  
Removed row: `Row.Invoke` on `ExpectedException` (SyntaxEngine.cs:135).

**Detect.** Any `[ExpectedException]` attribute (MSTest/NUnit 2); use `Assert.Throws`.

Positive (finding):
```csharp
[TestMethod, ExpectedException(typeof(ArgumentException))] public void Throws() { }
```
Negative (no finding):
```csharp
[TestMethod] public void Throws() => Assert.ThrowsException<ArgumentException>(() => Run());
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Attribute("no-expected-exception", ..., "ExpectedException")` is the whole check once retargeted from `Row.Invoke`.

**Fixtures.** bad = positive, good = negative. Stub `ExpectedExceptionAttribute`/`TestMethodAttribute` in the fixture.

## `no-explicit-caller-info`

Original message: "Caller information arguments should not be provided explicitly."  
Removed row: `Row.Invoke` on `CallerMemberName` (SymbolEngine.cs:99).

**Detect.** A call that passes an explicit argument to a parameter marked `[CallerMemberName]`/`[CallerFilePath]`/`[CallerLineNumber]`.

Positive (finding):
```csharp
Log("msg", "Main");  // caller param supplied
```
Negative (no finding):
```csharp
Log("msg");
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `InvocationExpression`; resolve `IMethodSymbol`; map each argument to its `IParameterSymbol` (positional and named); report when that parameter has a caller-info attribute. Exempt forwarding of the current method's own caller-info parameter.

**Fixtures.** bad = positive, good = negative.

## `no-inline-suppression`

Original message: "Do not suppress a finding in source."  
Removed row: `Row.Invoke` on `SuppressMessage` (SyntaxEngine.cs:151).

**Detect.** `[SuppressMessage]`/`[assembly: SuppressMessage]` attributes and `#pragma warning disable` directives. (Dated `// qlcheck-ignore:` comments are allowed - ADR 0008.)

Positive (finding):
```csharp
#pragma warning disable CS0168
[SuppressMessage("Style", "IDE0001")] void M() { }
```
Negative (no finding):
```csharp
void M() { }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle`: `Attribute` nodes named `SuppressMessage`/`UnconditionalSuppressMessage` plus `PragmaWarningDirectiveTrivia` with `DisableKeyword` (structured trivia via `DescendantTrivia`).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Include `#pragma warning disable`? It is the more common form; recommend yes.
- ADR 0008 names this check - keep its wording in sync.

## `no-optional-ref`

Original message: "``Optional`` should not be used on `ref` or `out` parameters."  
Removed row: `Row.Invoke` on `Optional` (SymbolEngine.cs:108).

**Detect.** `[Optional]` on a `ref` or `out` parameter.

Positive (finding):
```csharp
void Read([Optional] out int value) { value = 0; }
```
Negative (no finding):
```csharp
void Read([Optional] int value) { }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `Parameter`; has `Optional` attribute and `ref`/`out` modifier.

**Fixtures.** bad = positive, good = negative.

## `no-thread-static-initializer`

Original message: "`ThreadStatic` fields should not be initialized."  
Removed row: `Row.Invoke` on `ThreadStatic` (SymbolEngine.cs:118).

**Detect.** A `[ThreadStatic]` field declaration with an initializer (it runs once, on the first thread only).

Positive (finding):
```csharp
[ThreadStatic] static int counter = 1;
```
Negative (no finding):
```csharp
[ThreadStatic] static int counter;
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `FieldDeclaration`; `ThreadStatic` attribute and any `VariableDeclarator.Initializer`.

**Fixtures.** bad = positive, good = negative.

## `obsolete-needs-reason`

Original message: "`Obsolete` attributes should include explanations."  
Removed row: `Row.Invoke` on `Obsolete` (SymbolEngine.cs:131).

**Detect.** `[Obsolete]` with no arguments, or an empty/whitespace message literal.

Positive (finding):
```csharp
[Obsolete] public void Old() { }
```
Negative (no finding):
```csharp
[Obsolete("Use New instead.")] public void Old() { }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `Attribute` with `Names.Attribute == "Obsolete"`; `ArgumentList` null/empty or first argument is an empty string literal.

**Fixtures.** bad = positive, good = negative.

## `optional-attribute-pair`

Original message: "Parameters with ``DefaultParameterValue`` attributes should also be marked ``Optional``."  
Removed row: `Row.Invoke` on `DefaultParameterValue` (SymbolEngine.cs:133).

**Detect.** A parameter with `[DefaultParameterValue]` but no `[Optional]` (callers must still pass it).

Positive (finding):
```csharp
void Run([DefaultParameterValue(5)] int retries) { }
```
Negative (no finding):
```csharp
void Run([Optional, DefaultParameterValue(5)] int retries) { }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `Parameter`; attribute set contains `DefaultParameterValue` and not `Optional`.

**Fixtures.** bad = positive, good = negative.

## `pure-returns-value`

Original message: "Methods with `Pure` attribute should return a value."  
Removed row: `Row.Invoke` on `Pure` (SymbolEngine.cs:148).

**Detect.** A `void`-returning method marked `[Pure]` (System.Diagnostics.Contracts).

Positive (finding):
```csharp
[Pure] void Validate() { }
```
Negative (no finding):
```csharp
[Pure] bool IsValid() => true;
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `MethodDeclaration`; `Pure` attribute and `ReturnType` is `void` predefined type.

**Fixtures.** bad = positive, good = negative.

## `serialization-callback`

Original message: "Serialization event handlers should be implemented correctly."  
Removed row: `Row.Attribute` on `OnDeserialized` (SymbolEngine.cs:162).

**Detect.** A method marked `[OnSerializing]`/`[OnSerialized]`/`[OnDeserializing]`/`[OnDeserialized]` that is not `void`, does not take exactly one `StreamingContext` parameter, is generic, or is not `private`.

Positive (finding):
```csharp
[OnDeserialized] public void Done() { }
```
Negative (no finding):
```csharp
[OnDeserialized] private void Done(StreamingContext context) { }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `MethodDeclaration` with one of the four attributes; check return type, parameter count and `System.Runtime.Serialization.StreamingContext` type via the model.

**Fixtures.** bad = positive, good = negative.

## `single-key-attribute`

Original message: "Multiple ``Key`` attributes should not be used to define a composite key."  
Removed row: `Row.Invoke` on `Key` (SymbolEngine.cs:166).

**Detect.** More than one property in one type marked `[Key]` (EF Core composite keys must use `HasKey` / `[PrimaryKey]`).

Positive (finding):
```csharp
class Line { [Key] public int OrderId { get; set; } [Key] public int No { get; set; } }
```
Negative (no finding):
```csharp
[PrimaryKey(nameof(OrderId), nameof(No))]
class Line { public int OrderId { get; set; } public int No { get; set; } }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `TypeDeclaration`; count `PropertyDeclaration`s with `Key` attribute; report the second and later.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Match `Key` only from `System.ComponentModel.DataAnnotations` (needs Symbol) or by name? Recommend symbol match to skip unrelated `KeyAttribute`s.

## `sta-thread`

Original message: "Windows Forms entry points should be marked with STAThread."  
Removed row: `Row.Invoke` on `STAThread` (SymbolEngine.cs:167).

**Detect.** A `static Main` in a project that uses Windows Forms (`Application.Run(...)` in the body or `System.Windows.Forms` referenced) without `[STAThread]`.

Positive (finding):
```csharp
static void Main() { Application.Run(new MainForm()); }
```
Negative (no finding):
```csharp
[STAThread] static void Main() { Application.Run(new MainForm()); }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `MethodDeclaration` named `Main`; body contains an invocation resolving to `System.Windows.Forms.Application.Run`; no `STAThread` attribute.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- WinForms types are not in the platform TPA set on Linux; fixture needs a stub `System.Windows.Forms.Application` - acceptable?

## `thread-static-on-static`

Original message: "`ThreadStatic` should not be used on non-static fields."  
Removed row: `Row.Invoke` on `ThreadStatic` (SymbolEngine.cs:177).

**Detect.** `[ThreadStatic]` on a field without the `static` modifier (the attribute is ignored).

Positive (finding):
```csharp
[ThreadStatic] int counter;
```
Negative (no finding):
```csharp
[ThreadStatic] static int counter;
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `FieldDeclaration`; `ThreadStatic` attribute and no `static`/`const` modifier.

**Fixtures.** bad = positive, good = negative.

## `wrap-native-method`

Original message: "Native methods should be wrapped."  
Removed row: `Row.Invoke` on `DllImport` (SymbolEngine.cs:199).

**Detect.** A non-private `extern` method with `[DllImport]` or `[LibraryImport]` (expose a managed wrapper instead).

Positive (finding):
```csharp
[DllImport("user32.dll")] public static extern int MessageBox(IntPtr h, string t, string c, uint type);
```
Negative (no finding):
```csharp
[DllImport("user32.dll")] private static extern int MessageBox(IntPtr h, string t, string c, uint type);
public static int Show(string t) => MessageBox(IntPtr.Zero, t, "", 0);
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `MethodDeclaration`; `extern` (or `partial` with `LibraryImport`) modifier, the attribute, and an accessibility other than `private`.

**Fixtures.** bad = positive, good = negative.

