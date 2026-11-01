// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

// Test frameworks' test-method attributes (xunit, NUnit, MSTest). A test method's parameters come
// from the test itself, so they are never untrusted input.
internal static class TestMethods
{
    private const string AttributeSuffix = "Attribute";

    public static IReadOnlySet<string> AttributeNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Fact", "Theory", "Test", "TestCase", "TestMethod", "DataTestMethod",
    };

    // The method carries a test attribute, resolved or not (an unresolved attribute keeps the name
    // it was written with).
    public static bool IsTest(IMethodSymbol method) =>
        method.GetAttributes().Any(attribute => attribute.AttributeClass?.Name is { } name
            && AttributeNames.Contains(name.EndsWith(AttributeSuffix, StringComparison.Ordinal) ? name[..^AttributeSuffix.Length] : name));
}
