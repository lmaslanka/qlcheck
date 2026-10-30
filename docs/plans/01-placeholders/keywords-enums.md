# Contextual keywords, enums and nullability - revival plans

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Part of `../01-placeholder-cleanup.md`. Every check here was removed in that plan because its
`Row.Invoke`/`Row.Attribute`/`Row.Ident` placeholder was DEAD or NOISY (see `../01-placeholder-triage.md`).

Each section is a standalone plan to bring the check back for real. 12 removed checks: 12 to revive, 0 not worth reviving.

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

## `enum-not-reserved`

Original message: "Enumeration members should not be named `Reserved`."  
Removed row: `Row.Ident` on `Reserved` (SyntaxEngine.cs:55).

**Detect.** An enum member named `Reserved` (or `Reserved1`, `ReservedX`).

Positive (finding):
```csharp
enum Mode { On, Off, Reserved }
```
Negative (no finding):
```csharp
enum Mode { On, Off }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `EnumMemberDeclaration`; identifier starts with `Reserved`.

**Fixtures.** bad = positive, good = negative.

## `escape-extension-keyword`

Original message: "Identifiers should not conflict with the C# 14 `extension` contextual keyword."  
Removed row: `Row.Ident` on `extension` (SyntaxEngine.cs:56).

**Detect.** A type (class/struct/interface/enum/delegate/type parameter) or type reference named `extension` without `@` (conflicts with C# 14 extension blocks). Locals/parameters named `extension` are fine.

Positive (finding):
```csharp
class extension { }
```
Negative (no finding):
```csharp
class @extension { }  // locals: var extension = Path.GetExtension(p); stays clean
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `BaseTypeDeclaration`/`DelegateDeclaration`/`TypeParameter` identifiers and type-position `IdentifierName`s with text `extension` (unescaped: `token.Text == "extension"`).

**Fixtures.** bad = positive, good = negative.

## `escape-field-keyword`

Original message: "Identifiers should not conflict with the C# 14 `field` contextual keyword."  
Removed row: `Row.Ident` on `field` (SyntaxEngine.cs:57).

**Detect.** Inside a property accessor, an identifier `field` that refers to something other than the C# 14 backing field (a member or local named `field`), written without `@` or `this.`.

Positive (finding):
```csharp
int field; public int P { get => field; }  // means the member, not the backing field
```
Negative (no finding):
```csharp
int field; public int P { get => this.field; }  // and: void M(FieldInfo field) => field.Name stays clean
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `IdentifierName` `field` whose ancestor is an `AccessorDeclaration` of a property; skip outside accessors.

**Fixtures.** bad = positive, good = negative.

## `escape-partial-return`

Original message: "Return types named `partial` should be escaped with `@`."  
Removed row: `Row.Ident` on `partial` (SyntaxEngine.cs:58).

**Detect.** A method whose return type is a type named `partial` written without `@` (C# 14 parses `partial` there as the modifier).

Positive (finding):
```csharp
class partial { }  class C { partial M() => null; }
```
Negative (no finding):
```csharp
class C { @partial M() => null; }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `MethodDeclaration` whose `ReturnType` is `IdentifierName` with text `partial`.

**Fixtures.** bad = positive, good = negative.

## `escape-scoped`

Original message: "`scoped` should be escaped when used as an identifier or type name in parenthesized lambda parameter lists."  
Removed row: `Row.Ident` on `scoped` (SyntaxEngine.cs:59).

**Detect.** In a parenthesized lambda parameter list, a parameter type or name `scoped` written without `@`.

Positive (finding):
```csharp
var f = (scoped s) => s;  // where 'scoped' is a type
```
Negative (no finding):
```csharp
var f = (@scoped s) => s;
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `ParenthesizedLambdaExpression` parameters; `IdentifierToken` text `scoped` in type or identifier position.

**Fixtures.** bad = positive, good = negative.

## `flags-attribute-only-flags`

Original message: "Non-flags enums should not be marked with `FlagsAttribute`."  
Removed row: `Row.Invoke` on `Flags` (SyntaxEngine.cs:69).

**Detect.** A `[Flags]` enum with a member whose constant value is neither zero, a power of two, nor a combination of other members.

Positive (finding):
```csharp
[Flags] enum Perm { None = 0, Read = 1, Write = 2, Odd = 5 }  // 5 = Read|4, 4 undefined
```
Negative (no finding):
```csharp
[Flags] enum Perm { None = 0, Read = 1, Write = 2, All = Read | Write }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `EnumDeclaration` with `Flags`; `IFieldSymbol.ConstantValue` for members; check bit coverage.

**Fixtures.** bad = positive, good = negative.

## `flags-initialized`

Original message: "Flags enumerations should explicitly initialize all their members."  
Removed row: `Row.Invoke` on `Flags` (SyntaxEngine.cs:70).

**Detect.** A `[Flags]` enum member without an explicit initializer.

Positive (finding):
```csharp
[Flags] enum Perm { None = 0, Read = 1, Write }
```
Negative (no finding):
```csharp
[Flags] enum Perm { None = 0, Read = 1, Write = 2 }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `EnumMemberDeclaration` with `EqualsValue == null` in a `[Flags]` enum.

**Fixtures.** bad = positive, good = negative.

## `flags-none`

Original message: "Flags enumerations zero-value members should be named `None`."  
Removed row: `Row.Invoke` on `None` (SyntaxEngine.cs:71).

**Detect.** A `[Flags]` enum whose zero-valued member is not named `None`.

Positive (finding):
```csharp
[Flags] enum Perm { Nothing = 0, Read = 1 }
```
Negative (no finding):
```csharp
[Flags] enum Perm { None = 0, Read = 1 }
```

**Engine / scope.** Symbol.  **Size.** S.

**Matcher.** `Row.Handle` over `[Flags]` enums; member with constant value 0 and name != `None`.

**Fixtures.** bad = positive, good = negative.

## `no-bitwise-non-flags`

Original message: "Non-flags enums should not be used in bitwise operations."  
Removed row: `Row.Invoke` on `HasFlag` (SyntaxEngine.cs:110).

**Detect.** `&`, `|`, `^` (and compound forms) on operands of an enum type that lacks `[Flags]`.

Positive (finding):
```csharp
enum Color { Red, Green }  var c = Color.Red | Color.Green;
```
Negative (no finding):
```csharp
[Flags] enum Color { None = 0, Red = 1, Green = 2 }  var c = Color.Red | Color.Green;
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `BitwiseAnd/Or/ExclusiveOrExpression` and compound assignments; operand type `TypeKind.Enum` without `System.FlagsAttribute`.

**Fixtures.** bad = positive, good = negative.

## `no-is-this`

Original message: "`is` should not be used with `this`."  
Removed row: `Row.Invoke` on `ReferenceEquals` (SyntaxEngine.cs:153).

**Detect.** `this is T` type tests inside a class (use virtual dispatch instead).

Positive (finding):
```csharp
class Shape { public double Area() => this is Circle c ? c.R * c.R : 0; }
```
Negative (no finding):
```csharp
class Shape { public virtual double Area() => 0; }
```

**Engine / scope.** Syntax.  **Size.** S.

**Matcher.** `Row.Handle` over `IsExpression`/`IsPatternExpression` whose left operand is `ThisExpression`.

**Fixtures.** bad = positive, good = negative.

## `null-forgiving-needs-nullable`

Original message: "Null-forgiving operators should not be used when nullable warnings are disabled."  
Removed row: `Row.Invoke` on `value` (SyntaxEngine.cs:203).

**Detect.** A null-forgiving `!` operator in a location where the nullable annotation context is disabled.

Positive (finding):
```csharp
#nullable disable
var n = name!.Length;
```
Negative (no finding):
```csharp
#nullable enable
var n = name!.Length;
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `SuppressNullableWarningExpression`; `model.GetNullableContext(position)` lacks `AnnotationsEnabled`/`WarningsEnabled`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Fixture must control the context via `#nullable`; qlcheck's ad-hoc compilation uses the default (disabled) - confirm project `Nullable` is honored for csproj runs.

## `redundant-null-forgiving`

Original message: "Null-forgiving operators should not be redundant."  
Removed row: `Row.Invoke` on `value` (SyntaxEngine.cs:241).

**Detect.** A `!` applied to an expression whose flow state is already not-null (literals, `new`, non-nullable locals after assignment).

Positive (finding):
```csharp
#nullable enable
string s = "x"; var n = s!.Length;
```
Negative (no finding):
```csharp
#nullable enable
string? s = Find(); var n = s!.Length;
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `SuppressNullableWarningExpression` in enabled contexts; `model.GetTypeInfo(operand).Nullability.FlowState == NotNull`.

**Fixtures.** bad = positive, good = negative.

