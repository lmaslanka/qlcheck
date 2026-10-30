# Types, equality and events - revival plans

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Part of `../01-placeholder-cleanup.md`. Every check here was removed in that plan because its
`Row.Invoke`/`Row.Attribute`/`Row.Ident` placeholder was DEAD or NOISY (see `../01-placeholder-triage.md`).

Each section is a standalone plan to bring the check back for real. 14 removed checks: 12 to revive, 2 not worth reviving.

Exception to the intro: `no-suppress-finalize` worked as written, but it was removed by decision D5 because its advice contradicts CA1816.

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
| `no-recursive-inheritance` | Recursive generic base-type expansion. | The compiler already rejects circular inheritance; the generic-expansion variant (`class C<T> : B<C<C<T>>>`) is too rare to justify a check (see docs/fixes/no-fix-yet.md). |
| `no-suppress-finalize` | Any `GC.SuppressFinalize(...)` call. | Advice (never call `GC.SuppressFinalize`) contradicts the standard Dispose pattern and CA1816; the legitimate concern is covered by `suppress-finalize-needs-destructor`. |

## `comparable-equals`

Original message: "`Equals` and the comparison operators should be overridden when implementing `IComparable`."  
Removed row: `Row.Invoke` on `CompareTo` (SymbolEngine.cs:32).

**Detect.** A type implementing `IComparable`/`IComparable<T>` that does not override `Equals(object)` or does not define `==`, `!=`, `<`, `>`.

Positive (finding):
```csharp
public class Money : IComparable<Money> { public int CompareTo(Money o) => 0; }
```
Negative (no finding):
```csharp
public class Money : IComparable<Money>, IEquatable<Money> { /* Equals, GetHashCode, ==, !=, <, > */ }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `TypeDeclaration`; `AllInterfaces` has `IComparable(<T>)`; members lack `Equals(object)` override or the operator methods.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Require all six operators, or only `Equals` + `==`/`!=`? Recommend `Equals` + `==`/`!=`; comparison operators optional.

## `equatable-sealed`

Original message: "Classes implementing `IEquatable<T>` should be sealed."  
Removed row: `Row.Invoke` on `IEquatable` (SymbolEngine.cs:56).

**Detect.** A non-sealed, non-abstract class whose base list includes `IEquatable<T>` (subclasses break `Equals` symmetry). Records are exempt.

Positive (finding):
```csharp
public class Point : IEquatable<Point> { public bool Equals(Point o) => true; }
```
Negative (no finding):
```csharp
public sealed class Point : IEquatable<Point> { public bool Equals(Point o) => true; }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `ClassDeclaration` without `sealed`/`abstract`/`static`; `BaseList` contains a `GenericName` `IEquatable`.

**Fixtures.** bad = positive, good = negative.

## `equatable-value-type`

Original message: "Value types should implement `IEquatable<T>`."  
Removed row: `Row.Invoke` on `IEquatable` (SymbolEngine.cs:57).

**Detect.** A `struct` that overrides `Equals(object)` (or is used as a key) but does not implement `IEquatable<Self>`.

Positive (finding):
```csharp
struct Id { public int V; public override bool Equals(object o) => o is Id i && i.V == V; public override int GetHashCode() => V; }
```
Negative (no finding):
```csharp
struct Id : IEquatable<Id> { public int V; public bool Equals(Id o) => o.V == V; ... }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `StructDeclaration` (not `record struct`); overrides `Equals(object)` and lacks `IEquatable<Self>`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Fire on every struct without `IEquatable`, or only those overriding `Equals`? Recommend only those overriding `Equals` (CA1066 semantics).

## `event-args`

Original message: "Events should have proper arguments."  
Removed row: `Row.Invoke` on `EventArgs` (SymbolEngine.cs:58).

**Detect.** An event whose delegate type's second parameter is not `EventArgs` or derived (or whose signature is not `(object, TArgs)`).

Positive (finding):
```csharp
public event Action<string> Changed;
```
Negative (no finding):
```csharp
public event EventHandler<ChangedEventArgs> Changed;
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `EventFieldDeclaration`/`EventDeclaration`; delegate `Invoke` signature check.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- `Action<T>` events are common and harmless in app code; restrict to public events on public types? Recommend yes.

## `event-handler-signature`

Original message: "Event Handlers should have the correct signature."  
Removed row: `Row.Invoke` on `EventHandler` (SymbolEngine.cs:59).

**Detect.** A method subscribed with `+=` to an event, or a custom delegate declared for events, whose signature is not `void (object sender, TEventArgs e)`.

Positive (finding):
```csharp
public delegate void ChangedHandler(int value);  public event ChangedHandler Changed;
```
Negative (no finding):
```csharp
public event EventHandler<ChangedEventArgs> Changed;
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `DelegateDeclaration`s used as event types in the file: return `void`, 2 params, first `object`, second `EventArgs`-derived, names `sender`/`e`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Overlaps `event-args` heavily; merge them? Recommend merge into one check with the clearer message.

## `generic-event-handler`

Original message: "Generic event handlers should be used."  
Removed row: `Row.Invoke` on `EventHandler` (SymbolEngine.cs:63).

**Detect.** A custom delegate type `void X(object sender, TArgs e)` declared and used for events where `EventHandler<TArgs>` would do.

Positive (finding):
```csharp
public delegate void OrderEventHandler(object sender, OrderEventArgs e);
```
Negative (no finding):
```csharp
public event EventHandler<OrderEventArgs> OrderPlaced;
```

**Engine / scope.** Syntax.  **Size.** M.

**Matcher.** `Row.Handle` over `DelegateDeclaration` with that exact shape whose name is referenced by an `event` in the same file.

**Fixtures.** bad = positive, good = negative.

## `hashcode-immutable`

Original message: "`GetHashCode` should not reference mutable fields."  
Removed row: `Row.Invoke` on `GetHashCode` (SymbolEngine.cs:64).

**Detect.** A `GetHashCode()` override that reads a non-`readonly` field or an auto-property with a non-`init` setter of the same type.

Positive (finding):
```csharp
class Key { public int Id; public override int GetHashCode() => Id; }
```
Negative (no finding):
```csharp
class Key { public readonly int Id; public override int GetHashCode() => Id; }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `MethodDeclaration` named `GetHashCode` with `override`; collect `IdentifierName`s in the body resolving to `IFieldSymbol` (not readonly/const) or `IPropertySymbol` with a `set` (not init) on the containing type.

**Fixtures.** bad = positive, good = negative.

## `no-anonymous-unsubscribe`

Original message: "Anonymous delegates should not be used to unsubscribe from Events."  
Removed row: `Row.Invoke` on `Unsubscribe` (SymbolEngine.cs:84).

**Detect.** `event -= lambda` or `event -= delegate { ... }` - a new delegate instance, so nothing is removed.

Positive (finding):
```csharp
button.Click -= (s, e) => Save();
```
Negative (no finding):
```csharp
button.Click -= OnClick;
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `SubtractAssignmentExpression` whose right side is `ParenthesizedLambda`/`SimpleLambda`/`AnonymousMethodExpression`.

**Fixtures.** bad = positive, good = negative.

## `no-delegate-subtraction`

Original message: "Delegates should not be subtracted."  
Removed row: `Row.Invoke` on `Remove` (SyntaxEngine.cs:122).

**Detect.** Binary `-`/`-=` with delegate-typed operands on a non-event target (removes only a matching tail sequence).

Positive (finding):
```csharp
Action all = a + b + c; all -= a + b;
```
Negative (no finding):
```csharp
Action all = a + b + c; all -= a;  // or keep a list of handlers
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `SubtractExpression`/`SubtractAssignmentExpression`; `model.GetTypeInfo` of the left is `TypeKind.Delegate`; left is not an event symbol; report when the right side is itself a combination (`a + b`).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Report every delegate subtraction, or only subtraction of a combined delegate? Recommend combined only - single-delegate removal is fine.

## `no-public-pointer`

Original message: "Pointers to unmanaged memory should not be visible."  
Removed row: `Row.Invoke` on `DangerousGetHandle` (SyntaxEngine.cs:176).

**Detect.** A non-private, non-readonly field of type `IntPtr`/`UIntPtr`/`nint`/`nuint` or a pointer type on a public type.

Positive (finding):
```csharp
public class Native { public IntPtr Handle; }
```
Negative (no finding):
```csharp
public class Native { private IntPtr _handle; }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `FieldDeclaration`; resolved type special type `System_IntPtr`/`System_UIntPtr` or `TypeKind.Pointer`; accessibility not private; not readonly.

**Fixtures.** bad = positive, good = negative.

## `suppress-finalize-needs-destructor`

Original message: "`GC.SuppressFinalize` should not be invoked for types without destructors."  
Removed row: `Row.Invoke` on `SuppressFinalize` (SymbolEngine.cs:175).

**Detect.** `GC.SuppressFinalize(this)` in a `sealed` type with no finalizer (and no finalizer in source base types). Non-sealed types are exempt because a derived type may add one (CA1816).

Positive (finding):
```csharp
sealed class Res : IDisposable { public void Dispose() { GC.SuppressFinalize(this); } }
```
Negative (no finding):
```csharp
sealed class Res : IDisposable { public void Dispose() { } }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `GC.SuppressFinalize` invocations; containing type `IsSealed`; no `DestructorDeclaration` in it or its source bases.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- `no-suppress-finalize` was removed (decision D5) because it contradicts CA1816; this check is the one that comes back for `GC.SuppressFinalize` misuse.

## `type-name-not-namespace`

Original message: "Type names should not match namespaces."  
Removed row: `Row.Invoke` on `System` (SyntaxEngine.cs:272).

**Detect.** A type declared with the same name as a well-known namespace segment (`System`, `Collections`, `Linq`, `IO`, `Text`, `Threading`, `Tasks`, `Net`, `Http`, `Data`, ...) or a namespace in the same compilation.

Positive (finding):
```csharp
public class Tasks { }
```
Negative (no finding):
```csharp
public class TaskList { }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `BaseTypeDeclaration`; identifier in a fixed list or in `compilation.GlobalNamespace` descendants' names.

**Fixtures.** bad = positive, good = negative.

