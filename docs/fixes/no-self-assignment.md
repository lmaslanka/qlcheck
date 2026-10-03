# Fix: `no-self-assignment` false positive on object-initializer member assignments

## Reported issue

From `requisitionops/api/QLCHECK_KNOWN_ISSUES.md` (23 findings, confirmed false positives):

> all instances are `Foo = Foo` inside object initializers mapping between two *different*
> types with matching property names (e.g. `new ReportCatalogItemModel { Id = Id, ... }`
> inside a record's own method). Not a bug.

## Root cause

- Row: `SyntaxEngine.cs:207` — `Row.Handle("no-self-assignment", CheckClass.Syntax, Patterns.SelfAssign)`.
- Predicate: `Patterns.SelfAssign` (`Patterns.cs:160-161`) reports every `SyntaxKind.SimpleAssignmentExpression` node satisfying `StmtFacts.SelfAssignment`.
- `StmtFacts.SelfAssignment` (`Queries/StmtFacts.cs:65-72`):

  ```csharp
  public static bool SelfAssignment(SyntaxNode node)
  {
      if (node is not AssignmentExpressionSyntax assignment)
      {
          return false;
      }

      return assignment.Left.ToString() == assignment.Right.ToString();
  }
  ```

  Pure textual equality, no structural context check. Roslyn parses an object-initializer
  member assignment (`new Foo { Id = Id }`) as an ordinary `AssignmentExpressionSyntax` of
  kind `SimpleAssignmentExpression`, nested directly inside an `InitializerExpressionSyntax`.
  There, `Id` on the left names the property being set on the *new* object; `Id` on the right
  is the enclosing scope's own property/field of the same name — two different storage
  locations that happen to share a name. The predicate can't tell the difference because it
  never looks at the assignment's parent.

## Proposed fix

Add a guard in `StmtFacts.SelfAssignment` that skips assignments whose immediate parent is an
initializer (object, `with`, or collection-of-assignments initializer all use
`InitializerExpressionSyntax`):

```csharp
public static bool SelfAssignment(SyntaxNode node)
{
    if (node is not AssignmentExpressionSyntax assignment)
    {
        return false;
    }

    if (assignment.Parent is InitializerExpressionSyntax)
    {
        return false;
    }

    return assignment.Left.ToString() == assignment.Right.ToString();
}
```

No ancestor walk needed — an initializer's member assignments are direct children of
`InitializerExpressionSyntax.Expressions`. This mirrors the existing
`InitializerExpressionSyntax`-membership checks already used elsewhere in the catalog (e.g.
`MagicLiteralPattern.IsCollectionElement`/`IsIgnoredContext`).

`StmtFacts.SelfAssignment` is only used by this one row, so the change has no ripple effect.

## Test plan

- `tests/Qlcheck.Tests/Catalog/fixtures.json["no-self-assignment"]`: current bad case
  (`value = value;` as a bare statement) is unaffected — its parent is
  `ExpressionStatementSyntax`, not an initializer.
- Add a unit test (in a new or existing `StmtFacts`-adjacent test file, or directly exercising
  `Patterns.SelfAssign` via `MatchFixtures.Context`) asserting `Empty` findings for:
  ```csharp
  class Source { public int Id; }
  class Target { public int Id { get; set; } }
  class C
  {
      Target M(Source s) => new Target { Id = s.Id };
  }
  ```
  and, to specifically pin the reported shape (same-named property on an *enclosing* type, no
  qualifier), also:
  ```csharp
  class C
  {
      public int Id { get; set; }
      object M() => new C { Id = Id };
  }
  ```
- Keep a true-positive regression case: `int x = 0; x = x;` (bare statement) must still fire.

## Confidence

Clean, low-risk, one-line-guard fix. Verified the predicate is only reachable from this single
row; no other check reuses `StmtFacts.SelfAssignment`.
