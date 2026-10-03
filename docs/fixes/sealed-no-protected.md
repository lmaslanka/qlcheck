# Fix: `sealed-no-protected` false positive on required `override` accessibility

## Reported issue

From `requisitionops/api/QLCHECK_KNOWN_ISSUES.md` (4 findings, confirmed false positives):

> all four are `protected override` methods overriding a framework-declared `protected`
> member (`BackgroundService.ExecuteAsync`, `HttpMessageHandler.SendAsync`). C# requires
> override accessibility to match the base member exactly — this can't be changed to
> `private` without a compile error.

## Root cause

- Row: `SyntaxEngine.cs:276` — `Row.Handle("sealed-no-protected", CheckClass.Syntax, Patterns.SealedProtected)`.
- Predicate: `Patterns.SealedProtected` (`Patterns.cs:302-303`) reports every
  `SyntaxKind.MethodDeclaration` satisfying `ProtectedOnSealed`.
- `ProtectedOnSealed` (`Patterns.cs:750-759`):

  ```csharp
  private static bool ProtectedOnSealed(SyntaxNode node)
  {
      if (!Shapes.HasModifier(node, SyntaxKind.ProtectedKeyword))
      {
          return false;
      }

      var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
      return type is not null && Shapes.HasModifier(type, SyntaxKind.SealedKeyword);
  }
  ```

  It checks only "is this method `protected`" and "is the enclosing class `sealed`." It never
  checks for the `override` modifier. A `protected override` method in a `sealed` class is
  flagged identically to a genuinely-unnecessary `protected` method — but for an override, the
  accessibility isn't a free choice: C# requires it to match the base member's declared
  accessibility exactly. The rule's own remedy (narrow to `private`) is a compile error for
  every override case.

## Proposed fix

Skip methods that are overrides — their accessibility is fixed by the base member, not an
authoring choice this rule can act on:

```csharp
private static bool ProtectedOnSealed(SyntaxNode node)
{
    if (!Shapes.HasModifier(node, SyntaxKind.ProtectedKeyword)
        || Shapes.HasModifier(node, SyntaxKind.OverrideKeyword))
    {
        return false;
    }

    var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
    return type is not null && Shapes.HasModifier(type, SyntaxKind.SealedKeyword);
}
```

`Shapes.HasModifier` is already used by this same predicate, so no new helper is needed.
`ProtectedOnSealed` is only used by this one row.

## Test plan

- `tests/Qlcheck.Tests/Catalog/fixtures.json["sealed-no-protected"]`: current bad case
  (`protected void M()`, no `override`) is unaffected; good case (`private void M()`) is
  unaffected.
- Add a regression test asserting `Empty` findings for:
  ```csharp
  abstract class Base { protected abstract void M(); }
  sealed class C : Base
  {
      protected override void M() { }
  }
  ```
- Keep the true-positive case: a plain `protected void M()` with no base override, in a
  `sealed class`, must still fire.

## Confidence

Clean, trivial, single added condition. `ProtectedOnSealed` has no other callers.
