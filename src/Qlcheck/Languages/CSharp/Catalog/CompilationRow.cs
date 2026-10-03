// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

internal sealed class CompilationRow
{
    public required string Id { get; init; }

    public required CheckClass Class { get; init; }

    public required Action<CompilationContext> Apply { get; init; }
}
