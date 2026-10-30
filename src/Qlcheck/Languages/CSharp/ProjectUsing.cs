// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp;

// One `<Using>` item from a project file: `Include` adds a global using, `Remove` drops one added
// earlier (by the SDK defaults or a Directory.Build.props).
internal sealed record ProjectUsing(string Name, bool Remove, string? Alias, bool Static);
