using Qlcheck.Secrets;

namespace Qlcheck.Tests;

// Each connection string is split before its password so no committed line holds a whole one.
public class ConnectionStringsTests
{
    [Theory]
    [InlineData("Server=db;User Id=sa;Password=", "Hunter2!;")]
    [InlineData("Data Source=db;Initial Catalog=app;pwd=", "'Hunter2!'")]
    [InlineData("Host=db;Username=app;Password=", "\"Hunter2!\";Pooling=true")]
    [InlineData("postgres://app:", "Hunter2!@db:5432/app")]
    [InlineData("mongodb+srv://app:", "Hunter2!@cluster.mongodb.net/app")]
    [InlineData("redis://default:", "Hunter2!@cache:6379")]
    public void HasPassword_finds_a_real_password(string head, string tail)
    {
        Assert.True(ConnectionStrings.HasPassword(FakeSecrets.Join(head, tail)));
    }

    [Theory]
    [InlineData("Server=db;User Id=sa;Password=;")]
    [InlineData("Server=db;User Id=sa;Password={password};")]
    [InlineData("Server=db;User Id=sa;Password=${DB_PASSWORD};")]
    [InlineData("Server=db;Integrated Security=true;")]
    [InlineData("Password=Hunter2!")]
    [InlineData("Mode=fast;Password=Hunter2!")]
    [InlineData("log: user logged in; Server=db; Password=Hunter2!")]
    [InlineData("class C { string s = \"Server=db;User Id=sa;Password=Hunter2!;\"; }")]
    [InlineData("postgres://app@db/app")]
    [InlineData("https://user:pass@example.org")]
    public void HasPassword_ignores_empty_placeholder_and_non_connection_text(string value)
    {
        Assert.False(ConnectionStrings.HasPassword(value));
    }
}
