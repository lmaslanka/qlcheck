namespace Qlcheck.Tests;

public class CheckDiscoveryTests
{
    [Fact]
    public void Discovers_inline_sql_check()
    {
        var checks = CheckDiscovery.All();
        Assert.Contains(checks, c => c.Id == "inline-sql" && c is InlineSqlCheck);
        Assert.Contains(checks, c => c.Id == "magic-literal" && c is MagicLiteralCheck);
        Assert.Contains(checks, c => c.Id == "string-concat" && c is StringConcatCheck);
        Assert.Contains(checks, c => c.Id == "string-empty" && c is StringEmptyCheck);
        Assert.Contains(checks, c => c.Id == "switch-pattern" && c is SwitchPatternCheck);
        Assert.Contains(checks, c => c.Id == "unused-using" && c is UnusedUsingCheck);
        Assert.Contains(checks, c => c.Id == "one-type-per-file" && c is OneTypePerFileCheck);
    }
}
