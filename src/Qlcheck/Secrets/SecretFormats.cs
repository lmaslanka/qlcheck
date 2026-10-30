// Copyright (c) qlcheck contributors.
using System.Text.RegularExpressions;

namespace Qlcheck.Secrets;

// Token formats a provider issues, recognised without any name. Each pattern needs the provider's
// full prefix plus a body, so the prefixes written here never match themselves.
internal static partial class SecretFormats
{
    private const int TimeoutMilliseconds = 200;

    private static readonly (string Provider, Func<Regex> Pattern)[] Formats =
    [
        ("AWS access key", AwsKey),
        ("GitHub token", GitHubToken),
        ("GitHub token", GitHubFineGrainedToken),
        ("Slack token", SlackToken),
        ("Slack webhook URL", SlackWebhook),
        ("Stripe live key", StripeLiveKey),
        ("Google API key", GoogleApiKey),
        ("private key", PrivateKey),
        ("JSON Web Token", JsonWebToken),
        ("Azure storage account key", AzureAccountKey),
        ("Azure shared access key", AzureSharedAccessKey),
        ("Anthropic API key", AnthropicKey),
        ("OpenAI API key", OpenAiKey),
    ];

    // The provider of the first token found in the value, or null. A token that carries a
    // documentation marker (AWS's `...EXAMPLE` key) does not count.
    public static string? ProviderOf(string value)
    {
        foreach (var (provider, pattern) in Formats)
        {
            if (Tokens(pattern(), value).Any(token => !SecretValues.HasExampleMarker(token)))
            {
                return provider;
            }
        }

        return null;
    }

    // A pathological value is not worth a stalled scan, so a timeout counts as no match.
    private static List<string> Tokens(Regex pattern, string value)
    {
        try
        {
            return pattern.Matches(value).Select(match => match.Value).ToList();
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
    }

    [GeneratedRegex(@"\b(?:AKIA|ASIA)[0-9A-Z]{16}\b", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex AwsKey();

    [GeneratedRegex(@"\bgh[pousr]_[A-Za-z0-9]{36}\b", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex GitHubToken();

    [GeneratedRegex(@"\bgithub_pat_[A-Za-z0-9_]{82}\b", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex GitHubFineGrainedToken();

    [GeneratedRegex(@"\bxox[abposr]-[A-Za-z0-9-]{10,}", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex SlackToken();

    [GeneratedRegex(@"https://hooks\.slack\.com/services/T[A-Za-z0-9]+/B[A-Za-z0-9]+/[A-Za-z0-9]+", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex SlackWebhook();

    [GeneratedRegex(@"\b[sr]k_live_[A-Za-z0-9]{16,}", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex StripeLiveKey();

    [GeneratedRegex(@"\bAIza[0-9A-Za-z_\-]{35}", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex GoogleApiKey();

    [GeneratedRegex(@"-----BEGIN (?:RSA |EC |DSA |OPENSSH |ENCRYPTED |PGP )?PRIVATE KEY(?: BLOCK)?-----", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex PrivateKey();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_\-]{5,}\.eyJ[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{10,}", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex JsonWebToken();

    [GeneratedRegex(@"AccountKey=[A-Za-z0-9+/]{20,}={0,2}", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex AzureAccountKey();

    [GeneratedRegex(@"SharedAccessKey=[A-Za-z0-9+/]{20,}={0,2}", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex AzureSharedAccessKey();

    [GeneratedRegex(@"\bsk-ant-[A-Za-z0-9_\-]{20,}", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex AnthropicKey();

    [GeneratedRegex(@"\bsk-proj-[A-Za-z0-9_\-]{20,}", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex OpenAiKey();
}
