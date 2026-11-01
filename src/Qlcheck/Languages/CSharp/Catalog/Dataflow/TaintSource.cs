// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// Where untrusted data entered the body, for the Finding's message: `userId` (parameter of public
// `DeleteUser`, line 6). PublicParameter marks a parameter of a method visible outside the
// assembly rather than an entry point proper (a request, the console): each row decides whether
// those count (TaintSpec.PublicParameters).
internal sealed record TaintSource(string Name, string Description, int Line, bool PublicParameter = false);
