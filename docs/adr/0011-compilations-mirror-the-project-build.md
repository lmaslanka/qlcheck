# Compilations mirror the project's build

Extends 0003. A csproj's `CSharpCompilation` should bind the way `dotnet build` does, so a type
that compiles fine is never an error type to a semantic Check. Missing build settings are now a
fidelity bug, not an accepted limit. Three things changed:

- **Build properties.** `<Nullable>` and `<OutputType>` (plus the target framework, `ImplicitUsings`,
  `<Using>` and `<FrameworkReference>` items) are read from the csproj and the nearest
  `Directory.Build.props`. Files outside any csproj compile as a nullable-enabled library.
- **Global usings.** The newest `obj/**/<csproj file name>.GlobalUsings.g.cs` is loaded when it is at
  least as new as the csproj. Otherwise the same list is synthesized from the SDK defaults and
  `<Using>` items. The tree is a *context tree*: compiled, never walked or reported. Compilation
  Checks report only on the scanned sources.
- **ASP.NET Core.** Web SDK projects, and projects with a `FrameworkReference` to
  `Microsoft.AspNetCore.App`, get the `Microsoft.AspNetCore.App.Ref` pack installed next to the
  running runtime. References are deduped by assembly file name, and project or package references
  win over the pack.

We accept partial MSBuild fidelity rather than calling `dotnet msbuild`, which would cost a
subprocess and seconds per project (0002). The XML is read in process: only unconditioned groups
and elements count, the csproj wins over the props file, and `Directory.Build.targets`, `<Import>`s
and custom SDKs are not seen. A misread setting fails safe where it can, because trees with
unresolved names are still skipped by `unused-using`.

The surprising part is that qlcheck now reads `obj/` build output, and it trusts a generated file
over the csproj when that file is newer. A repository should be restored or built for full results.
The cost: on this repo's self-scan, 118 more Findings (mostly `dispose-created` and
`magic-literal-shared` in tests) that error types had been hiding.
