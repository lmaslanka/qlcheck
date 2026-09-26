# Plan: Check architecture reframe

Resolve the thermo-nuclear review blockers. **Behavior stays.** Structure changes so empty methods, the test-only runner, the always-compile loop, and the `InlineSqlCheck` special case in the CLI all disappear.

No config file. No plugins. ADR 0002 still holds: checks are in-process types, discovered by reflection.

## Blockers this deletes

1. Dual runner (`ICheck.Run` / `CheckContext`) beside `QlcheckApp`
2. One `ICheck` shape forcing empty methods + always-compile
3. Opt-in policy hardcoded to `InlineSqlCheck` in the CLI
4. Finding location/id constructed in every check instead of on `Finding`

## Target shape

```csharp
public interface ICheck
{
    string Id { get; }
    bool EnabledByDefault => true;
}

public interface IFileCheck : ICheck
{
    IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree);
}

public interface ICompilationCheck : ICheck
{
    IReadOnlyList<Finding> AnalyzeCompilation(
        Compilation compilation,
        IReadOnlySet<string> includedFiles);
}
```

| Type | Implements | `EnabledByDefault` |
|---|---|---|
| `MagicLiteralCheck` | `IFileCheck` | true (DIM) |
| `StringConcatCheck` | `IFileCheck` | true |
| `StringEmptyCheck` | `IFileCheck` | true |
| `SwitchPatternCheck` | `IFileCheck` | true |
| `OneTypePerFileCheck` | `IFileCheck` | true |
| `InlineSqlCheck` | `IFileCheck` | **false** |
| `UnusedUsingCheck` | `ICompilationCheck` | true |

A type may implement both; none do today. `OfType<IFileCheck>()` / `OfType<ICompilationCheck>()` in the app is the split. No `NeedsCompilation` boolean.

Delete:

- `CheckContext`
- `ICheck.Run`
- `ICheck.Analyze` / `ICheck.AnalyzeCompilation` as members of the root interface
- `UnusedUsingCheck.Analyze => []`
- `UnusedUsingCheck` explicit DIM forwarding
- `QlcheckApp` references to `InlineSqlCheck`
- Private `MakeFinding` on `StringConcatCheck` and `InlineSqlCheck`

## 1. `Finding.At` is the location contract

All checks currently copy:

- `GetLineSpan().StartLinePosition`
- `line + 1`, `column + 1`
- `path.Replace('\\', '/')`
- `Id: $"{checkId}:{file}:{line}:{column}"`

Put that on `Finding`:

```csharp
public static Finding At(
    string checkId,
    string path,
    Location location,
    string message,
    string? replacement = null)
{
    var file = path.Replace('\\', '/');
    var span = location.GetLineSpan().StartLinePosition;
    var line = span.Line + 1;
    var column = span.Character + 1;
    return new Finding(
        Id: $"{checkId}:{file}:{line}:{column}",
        Check: checkId,
        File: file,
        Line: line,
        Column: column,
        Message: message,
        Replacement: replacement);
}

public static Finding At(string checkId, string path, SyntaxNode node, string message, string? replacement = null)
    => At(checkId, path, node.GetLocation(), message, replacement);

public static Finding At(string checkId, string path, SyntaxToken token, string message, string? replacement = null)
    => At(checkId, path, token.GetLocation(), message, replacement);
```

Call sites:

- Most checks: `Finding.At(CheckId, file.Path, node, Message, replacement)`
- `OneTypePerFileCheck`: `Finding.At(CheckId, path, extra.Identifier, $"Move '{extra.Identifier.Text}' into its own file.")`
- `UnusedUsingCheck`: `Finding.At(CheckId, path, diagnostic.Location, Message, string.Empty)`

`InlineSqlCheck.MakeFinding` takes a `SyntaxTree` it never uses. Drop it.

Keep the `Finding` record constructor public so tests that assert fields still work. Do not construct Findings by hand in checks.

`SourceFile` stays in `Finding.cs`. Move `ICheck` / `IFileCheck` / `ICompilationCheck` to `src/Qlcheck/ICheck.cs` so `Finding.cs` is not a junk drawer.

## 2. Discovery

`CheckDiscovery` still returns `IReadOnlyList<ICheck>`, still `Activator.CreateInstance`, still `OrderBy Id`.

Tighten the type filter so a class that implements only `ICheck` (no analyze method) is not discovered:

```csharp
t is { IsClass: true, IsAbstract: false } &&
(typeof(IFileCheck).IsAssignableFrom(t) || typeof(ICompilationCheck).IsAssignableFrom(t))
```

Interfaces themselves are not classes; no extra exclusion needed.

## 3. One runner: `QlcheckApp`

After parse + unknown-id validation:

```csharp
if (options.CheckIds.Count > 0)
{
    checks = checks.Where(c => options.CheckIds.Contains(c.Id)).ToList();
}
else
{
    checks = checks.Where(c => c.EnabledByDefault || options.EnableIds.Contains(c.Id)).ToList();
}

var fileChecks = checks.OfType<IFileCheck>().ToList();
var compilationChecks = checks.OfType<ICompilationCheck>().ToList();
```

File pass (unchanged parallelism), but only `fileChecks`:

```csharp
return fileChecks.SelectMany(c => c.Analyze(file, tree));
```

Compilation pass **only if** `compilationChecks.Count > 0`. Same csproj grouping as today. Loop `compilationChecks`, not `checks`.

`--stats` `Checks run` still counts the filtered `checks` list (both kinds). Default run stays 6 (inline-sql still out).

### `--inline-sql` sugar (behavior unchanged)

Do **not** delete the flag. Do **not** name `InlineSqlCheck` in the app.

`--inline-sql` appends `"inline-sql"` to `Options.EnableIds`. That id must match `InlineSqlCheck.CheckId`.

Truth table (same as today):

| Args | Runs |
|---|---|
| (default) | all `EnabledByDefault` |
| `--inline-sql` | default-on **plus** `inline-sql` |
| `--check inline-sql` | only `inline-sql` |
| `--check magic-literal --inline-sql` | only `magic-literal` (`--check` wins; enable-ids ignored) |
| `--check nope` | `Unknown check: nope`, exit 2 |

`Options` today: `(bool Human, bool Stats, bool InlineSql, IReadOnlyList<string> CheckIds, IReadOnlyList<string> Paths)`.

Replace `InlineSql` with `EnableIds`. Parser: `--inline-sql` → `enableIds.Add("inline-sql")`. No other flag changes.

Unknown-id validation stays on `--check` values only, not on enable-ids (the sugar is a known literal).

## 4. Tests: delete `Run` / `CheckContext`

Every check test file currently:

```csharp
ICheck check = new FooCheck();
return check.Run(new CheckContext([new SourceFile(path, source)]));
```

**File checks** — call `Analyze` directly:

```csharp
private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
{
    var file = new SourceFile(path, source);
    return new FooCheck().Analyze(file, file.Tree);
}
```

Do **not** introduce a shared test runner. That would recreate `CheckContext`.

**`UnusedUsingCheckTests`** — call `AnalyzeCompilation` with the production compilation helper:

```csharp
private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
{
    var file = new SourceFile(path, source);
    var compilation = CSharpCompilations.Create("qlcheck", [file.Tree]);
    var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
    return new UnusedUsingCheck().AnalyzeCompilation(compilation, included);
}
```

`CSharpCompilations` is `internal`. Add to `Qlcheck.csproj`:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="Qlcheck.Tests" />
</ItemGroup>
```

`QlcheckAppTests` and `CheckDiscoveryTests` should stay green with no assertion changes if behavior is preserved. `Checks run 6` remains 6.

Optional: one discovery/app test that a default run does not mention `InlineSqlCheck` by going through `EnabledByDefault` — existing `Default_run_skips_inline_sql` already covers behavior.

## 5. Check-by-check edits

### `UnusedUsingCheck`

- `ICompilationCheck` only
- Drop `Analyze => []` and the explicit `ICheck.AnalyzeCompilation` shim
- `Finding.At(CheckId, path, diagnostic.Location, Message, string.Empty)`
- Keep unresolved-tree / CS8019 / `UsingResolved` logic as-is

### `InlineSqlCheck`

- `IFileCheck`
- `public bool EnabledByDefault => false;`
- Replace `MakeFinding` with `Finding.At`
- No other logic changes

### Other file checks

- `IFileCheck` instead of `ICheck`
- Replace hand-built `new Finding(...)` with `Finding.At`
- Drop private `MakeFinding` where present (`StringConcatCheck`)

### `QlcheckApp`

- Filter via `EnabledByDefault` / `EnableIds` / `CheckIds` as above
- Split `fileChecks` / `compilationChecks`
- Skip compilation groups when `compilationChecks` is empty
- Zero references to concrete check types

## 6. ADR

`docs/adr/0004-file-and-compilation-checks.md`:

- A Check is still an in-process type (0002). Syntax inspection and compilation inspection are different interfaces because they have different inputs.
- The CLI selects by `Id` and `EnabledByDefault`. It does not name check types.
- `Finding.At` owns path normalization and stable ids.
- Tests exercise `Analyze` / `AnalyzeCompilation` directly. There is no second runner.

Update ADR 0002’s wording only if it still says “implements `ICheck`” as the analyze surface — keep “implements `ICheck`” (both sub-interfaces do) so 0002 does not rot.

`CONTEXT.md`: no vocabulary change. `--inline-sql` / `--check inline-sql` remain the documented opt-in.

## 7. Out of scope

- Unifying `IsIgnoredContext` / `IsConstantContext` (policies differ)
- Extracting the stats box
- Deleting `--inline-sql`
- Line-length Check (`docs/plan-line-length-check.md`)
- Config files, plugins, extra packages

## Tracer order

Implement in this order so each step is red-green and deletes something:

1. **`Finding.At` + `StringEmptyCheck`** — smallest file check. Tests call `Analyze` instead of `Run`. Proves the factory and the test seam.
2. **Interfaces in `ICheck.cs`.** `StringEmptyCheck : IFileCheck`. `CheckDiscovery` filter. Delete `CheckContext` and `Run` only after every check is migrated — so do 3–4 before deleting, or migrate all file checks in this slice then compilation.
3. **Remaining `IFileCheck`s** (`MagicLiteral`, `StringConcat`, `SwitchPattern`, `OneTypePerFile`, `InlineSql`) + `Finding.At` + `EnabledByDefault => false` on inline-sql. File-check tests all call `Analyze`.
4. **`UnusedUsingCheck : ICompilationCheck`**, `InternalsVisibleTo`, tests call `AnalyzeCompilation`. Delete `Analyze => []` and DIM shim.
5. **Delete `CheckContext` and `ICheck.Run`.** Root `ICheck` is only `Id` + `EnabledByDefault`.
6. **`QlcheckApp` filter + skip compile.** Remove `InlineSqlCheck` type references. Replace `Options.InlineSql` with `EnableIds`.
7. **ADR 0004.** Run tests. `qlcheck` on every changed `.cs` path.

If a slice needs `Run` still present for unmigrated tests, keep it until slice 5, then delete. Do not leave it as a compatibility layer.

## Done when

- `grep` finds no `CheckContext`, no `.Run(new CheckContext`, no `AnalyzeCompilation` on `ICheck`, no `InlineSqlCheck` under `QlcheckApp.cs`
- Default `qlcheck <path>` does not build a compilation unless `unused-using` is in the selected set
- `--inline-sql` / `--check` truth table above still holds
- All existing tests pass without a shared test runner
- No file crosses 1k (`QlcheckApp` should shrink)
