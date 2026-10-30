// Copyright (c) qlcheck contributors.
namespace Qlcheck.Secrets;

// What the calling Language worked out from its own syntax about where a value sits.
internal readonly record struct SecretContext(bool IsTestPath, bool IsConnectionStringSlot, bool IsSigningKeySlot);
