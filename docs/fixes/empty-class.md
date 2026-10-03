# Fix: `empty-class` false positive on marker exception types

## Reported issue

From `requisitionops/api/QLCHECK_KNOWN_ISSUES.md` (2 findings, one confirmed false positive):

> `ControlPlaneAdministratorAuthenticationException` is an intentionally minimal marker
> exception (used ~15 places); the other instance is `UpdateSupplierCommandTests.cs`, which
> is a real (if unusual) empty-class case.

Only the exception case needs a fix; the commented-out-tests class should keep firing.

## Root cause

- Row: `SyntaxEngine.cs:58-59` — `Row.Handle("empty-class", CheckClass.Syntax, Patterns.EmptyClass)`, where `Patterns.EmptyClass => Report(ctx, id, SyntaxKind.ClassDeclaration, IsEmptyType)`.
- `IsEmptyType` (`Patterns.cs:469`) is **shared** by three rows — `empty-class` (ClassDeclaration), and two sibling rows for `InterfaceDeclaration`/`NamespaceDeclaration` (presumably `empty-interface`/`empty-namespace`, not reported and out of scope here):
  ```csharp
  private static bool IsEmptyType(SyntaxNode node) => Shapes.MemberCount(node) == 0;
  ```
- `Shapes.MemberCount` (`Queries/Shapes.cs:52-64`) counts direct child nodes whose kind is in a fixed `MemberKinds` set that includes `ConstructorDeclaration`. So a class with zero explicit members — including zero explicit constructors, relying on the compiler-synthesized default — counts as "empty." A marker exception type (`class Foo : Exception { }`) is exactly this shape, and it's a standard, widely-used C# idiom, not a code smell.
- The other reported instance (`UpdateSupplierCommandTests.cs`) has all its `[Fact]` methods commented out; comments are trivia, not syntax nodes, so `MemberCount` correctly sees zero real members and correctly flags it — that part of the check is working as intended.

## Proposed fix

Don't touch the shared `IsEmptyType`/`Shapes.MemberCount` (used by the two other, unreported rows). Give `empty-class` its own predicate that additionally exempts classes whose base type is (or ends in) `Exception`:

```csharp
private static bool IsEmptyClass(SyntaxNode node) =>
    IsEmptyType(node) && !TypeFacts.BaseType(node).EndsWith("Exception", StringComparison.Ordinal);
```

and change the row's handler:

```csharp
public static void EmptyClass(WalkContext ctx, string id) =>
    Report(ctx, id, SyntaxKind.ClassDeclaration, IsEmptyClass);
```

`TypeFacts.BaseType(node)` (`Queries/TypeFacts.cs:10-24`) already returns the first base-list
entry's simple type text with no semantic model needed — the same syntax-only convention
`QuietPatterns.BaseIsObject` already uses (`TypeFacts.BaseType(node) == ObjectName`). It also
composes correctly for exception hierarchies more than one level deep (e.g.
`: ControlPlaneException` still ends with `"Exception"`), and returns `string.Empty` for a
class with no base list at all, so `class C { }` (no base type) is correctly unaffected —
`"".EndsWith("Exception")` is false, so it's still flagged.

`TypeFacts` is already imported in `Patterns.cs` via `using Qlcheck.Languages.CSharp.Catalog.Queries;`.

## Test plan

- `tests/Qlcheck.Tests/Catalog/fixtures.json["empty-class"]`: bad case `class C { }` (no base
  type) stays flagged; good case (already has a member) unaffected.
- Add a regression test asserting `Empty` findings for:
  ```csharp
  class MyMarkerException : Exception
  {
  }
  ```
  and, for a multi-level hierarchy:
  ```csharp
  class AppException : Exception { public AppException(string m) : base(m) { } }
  class SpecificException : AppException
  {
  }
  ```
- Keep a true-positive regression case for a genuinely empty, non-exception class:
  `class C { }` and a class whose only members are comments (mirroring the
  `UpdateSupplierCommandTests.cs` shape) must still fire.

## Confidence

Clean fix, scoped only to `empty-class`'s own predicate — deliberately does not touch
`IsEmptyType`, `empty-interface`, or `empty-namespace`, which were not reported as false
positives and weren't investigated as part of this fix.
