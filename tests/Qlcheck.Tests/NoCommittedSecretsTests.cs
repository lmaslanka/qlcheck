using System.Text.RegularExpressions;
using Qlcheck.Secrets;

namespace Qlcheck.Tests;

// Push protection and secret scanners read every committed file, tests and docs included. Test
// tokens are built at run time (FakeSecrets), and this guard fails if a whole token ever lands in
// a file under src/, tests/ or docs/. It also looks for the token formats the classifier
// deliberately ignores but push protection still blocks, such as Stripe test keys.
public class NoCommittedSecretsTests
{
    private static readonly string[] Roots = ["src", "tests", "docs"];

    private static readonly string[] BuildDirs = ["bin", "obj"];

    private static readonly Regex IgnoredButBlocked = new(@"\b[rs]k_test_[0-9A-Za-z]{16,}", RegexOptions.CultureInvariant);

    [Fact]
    public void No_committed_file_holds_a_known_token()
    {
        var root = RepoPaths.Root();
        var offenders = Roots
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(root, dir), "*", SearchOption.AllDirectories))
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Any(BuildDirs.Contains))
            .Where(path => HoldsToken(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void A_stripe_test_key_counts_as_a_token()
    {
        Assert.True(HoldsToken(FakeSecrets.StripeTest));
        Assert.False(HoldsToken("sk_test_short"));
    }

    private static bool HoldsToken(string text) => SecretFormats.ProviderOf(text) is not null || IgnoredButBlocked.IsMatch(text);
}
