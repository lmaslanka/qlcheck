// Copyright (c) qlcheck contributors.
using Qlcheck.Checks;

namespace Qlcheck.Languages.Config;

internal sealed class ConfigCheck(string id, string message) : ICheck
{
    public string Id { get; } = id;

    public string Message { get; } = message;

    public string Language => ConfigLanguage.LanguageId;
}
