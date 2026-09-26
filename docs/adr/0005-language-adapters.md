# A Language owns parsing and which Checks run

A Check belongs to one Language and has a globally unique id. The runner discovers Languages the same way it discovers Checks: in-process types, not plugins (0002). It gives each Language only the files that language claims and only the Checks whose Language matches. `IFileCheck` and `ICompilationCheck` are the C# adapter's inputs (Roslyn). They are not a shared tree. Adding a language is a new `ILanguage` plus its Checks; the CLI, scan, and dispatcher do not name that language. ADR 0001 and 0003 are C# adapter decisions.
