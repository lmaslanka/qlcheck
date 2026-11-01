// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

internal sealed class ArchitectureRow
{
    public required string Id { get; init; }

    public required CheckClass Class { get; init; }

    // Findings for the run's governed dependencies; the message map holds the catalog messages.
    public required Func<IReadOnlyList<GovernedDependency>, IReadOnlyDictionary<string, string>, IEnumerable<Finding>> Apply { get; init; }
}
