// Copyright (c) qlcheck contributors.
using Qlcheck.Checks;

namespace Qlcheck.Languages.CSharp.Catalog;

internal sealed class CatalogCheck : ICheck
{
    public CatalogCheck(string id, string message)
    {
        Id = id;
        Message = message;
    }

    public string Id { get; }

    public string Message { get; }

    public string Language => CSharpLanguage.LanguageId;

    public bool EnabledByDefault => CatalogWalker.IsImplemented(Id);
}
