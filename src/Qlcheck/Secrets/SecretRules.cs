// Copyright (c) qlcheck contributors.
namespace Qlcheck.Secrets;

// Classifies one hard-coded value for every Language. The kinds are tried in priority order, so a
// value gets at most one hit and only one Check reports it. On test paths only the kinds that do
// not depend on a name fire.
internal static class SecretRules
{
    public static SecretHit? Classify(string? name, string? keyPath, string value, SecretContext context)
    {
        name ??= SecretNames.LastSegment(keyPath);
        if ((context.IsSigningKeySlot || SecretNames.IsJwtKeyPath(keyPath)) && IsKeyMaterial(value))
        {
            return new SecretHit(SecretKind.JwtKey, string.Empty);
        }

        if (context.IsConnectionStringSlot ? !SecretValues.IsNonSecret(value, name) : ConnectionStrings.HasPassword(value))
        {
            return new SecretHit(SecretKind.ConnectionPassword, string.Empty);
        }

        if (SecretFormats.ProviderOf(value) is { } provider)
        {
            return new SecretHit(SecretKind.Format, $"It looks like a {provider}.");
        }

        if (context.IsTestPath || SecretValues.IsNonSecret(value, name))
        {
            return null;
        }

        return SecretNames.Classify(name) switch
        {
            SecretNameKind.Password => new SecretHit(SecretKind.Password, string.Empty),
            SecretNameKind.Secret when SecretValues.LooksRandom(value) => new SecretHit(SecretKind.NamedSecret, string.Empty),
            _ => null,
        };
    }

    // Whatever sits in a signing-key slot is the key, even a readable phrase, unless it is a
    // placeholder or names where the key really lives.
    private static bool IsKeyMaterial(string value) => !SecretValues.IsPlaceholder(value) && !SecretValues.IsKeyName(value);
}
