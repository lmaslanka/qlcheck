# Fix: `loop-bound-injection` treats any non-`private` method as attacker-reachable

## Reported issue

From `requisitionops/api/QLCHECK_KNOWN_ISSUES.md` (1 finding, confirmed false positive):

> `for (var index = 0; index < maxBatch; index++)` where `maxBatch` is an internal, hardcoded
> batch-size constant passed by the hosted service caller, not external/attacker input.

## Root cause

- Row: `TaintEngine.cs:20` — `Row.Handle("loop-bound-injection", CheckClass.Taint, Patterns.LoopBound)`.
- `Patterns.LoopBound` (`Patterns.cs:261`) reports every `SyntaxKind.ForStatement` satisfying
  `ForUsesParameter`.
- `ForUsesParameter` (`Patterns.cs:580-595`):
  ```csharp
  private static bool ForUsesParameter(SyntaxNode node)
  {
      if (node is not ForStatementSyntax statement || statement.Condition is null)
      {
          return false;
      }

      if (Shapes.Enclosing(statement, SyntaxKind.MethodDeclaration) is MethodDeclarationSyntax method
          && Shapes.HasModifier(method, SyntaxKind.PrivateKeyword))
      {
          return false;
      }

      foreach (var name in statement.Condition.DescendantNodes().OfType<IdentifierNameSyntax>())
      {
          if (IsDirectParameter(name))
          {
              return true;
          }
      }

      return false;
  }
  ```
  The only "is this attacker-reachable" proxy is "is the enclosing method `private`." Any
  `internal`, `protected`, or `public` method — including a hosted service's `ExecuteAsync`
  override or a plain internal helper never reachable from HTTP — is treated the same as an
  HTTP controller action.

## Proposed fix

The `private`-only exclusion is the wrong proxy for "not attacker-reachable." Two options,
in increasing order of precision:

**Option A (minimal, one-line):** widen the exclusion to `internal` as well as `private` —
`internal` members can't be called from outside the assembly either, so they're no more
attacker-reachable than `private` ones from qlcheck's syntax-only vantage point:

```csharp
if (Shapes.Enclosing(statement, SyntaxKind.MethodDeclaration) is MethodDeclarationSyntax method
    && (Shapes.HasModifier(method, SyntaxKind.PrivateKeyword) || Shapes.HasModifier(method, SyntaxKind.InternalKeyword)))
{
    return false;
}
```
This directly resolves the reported case if the hosted-service method in question is `internal`
(the report says "internal... batch-size constant," consistent with this). If the method is
actually `public` (e.g. `BackgroundService.ExecuteAsync`, which — per the `sealed-no-protected`
investigation — is `protected override` and therefore can't be `private`/`internal` anyway),
Option A alone won't clear it; Option B is needed for that shape.

**Option B (more precise, larger):** only flag methods that are actual HTTP-facing entry
points — i.e. require the enclosing type to be controller-shaped, reusing the controller
detection already used elsewhere in this file (`Patterns.BaseController`, and the
`http-verb-attribute`/`controller-route` rows in `SyntaxEngine.cs` that check for `[HttpGet]`
-style attributes or `Controller`/`ControllerBase` base types). Concretely: require
`Shapes.Enclosing(statement, SyntaxKind.ClassDeclaration)` to satisfy the same "is a
controller" predicate those rows already use, rather than checking the method's own
accessibility at all. This directly excludes `BackgroundService`/`HttpMessageHandler`-derived
classes regardless of their required `public`/`protected` override accessibility, and is a more
accurate model of "attacker-reachable" than any accessibility keyword.

Recommend landing Option A first (trivial, resolves the specific reported case), and treating
Option B as a natural follow-up once there's a second confirmed false positive that Option A
doesn't cover (a hosted service or background worker whose loop lives in a `public override`
method is a very plausible next case, given .NET's `BackgroundService.ExecuteAsync` shape).

## Test plan

- Add a regression test for the reported shape:
  ```csharp
  internal class BatchWorker
  {
      internal void Run(int maxBatch)
      {
          for (var index = 0; index < maxBatch; index++) { }
      }
  }
  ```
  must produce `Empty` findings after Option A.
- Keep the existing true-positive shape (a `public` method's loop bound sourced from a
  `public`-method `string`/`int` parameter, per the current fixture) firing unchanged.
- If Option B is pursued later, add a case for `protected override Task ExecuteAsync(...)`
  inside a class deriving from `BackgroundService`, asserting `Empty` even though the method
  is not `private`/`internal`.

## Confidence

Option A: trivial, near-zero-risk, directly addresses the reported case as described. Option B:
a modest, bounded refinement reusing existing controller-detection helpers already in
`Patterns.cs`/`StylePatterns.cs` — not attempted here, proposed as a follow-up.
