# File checks and compilation checks are different interfaces

A Check is still an in-process type (0002). Syntax inspection and compilation inspection take different inputs, so they are `IFileCheck` and `ICompilationCheck`. The CLI selects by `Id` and `EnabledByDefault`; it does not name check types. `Finding.At` owns path normalization and stable ids. Tests call `Analyze` / `AnalyzeCompilation` directly. There is no second runner.
