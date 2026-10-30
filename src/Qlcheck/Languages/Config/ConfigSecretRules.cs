// Copyright (c) qlcheck contributors.
using Qlcheck.Languages.Config.Readers;
using Qlcheck.Secrets;

namespace Qlcheck.Languages.Config;

// Classifies a configuration entry with the shared rules and maps its kind to a config Check. The
// name is the last section of the key path, and a `Jwt:Key`-style path is a signing-key slot.
internal static class ConfigSecretRules
{
    private const string HardcodedSecret = "config-hardcoded-secret";

    private const string HardcodedCredential = "config-hardcoded-credential";

    private const string DatabasePassword = "config-database-password";

    private const string HardcodedJwtSecret = "config-hardcoded-jwt-secret";

    public static SecretHit? Classify(ConfigEntry entry, bool isTestPath) =>
        SecretRules.Classify(
            name: null,
            entry.KeyPath,
            entry.Value,
            new SecretContext(isTestPath, IsConnectionStringSlot: false, IsSigningKeySlot: false));

    // A line of raw text, for a token outside any entry such as a private key block. Only a known
    // format counts here: without a key there is no name to go on.
    public static SecretHit? ClassifyLine(string line, bool isTestPath) =>
        SecretRules.Classify(null, null, line, new SecretContext(isTestPath, IsConnectionStringSlot: false, IsSigningKeySlot: false))
            is { Kind: SecretKind.Format } hit
            ? hit
            : null;

    public static string CheckFor(SecretKind kind) =>
        kind switch
        {
            SecretKind.JwtKey => HardcodedJwtSecret,
            SecretKind.ConnectionPassword => DatabasePassword,
            SecretKind.Password => HardcodedCredential,
            _ => HardcodedSecret,
        };
}
