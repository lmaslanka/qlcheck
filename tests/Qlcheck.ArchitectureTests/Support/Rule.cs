namespace Qlcheck.ArchitectureTests.Support;

/// <summary>An architecture rule: a stable id (matches its baseline file), a title and fix guidance.</summary>
public sealed record Rule(string Id, string Title, string Guidance);
