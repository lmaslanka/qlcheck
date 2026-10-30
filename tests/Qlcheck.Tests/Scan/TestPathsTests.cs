namespace Qlcheck.Tests;

public class TestPathsTests
{
    [Theory]
    [InlineData("tests/App.Tests/OrderTests.cs", true)]
    [InlineData("src/app/testing/Helpers.cs", true)]
    [InlineData("src/App/Latest.cs", true)]
    [InlineData("src/App/Order.cs", false)]
    public void IsTest_matches_test_anywhere_in_the_path(string path, bool expected)
    {
        Assert.Equal(expected, TestPaths.IsTest(path));
    }
}
