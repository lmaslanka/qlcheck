// Copyright (c) qlcheck contributors.
namespace Qlcheck.Secrets;

// What a hard-coded value is, in priority order: one value gets at most one kind.
internal enum SecretKind
{
    JwtKey,
    ConnectionPassword,
    Format,
    Password,
    NamedSecret,
}
