# Plan 00: compilations that match the real build

Status: Agreed 2026-10-29 · Depends on: none · Unblocks: 02, 03, 06

Probes and raw output (outside the repo):
`/tmp/claude-1000/-home-lmaslanka-projects-qlcheck/1fadbd8a-84fa-480a-a869-298258cc64bb/scratchpad/probes-nullable/`.
- `probe/` counts diagnostics per project.
- `probe3/` times binding.
- `copy/` is a patched build used for the blast-radius diff.
- `base.json` and `globals.json` are the self-scan outputs before and after adding global usings.

## Goal

Each csproj's `CSharpCompilation` should bind the way `dotnet build` does, so semantic Checks stop seeing error types
for code that compiles fine. Three gaps need closing:

1. **Implicit/global usings.** Load the project's generated `GlobalUsings.g.cs`, or synthesize one from the csproj.
2. **Build properties.** Read `<Nullable>`, `<OutputType>` and the target framework from the csproj (and the nearest
   `Directory.Build.props`), and apply them to the compilation options.
3. **ASP.NET shared framework.** Reference the installed `Microsoft.AspNetCore.App.Ref` pack when the project is a web
   project.

This is the prerequisite for Plan 02 (nullable Findings), Plan 03 (taint sources and sinks resolve by symbol) and
Plan 06 (architecture dependencies see `System.*` targets).

Acceptance:
- On this repo, no `.cs` tree in any project compilation has CS0103, CS0246 or CS1061. Today 128 of the 145 trees do.
- The self-scan Finding delta equals the agreed list below.

## Current state

- **Options ignore the project.** `src/Qlcheck/Languages/CSharp/CSharpCompilations.cs:40-52` always builds
  `new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)`, which means nullable is disabled and the
  output is a DLL. The only thing it sets is CS8019→Warn.
  - The csproj is read only for `HintPath` (`ProjectReferences.cs:128-137`).
  - Package and project refs come from `obj/project.assets.json` (`ProjectReferences.cs:139-162`).
  - Platform refs are qlcheck's own TPA (`CSharpCompilations.cs:72-84`), which is `Microsoft.NETCore.App` only.
- **Global usings are absent.** `obj/` is ignored by default, so `obj/<Debug|Release>/<tfm>/<MSBuildProjectName>.GlobalUsings.g.cs`
  never enters a compilation. The file is named after the **csproj file name**, not `AssemblyName`. Verified:
  `src/Qlcheck/obj/Debug/net10.0/Qlcheck.GlobalUsings.g.cs`, although `AssemblyName` is `qlcheck`. The generated file
  carries both the SDK defaults and `<Using>` items: the tests project's file has `global using Xunit;` from
  `tests/Qlcheck.Tests/Qlcheck.Tests.csproj`'s `<Using Include="Xunit" />`.
- **Census of trees with unresolved ids (CS0103, CS0246, CS1061), current options:**

  | Project | Today | With GlobalUsings.g.cs |
  |---|---|---|
  | src/Qlcheck (89 trees) | 76 | 0 |
  | tests/Qlcheck.Tests (51 trees) | 51 | 0 |
  | tests/Qlcheck.ArchitectureTests (5 trees) | 1 | 0 |

  Two src trees still have *other* errors after the fix:
  - CS8805: `Program.cs` uses top-level statements, but the compilation is built as a DLL. Fixed by `OutputType`.
  - CS8795: the `[GeneratedRegex]` partial in `Run/Suppressions.cs`. Source generators don't run, and that is out of scope.
- **Who suffers.**
  - `UnusedUsingPattern.cs:8-13,19-24` skips every tree with an unresolved id, so `unused-using` is blind in about
    90% of this repo's files.
  - Every Symbol and Taint row binds `List<>`, `Path`, `File` and similar types to error types.
- **Call path.**
  - `CSharpLanguage.cs:71` groups loaded files by `FindCsproj`.
  - `CatalogRun.cs:40-55` calls `Create` (adhoc) or `CreateForProject`.
  - `CatalogRun.cs:23-27` walks only the loaded `sources`, so an extra compilation tree is never walked.
  - Compilation rows filter by `CompilationContext.Includes` (`CompilationContext.cs:32-35`). An empty included set
    means every tree. `CSharpLanguage` always passes a non-empty set, but some tests rely on the empty fallback.
- **ASP.NET.** No web project resolves `Microsoft.AspNetCore.*`, because that framework is a `FrameworkReference`
  and never appears in `project.assets.json` as a package.
  - On this machine the pack is at `<dotnet root>/packs/Microsoft.AspNetCore.App.Ref/{10.0.4,10.0.8}/ref/net10.0/`.
  - The `<dotnet root>` is three levels above `typeof(object).Assembly.Location`
    (`shared/Microsoft.NETCore.App/<ver>/`).
  - The runtime copy at `shared/Microsoft.AspNetCore.App/<ver>/` also exists.
- **Tests today.** `tests/Qlcheck.Tests/Languages/CSharp/CSharpCompilationsTests.cs` covers only `FindCsproj`.
  `ProjectReferencesTests.cs` uses temp csproj directories, which is the pattern to copy.

### Measured blast radius (self-scan, this repo)

Global usings take the self-scan from 367 to 485 Findings: 121 added and 3 removed, so **+118 net**. The user accepts
all of them. By check:

| Check | Δ | Where | Notes |
|---|---|---|---|
| `dispose-created` | **+70** | tests/Qlcheck.Tests | Mostly `new StringWriter()` passed to `QlcheckApp.Run`. Now visible because `StringWriter` resolves as `IDisposable`. |
| `magic-literal-shared` | **+36** | tests/Qlcheck.Tests | The same literal fed to the same now-resolved parameter across test files. |
| `unused-using` | **+12** | 5 in src/Qlcheck, 7 in tests/Qlcheck.Tests | src: `Catalog/TaintEngine.cs:2`, `Catalog/FlowEngine.cs:2`, `Catalog/MetricEngine.cs:2`, `Catalog/SymbolEngine.cs:2`, `CSharpLanguage.cs:4`. tests: `Catalog/Queries/SymbolsTests.cs:1,2,4`, `Run/SuppressionsTests.cs:1,2` (duplicates of `GlobalUsings.cs`), `Languages/CSharp/CSharpLanguageTests.cs:2`, `GlobalUsings.cs:1`. |
| `disposable-pattern` | **+3** | tests/Qlcheck.Tests | `QlcheckAppTests.cs:31`, `Scan/DisableFilesTests.cs:18`, `Scan/IgnoreTreeTests.cs:11`. |
| `dispose-implements-interface` | **−3** | tests/Qlcheck.Tests | The same three classes. `IDisposable` now resolves, so they correctly move to `disposable-pattern`. |

Other parts of this plan:
- **`<Nullable>`**: measured on top of global usings, it changed **0** Findings.
- **`OutputType`**: not measured separately. Its only effect is that `src/Qlcheck/Program.cs` loses CS8805 and so
  becomes eligible for `unused-using`. That is 0 or 1 Finding; Program.cs's single `using Qlcheck;` is used.
- **ASP.NET pack**: this repo is not a web project, so 0.

Fixing these Findings is not part of this plan. A later dogfood commit can do it.

## Design

### 1. `ProjectSettings` (new, next to `ProjectReferences.cs`)

`ProjectSettings.Load(csproj)` returns an immutable record:
- `Sdk`: the `Project/@Sdk` attribute, or `<Sdk Name>` / `<Import Sdk>`.
- `Nullable`, as a `NullableContextOptions`.
- `OutputKind`.
- `TargetFrameworks`: from `TargetFramework`, or the split `TargetFrameworks`.
- `ImplicitUsings`: a bool.
- `Usings`: the `<Using>` items with `Include`, `Remove`, `Alias` and `Static`.
- `FrameworkReferences`.

How it reads them:
- It takes properties from **unconditioned** `<PropertyGroup>` elements of the nearest `Directory.Build.props`, then
  the csproj. The csproj wins, and within a file the last value wins.
- Items come from unconditioned `<ItemGroup>` elements in the same two files.
- The XML is parsed in process, with no `dotnet msbuild` call (ADR 0002).

Value mapping:
- `Nullable` maps `enable`/`disable`/`warnings`/`annotations` case-insensitively. A missing value means `Disable`,
  the SDK default.
- `OutputType` maps `Exe`→`ConsoleApplication` and `WinExe`→`WindowsApplication`. Anything else gives
  `DynamicallyLinkedLibrary`.
- `ImplicitUsings` accepts `enable` or `true`.

### 2. Global usings tree

`ImplicitUsingsTree.For(csproj, settings)`:
- **Generated file first.** Use the newest `obj/**/{csprojFileName}.GlobalUsings.g.cs` if it exists and is at least as
  new as the csproj. A csproj edited after the last build (for example a new `<Using>`) falls through to synthesis.
- **Otherwise synthesize**, but only when ImplicitUsings is on or `<Using>` items exist. The synthesized tree holds the
  SDK default list, then `<Using>` items applied (`Remove` drops, `Alias` gives `global using A = B;`, `Static` gives
  `global using static`). The default lists:
  - `Microsoft.NET.Sdk`: System, System.Collections.Generic, System.IO, System.Linq, System.Net.Http,
    System.Threading, System.Threading.Tasks.
  - `Microsoft.NET.Sdk.Web` adds System.Net.Http.Json, Microsoft.AspNetCore.Builder, Microsoft.AspNetCore.Hosting,
    Microsoft.AspNetCore.Http, Microsoft.AspNetCore.Routing, Microsoft.Extensions.Configuration,
    Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Hosting, Microsoft.Extensions.Logging.
  - `Microsoft.NET.Sdk.Worker` adds the four Microsoft.Extensions.* namespaces.
  - Any other SDK gets only `<Using>` items.
- **Context tree.** The tree is added to the compilation as a *context tree*: it is compiled but never walked or
  reported. `CompilationContext` gets an explicit set of reportable trees, the loaded sources, in place of the empty-set
  fallback in `Includes`. This is how Plans 02 and 06 rely on it.

### 3. Options

`CSharpCompilations.CreateForProject` applies `settings.Nullable` and `settings.OutputKind`.
- The adhoc compilation (files outside any csproj) uses `NullableContextOptions.Enable` and DLL output. Plan 02 D2
  decided this.
- `#nullable` directives keep overriding per file, as before.
- Parse options are unchanged: latest language version, no preprocessor symbols.

### 4. ASP.NET shared framework

If `Sdk` is `Microsoft.NET.Sdk.Web` (or `.Razor` / `.BlazorWebAssembly`), or `FrameworkReferences` contains
`Microsoft.AspNetCore.App`, add the reference pack's DLLs.

Locating the pack (`SharedFrameworks.AspNetCore()`, lazy and cached like `PlatformReferences`):
- First choice: `<dotnet root>/packs/Microsoft.AspNetCore.App.Ref/<highest version with the running runtime's major>/ref/net<major>.0/*.dll`.
- Fallback: `<dotnet root>/shared/Microsoft.AspNetCore.App/<same rule>/*.dll`.
- If neither exists, add nothing: web types stay unresolved and the affected trees are skipped, which is the safe
  direction.

`Merge` (`CSharpCompilations.cs:54-70`) dedupes by path today. It changes to dedupe **by assembly file name**, and
project or package refs win over the pack. Without that, a NuGet `Microsoft.Extensions.*` plus the pack's copy would
give CS0433 ambiguity errors.

### 5. TargetFramework

The TFM is only exposed here. Plan 02 uses it to skip nullable Findings on non-annotated TFMs. Platform refs stay
qlcheck's net10 TPA, and multi-target fidelity is out of scope.

## Implementation steps (test-first, one commit each)

1. **`ProjectSettings`.**
   - Red: `tests/Qlcheck.Tests/Languages/CSharp/ProjectSettingsTests.cs`, using temp csproj directories:
     - each `<Nullable>` value, plus a missing value meaning Disable;
     - `Directory.Build.props` inherited and overridden by the csproj;
     - a conditioned property ignored;
     - `OutputType` Exe, WinExe and Library;
     - `TargetFrameworks` split;
     - `Sdk` attribute vs `<Sdk>` element;
     - `<Using>` Include, Remove, Alias and Static;
     - `FrameworkReference`.
   - Green: the reader.
2. **Context trees in `CompilationContext`.**
   - Red: a compilation row never reports on a tree outside the reportable set, even when `IncludedFiles` is empty.
   - Green: an explicit reportable set, passed by `CatalogRun` from `sources`. Port `UnusedUsingPatternTests` and
     `CatalogTests.cs:424-441` to pass it.
3. **Global usings tree.**
   - Red, in `CSharpCompilationsTests`:
     - an ImplicitUsings project resolves `List<int>` and `Path` with no `using`;
     - the generated g.cs wins over synthesis when it is newer than the csproj;
     - a csproj newer than the g.cs uses synthesis;
     - `<Using Include="Xunit"/>` (a stub type) resolves, and `<Using Remove="System.Linq"/>` is honoured;
     - an unrelated SDK gets only `<Using>` items;
     - the g.cs tree yields no `unused-using` Finding.
   - Green: `ImplicitUsingsTree` wired into `CreateForProject`.
   - Then run the self-scan diff. It must equal the table above, with any deviation explained in the commit message.
4. **Nullable + OutputKind.**
   - Red: `Options.NullableContextOptions` matches each csproj value; adhoc is `Enable`; an Exe project with
     top-level statements has no CS8805.
   - Green: wire it in.
   - Re-run the full test suite, since the adhoc change touches every fixture compilation. Then re-run the self-scan:
     expect at most one more Finding (Program.cs).
5. **ASP.NET pack.**
   - Red: a `Sdk="Microsoft.NET.Sdk.Web"` temp project and a `FrameworkReference` temp project each resolve
     `Microsoft.AspNetCore.Http.HttpContext`. A Library project does not get the pack. A duplicate
     `Microsoft.Extensions.Logging.Abstractions` from assets wins with no CS0433.
   - Green: `SharedFrameworks` + `Merge` by file name. Skip the test with a clear message when the pack isn't
     installed.
6. **ADR 0011 + docs.**
   - Add `docs/adr/0011-compilations-mirror-the-project-build.md`.
   - In README "How it's built", add one sentence saying project settings and implicit usings are honoured and
     `obj/` must be restored or built.
   - Add a CONTEXT.md entry only if a new term is needed. None is expected.

## Files touched

- `src/Qlcheck/Languages/CSharp/ProjectSettings.cs` (new)
- `src/Qlcheck/Languages/CSharp/ImplicitUsingsTree.cs` (new)
- `src/Qlcheck/Languages/CSharp/SharedFrameworks.cs` (new)
- `src/Qlcheck/Languages/CSharp/CSharpCompilations.cs`
- `src/Qlcheck/Languages/CSharp/Catalog/CatalogRun.cs`, `Catalog/CompilationContext.cs`, `Catalog/UnusedUsingPattern.cs`
  (only for the reportable-tree change)
- `tests/Qlcheck.Tests/Languages/CSharp/ProjectSettingsTests.cs` (new)
- `tests/Qlcheck.Tests/Languages/CSharp/CSharpCompilationsTests.cs`
- `tests/Qlcheck.Tests/Catalog/UnusedUsingPatternTests.cs`, `Catalog/CatalogTests.cs`
- `docs/adr/0011-compilations-mirror-the-project-build.md` (new), `README.md`

## ADR needed?

**Yes: ADR 0011, "Compilations mirror the project's build."** It meets all three tests:
- **Hard to reverse.** Every semantic Check's results shift. Here that is +118 Findings, and Plans 02, 03 and 06 build
  on it.
- **Surprising.** qlcheck now reads `obj/` build output and MSBuild properties, and it trusts a generated file over
  the csproj when that file is newer.
- **A real trade-off.** It accepts partial MSBuild fidelity (unconditioned properties, the nearest
  `Directory.Build.props` only) rather than calling `dotnet msbuild`, which would cost a subprocess and seconds per
  project (ADR 0002).

The ADR extends ADR 0003, which built a compilation from scanned trees plus platform refs: missing settings are now a
fidelity bug, not an accepted limit.

## Risks

- **MSBuild fidelity.** Conditioned properties, `Directory.Build.targets`, custom SDKs and `<Import>`s are not seen.
  - A misread `ImplicitUsings`: unresolved trees, skipped silently.
  - A misread `Nullable`: Plan 02 false positives (Enable read wrongly) or silence (Disable read wrongly).
- **Stale `obj/`.** An old g.cs whose csproj changed after the build is caught by the mtime rule. Other generators'
  output is not loaded.
- **Pack and TFM mismatch.** A net8 web project binds against the net10 ASP.NET pack. A few APIs differ. Accepted:
  missing members produce errors, which are skipped, rather than wrong results.
- **Assembly dedupe by file name** could drop a legitimately different same-named assembly. That is rare, and it
  matches SDK conflict resolution in spirit.
- **Adhoc `Nullable=Enable`** changes the semantic model for every fixture test. Symbol checks that compare display
  strings could see `string?` where they used to see `string`. Mitigation: run the full suite in step 4. Measured:
  enabling nullable changed 0 project Findings.
- **Performance.**
  - Enabling nullable costs about +20–40% of the per-tree semantic walk in the warm probe (about 240 to 400 ms on
    src/Qlcheck). The full self-scan is 5.3 s today; estimate +0.2–0.5 s.
  - One extra tree per project is negligible.
  - The ASP.NET pack adds about 200 metadata references to web projects, loaded lazily once per run.
- **New Findings on users' repos** are the same kind as here: previously hidden `dispose-created`,
  `magic-literal-shared` and `unused-using` Findings in ImplicitUsings projects.

## Effort

About 1.5–2 days:

| Steps | Effort |
|---|---|
| 1 | ½ day |
| 2–3 | ½ day |
| 4 | ¼ day |
| 5 | ½ day |
| 6 (ADR and docs) | ¼ day |

## Decisions (resolved)

- **Global usings:** load the newest generated `obj/**/<csprojName>.GlobalUsings.g.cs` when present and not older
  than the csproj. Otherwise synthesize it from `ImplicitUsings`, the SDK, and `<Using>` items. This matches Plan 06 D7.
- **Accept the new Findings** from existing Checks: +118 net on this repo, listed above. They are not fixed in this plan.
- **Read `<Nullable>` and `<OutputType>`** from the csproj and the nearest `Directory.Build.props`, unconditioned
  only, in process.
- **Adhoc compilation uses Nullable=Enable** (Plan 02 D2).
- **Add `Microsoft.AspNetCore.App.Ref`** for `Microsoft.NET.Sdk.Web` and `FrameworkReference Microsoft.AspNetCore.App`.
  This matches Plan 03 D3.
- **ADR number 0011.**
