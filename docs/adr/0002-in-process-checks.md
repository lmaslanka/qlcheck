# Checks are in-process types, not loadable plugins

A house Check is a class in this assembly that implements `ICheck`. Catalog Checks are rows (0006). C# file checks live in `Qlcheck.Languages.CSharp.Checks.File`, compilation checks in `Qlcheck.Languages.CSharp.Checks.Compilation`. Discovery is reflection. Adding a Check means adding a type, not an extension host. External assemblies can wait until a Check actually lives outside this repo.
