# Plan 08: public API rows judge visibility like the compiler

Status: Agreed 2026-11-02 · Depends on: none

## Goal

Three rows exist to protect callers in *other* assemblies, but they decide what is public by looking only at the
`public` keyword on the member itself. A `public` property of a `private` nested class, or of an `internal` class,
is reported as public API although no other assembly can reach it. The self-scan shows it:

- `no-list-in-public-api` on `Cli.cs:130/132/134` (`ParserState` is a `private` nested class),
  `CompilationContext.cs:49` and `WalkContext.cs:45` (both `internal sealed`).
- `no-public-const` on `MatchFixtures.cs:7` (`internal static class`).

The rows should judge reachability as the compiler does: a member counts as public API when it and every type that
contains it are `public`, `protected` or `protected internal`.

Acceptance:
- `internal class C { public List<int> M() => new(); }` and a `public` member of a `private` nested class give no
  `no-list-in-public-api` Finding; `public class C { public List<int> M() => new(); }` still gives one.
- `public interface I { List<int> M(); }` and `public class C { protected List<int> M() => new(); }` each give one
  (missed today).
- `internal static class K { public const int N = 1; ... }` gives no `no-public-const`; `public class C { protected
  const int N = 1; }` gives one.
- `internal class Outer { public class FooException : Exception { } }` gives `exception-public` (missed today).
- The self-scan loses exactly the six Findings listed above and gains none.

## Current state

- **The three rows** (all `CheckClass.Syntax`, registered in `SyntaxEngine.cs`):
  - `no-list-in-public-api`: `Patterns.ListInPublicApi` (`Patterns.cs:247`). Methods whose return or parameter type is
    `List`, and properties of type `List`, with the `public` keyword. Interface members (no modifier) and `protected`
    members are never reported.
  - `no-public-const`: `StylePatterns.PublicConst` → `PublicAndConst` (`StylePatterns.cs:254`). A `public const`
    field, except constants needed elsewhere (`ConstantUsage.IsRequiredElsewhere`) and constants-only static classes.
  - `exception-public`: `Patterns.ExceptionPublic` (`Patterns.cs:170`). A type named `*Exception` without the
    `public` keyword. A `public` exception nested in a non-public type is missed.
- **The helper exists:** `Symbols.IsVisibleOutsideAssembly(ISymbol)` (`Queries/Symbols.cs:93`) walks the member and
  its containing types, accepting `Public`, `Protected` and `ProtectedOrInternal`. `validate-public-arguments` and
  the taint model (plan 03, D1 revision) already use it.
- **Syntax rows may use the model:** `WalkContext.Model` is always set, and syntax rows already read it
  (`ConstantUsage`, `StylePatterns.cs:517`). Declared accessibility is read from declarations, so compile errors in
  the file do not change the answer.
- **Tests that assume the keyword rule:** the `fixtures.json` bad cases for `no-list-in-public-api` and
  `no-public-const` use a top-level `class C` (internal), as do the `PublicConst_*` tests in `StylePatternsTests.cs`.
  The `exception-public` fixture is listed in `Baseline/FixturesCompile.txt` (it lacks `using System;`).

## Design

### 1. Visibility of a declaration (`Queries/Symbols.cs`)

Add `Symbols.IsVisibleOutsideAssembly(WalkContext ctx, SyntaxNode declaration)`: resolve the declared symbol (for a
`FieldDeclarationSyntax`, its first variable; all variables share the modifiers) and pass it to the existing
`IsVisibleOutsideAssembly(ISymbol)`. No symbol gives false, so the rows stay silent rather than guess.

This follows the compiler's defaults without extra code: top-level types are internal, nested types and class
members private, interface members public, and a partial type's accessibility comes from whichever part declares it.

### 2. The rows

The shapes each row matches do not change; only the visibility test does.

- **`no-list-in-public-api`:** match the shapes (method return or parameter of type `List`, property of type `List`)
  first, then report when `IsVisibleOutsideAssembly`. The `public`-keyword prefilter goes; interface and `protected`
  members are now reported.
- **`no-public-const`:** a `const` field that is visible outside the assembly, with both exemptions kept. `protected`
  constants of visible types are now reported: they are compiled into derived classes in other assemblies just the same.
- **`exception-public`:** a type named `*Exception` that is *not* visible outside the assembly. Today's Findings stay
  (an internal or private exception is not visible); a `public` exception nested in a non-public type is added.

Messages, ids, classes and default-on state are unchanged. No catalog count changes.

### 3. Not changed

- **Field rows** (`no-public-field`, `no-public-static-field`, `no-public-static-mutable`), by D1: their concern
  (encapsulation, mutable global state) applies inside an assembly too.
- **The SSRF rows' public-parameter rule** (`SsrfPatterns.IsTaintedParameterReference`), by D1.
- **Keyword rows whose subject is the keyword itself:** `abstract-no-public-constructor`, `sealed-no-protected`,
  `redundant-modifier`, `no-redundant-constructor`, `utility-private-constructor`, `member-not-more-visible`,
  `check-model-state`. Each is about which modifier is written, not about who can call the member.
- **`InternalsVisibleTo`** (D3): internal members stay internal even when a friend assembly can see them.

## Implementation steps (test-first, one commit each)

1. **Visibility of a declaration.**
   - Red, `tests/Qlcheck.Tests/Catalog/Queries/SymbolsTests.cs`: a `public` member of a `public` type is visible; of an
     `internal` type or a `private` nested type it is not; a `protected` member of a `public` unsealed type is
     visible; `private protected` is not; an interface member with no modifier in a `public` interface is visible; a
     field declaration with two variables resolves; `public` written on another part of a partial type counts.
   - Green: the overload.
2. **`no-list-in-public-api`.**
   - Red, `PatternsTests.cs`: the acceptance cases above (internal type, private nested type, public type,
     interface member, protected member, parameter of type `List`).
   - Green: shapes first, then visibility. Change the `fixtures.json` bad case to `public class C`.
3. **`no-public-const`.**
   - Red, `StylePatternsTests.cs`: an internal type gives nothing; a `protected const` in a public type gives one.
     Change the existing `PublicConst_*` cases to `public` types, so the exemption tests still test the exemption.
   - Green: visibility replaces the keyword. Change the `fixtures.json` bad case to `public class Holder`.
4. **`exception-public`.**
   - Red, `PatternsTests.cs`: a `public` exception nested in an internal type gives one; one nested in a `public`
     type gives nothing; the existing internal and public cases keep their result.
   - Green: visibility replaces the keyword. Add `using System;` to both fixtures and delete `exception-public` from
     `Baseline/FixturesCompile.txt` (shrink-only).
5. **Self-scan.** Run `./run .`: expect 454 → 448, losing the five `no-list-in-public-api` Findings and the
   `MatchFixtures.cs` `no-public-const` one. `Baseline.cs:26` (a `public const` in a `public` test class) stays, as do
   both `no-public-field` Findings. Record the count in this plan's Progress section and mark it done.

## Files touched

- Changed: `src/Qlcheck/Languages/CSharp/Catalog/Queries/Symbols.cs`, `Patterns.cs`, `StylePatterns.cs`
- Tests: `tests/Qlcheck.Tests/Catalog/Queries/SymbolsTests.cs`, `PatternsTests.cs`, `StylePatternsTests.cs`,
  `fixtures.json`, `Baseline/FixturesCompile.txt`
- Docs: this plan, `docs/plans/README.md`

## ADR needed?

No. It is a precision fix inside three rows, using a helper the catalog already has; nothing is hard to reverse.

## Risks

- **New Findings from the widening.** `protected` and interface members are now reported. qlcheck itself has none, but
  libraries with protected `List` members will see new Findings. Intended: they are reachable API.
- **`protected` in a sealed type** counts as visible (it is unreachable, but `sealed-no-protected` already reports it).
  Accepted rather than adding a special case.
- **Executables.** Members of an `Exe` project are reachable only by reflection, but are still counted. Out of scope,
  as today.

## Effort

About half a day.

## Decisions (resolved)

- **D1. Rows:** only the three API rows (`no-list-in-public-api`, `no-public-const`, `exception-public`). Field rows
  and the SSRF parameter rule keep the keyword test.
- **D2. Visibility is the compiler's:** the member and every containing type are `public`, `protected` or
  `protected internal`, read from the semantic model (`Symbols.IsVisibleOutsideAssembly`), not from keywords.
- **D3. `InternalsVisibleTo` does not count:** internal members are not public API, even with friend assemblies.

## Noticed in passing (not in this plan)

- `seal-attribute` ("Non-abstract attributes should be sealed") is registered with `StylePatterns.RedundantPublic`,
  the handler for `public` on interface members, so it reports the wrong thing. It needs its own fix.
