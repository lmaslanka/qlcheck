// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// Where untrusted data entered the body, for the Finding's message: `userId` (parameter of public
// `DeleteUser`, line 6).
internal sealed record TaintSource(string Name, string Description, int Line);
