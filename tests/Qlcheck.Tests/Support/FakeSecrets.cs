namespace Qlcheck.Tests;

// fake: not a real credential. Format-valid tokens built at run time, so no committed file holds a
// whole token (push protection and secret scanners read every file) and none is a compile-time
// constant the C# secret Checks would fold. The body is a walk over a fixed alphabet with no
// EXAMPLE/FAKE/DUMMY marker, because those markers are deliberately allowed by the classifier.
internal static class FakeSecrets
{
    private const string Alphabet = "Q7mZ2pK9xR4tW8vN3bL6cH1jF5gD0sY";

    private const int Stride = 7;

    private const int TokenBody = 24;

    private const int JwtPart = 12;

    private const int WebhookPart = 9;

    private const int ApiKeyBody = 32;

    public static string GitHub => $"ghp_{Body(36)}";

    public static string GitHubFineGrained => $"github_pat_{Body(82)}";

    public static string Aws => $"AKIA{Body(16).ToUpperInvariant()}";

    // AWS's documented example key, which carries the EXAMPLE marker.
    public static string AwsDocumented => $"AKIA{Documented}EXAMPLE";

    public static string SlackToken => $"xoxb-{Body(TokenBody)}";

    public static string SlackWebhook =>
        $"https://hooks.slack.com/services/T{Body(WebhookPart)}/B{Body(WebhookPart)}/{Body(TokenBody)}";

    public static string StripeLive => $"sk_live_{Body(TokenBody)}";

    // A Stripe test-mode key, which the classifier deliberately ignores.
    public static string StripeTest => $"sk_test_{Body(TokenBody)}";

    public static string Google => $"AIza{Body(35)}";

    public static string PrivateKey => $"-----BEGIN RSA {Private} KEY-----";

    public static string Jwt => $"eyJ{Body(JwtPart)}.eyJ{Body(JwtPart)}.{Body(TokenBody)}";

    public static string AzureStorage => $"DefaultEndpointsProtocol=https;AccountName=store;AccountKey={Body(44)}==";

    public static string Anthropic => $"sk-ant-{Body(ApiKeyBody)}";

    public static string OpenAi => $"sk-proj-{Body(ApiKeyBody)}";

    // A random-looking value with no provider format, for secret-named values.
    public static string Random => $"Zq{Body(22)}";

    private static string Documented => "IOSFODNN7";

    private static string Private => "PRIVATE";

    public static string Join(params string[] parts) => string.Join(string.Empty, parts);

    private static string Body(int length) =>
        new(Enumerable.Range(0, length).Select(i => Alphabet[(i * Stride) % Alphabet.Length]).ToArray());
}
