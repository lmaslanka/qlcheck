# File checks and compilation checks are different interfaces

Superseded by 0007: `IFileCheck` and `ICompilationCheck` are gone. A row needing per-file syntax inspection is a `SyntaxEngine`/etc. row; a row needing whole-compilation diagnostics is a `CompilationEngine` row, run once per `Compilation` from the same seam the catalog already builds one in (`CatalogRun`). The CLI-selects-by-`Id`/`Finding.At`-owns-ids points below still hold.

A Check is still an in-process type (0002). Syntax inspection and compilation inspection take different inputs, so they are `IFileCheck` (`Qlcheck.Languages.CSharp.Checks.File`) and `ICompilationCheck` (`Qlcheck.Languages.CSharp.Checks.Compilation`). The CLI selects by `Id` and `EnabledByDefault`; it does not name check types. `Finding.At` owns path normalization and stable ids. Tests call `Analyze` / `AnalyzeCompilation` directly. There is no second runner.
