namespace Qlcheck.Tests;

public class LcovMapTests
{
    private const string Foo = "/repo/src/Foo.cs";

    private const string Bar = "/repo/src/Bar.cs";

    private const int CoveredLine = 4;

    private const int UncoveredLine = 5;

    private const int OtherLine = 8;

    private const int CoveredHits = 1;

    private const string TryParse = "TryParse";

    private const string Other = "Other";

    [Fact]
    public void Parses_uncovered_lines_and_maps_methods()
    {
        var lcov = $"""
            SF:{Foo}
            FN:{CoveredLine},TryParse
            DA:{CoveredLine},{CoveredHits}
            DA:{UncoveredLine},0
            FN:{OtherLine},Other
            DA:{OtherLine},0
            end_of_record
            """;

        var document = LcovMap.Parse(lcov);

        Assert.True(document.TryGet(Foo, out var foo));
        Assert.Contains(foo.Lines, line => line.Line == CoveredLine && line.Hits == CoveredHits && line.Method == TryParse);
        Assert.Contains(foo.Lines, line => line.Line == UncoveredLine && line.Hits == 0 && line.Method == TryParse);
        Assert.Contains(foo.Lines, line => line.Line == OtherLine && line.Hits == 0 && line.Method == Other);
    }

    [Fact]
    public void Matches_a_relative_source_path()
    {
        var lcov = """
            SF:src/Foo.cs
            DA:4,0
            end_of_record
            """;

        var document = LcovMap.Parse(lcov);

        Assert.True(document.TryGet(Foo, out var foo));
        Assert.Contains(foo.Lines, line => line.Line == CoveredLine && line.Hits == 0);
    }

    [Fact]
    public void Merge_keeps_a_hit()
    {
        var missed = LcovMap.Parse($"""
            SF:{Foo}
            DA:{UncoveredLine},0
            end_of_record
            """);
        var hit = LcovMap.Parse($"""
            SF:{Foo}
            DA:{UncoveredLine},{CoveredHits}
            end_of_record
            SF:{Bar}
            DA:{CoveredLine},0
            end_of_record
            """);

        missed.Merge(hit);

        Assert.True(missed.TryGet(Foo, out var foo));
        Assert.Contains(foo.Lines, line => line.Line == UncoveredLine && line.Hits == CoveredHits);
        Assert.True(missed.TryGet(Bar, out var bar));
        Assert.Contains(bar.Lines, line => line.Line == CoveredLine && line.Hits == 0);
    }
}
