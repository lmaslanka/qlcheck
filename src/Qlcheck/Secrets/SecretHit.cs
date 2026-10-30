// Copyright (c) qlcheck contributors.
namespace Qlcheck.Secrets;

// A classified value. Detail names the kind or provider for the Finding message and never holds
// the value itself.
internal sealed record SecretHit(SecretKind Kind, string Detail);
