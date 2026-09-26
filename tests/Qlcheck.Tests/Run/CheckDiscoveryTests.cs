namespace Qlcheck.Tests;

public class CheckDiscoveryTests
{
    [Fact]
    public void Discovers_inline_sql_check()
    {
        var checks = CheckDiscovery.All();
        Assert.Contains(checks, c => c.Id == InlineSqlCheck.CheckId && c is InlineSqlCheck);
        Assert.Contains(checks, c => c.Id == MagicLiteralCheck.CheckId && c is MagicLiteralCheck);
        Assert.Contains(checks, c => c.Id == StringConcatCheck.CheckId && c is StringConcatCheck);
        Assert.Contains(checks, c => c.Id == StringEmptyCheck.CheckId && c is StringEmptyCheck);
        Assert.Contains(checks, c => c.Id == SwitchPatternCheck.CheckId && c is SwitchPatternCheck);
        Assert.Contains(checks, c => c.Id == UnusedUsingCheck.CheckId && c is UnusedUsingCheck);
        Assert.Contains(checks, c => c.Id == OneTypePerFileCheck.CheckId && c is OneTypePerFileCheck);
        Assert.Contains(checks, c => c.Id == CoverageCheck.CheckId && c is CoverageCheck && !c.EnabledByDefault);
    }

    [Fact]
    public void Default_selection_skips_coverage()
    {
        Assert.True(CheckDiscovery.TrySelect([], [], out var checks, new StringWriter()));
        Assert.DoesNotContain(checks, check => check.Id == CoverageCheck.CheckId);
    }
}
