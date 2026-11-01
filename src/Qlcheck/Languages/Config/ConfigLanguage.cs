// Copyright (c) qlcheck contributors.
using Qlcheck.Checks;
using Qlcheck.Languages.Config.Readers;
using Qlcheck.Scan;
using Qlcheck.Secrets;

namespace Qlcheck.Languages.Config;

// Configuration files as a Language (ADR 0012). Settings JSON, XML config and YAML files are always
// claimed; dotenv files only when git tracks them, because an untracked one is where secrets belong.
// Each file is read into entries and classified by the shared secret rules, and a raw-text pass
// catches a known token outside any entry, such as a private key block.
internal sealed class ConfigLanguage : ILanguage
{
    internal const string LanguageId = "config";

    private const string SettingsPrefix = "appsettings";

    private const string JsonExtension = ".json";

    private const string ConfigExtension = ".config";

    private const string YmlExtension = ".yml";

    private const string YamlExtension = ".yaml";

    private const string EnvFile = ".env";

    private const string EnvPrefix = ".env.";

    private readonly Dictionary<string, IReadOnlySet<string>> _tracked = new(StringComparer.Ordinal);

    public string Id => LanguageId;

    public IReadOnlyList<ICheck> Checks => ConfigCatalog.Checks;

    public bool Matches(string path)
    {
        var name = Path.GetFileName(path);
        return FormatOf(name) switch
        {
            Format.DotEnv => IsTracked(path),
            Format.None => false,
            _ => true,
        };
    }

    public RunResult Execute(IReadOnlyList<SourceScan.LoadedSource> files, IReadOnlyList<ICheck> checks, RunInputs inputs)
    {
        var selected = checks.OfType<ConfigCheck>().ToDictionary(check => check.Id, check => check.Message, StringComparer.Ordinal);
        var findings =
            from source in files
            from found in Hits(source)
            let id = ConfigSecretRules.CheckFor(found.Hit.Kind)
            where selected.ContainsKey(id)
            select Finding.At(id, source.File.Path, found.Line, found.Column, Message(selected[id], found.Hit));
        return new RunResult(findings.ToList(), []);
    }

    private static string Message(string message, SecretHit hit) => hit.Detail.Length == 0 ? message : $"{message} {hit.Detail}";

    // One hit per entry, then one per line for a token no entry on that line already reported.
    private static List<(SecretHit Hit, int Line, int Column)> Hits(SourceScan.LoadedSource source)
    {
        var isTest = TestPaths.IsTest(source.File.Path);
        var hits = Read(source)
            .Select(entry => (Hit: ConfigSecretRules.Classify(entry, isTest), entry.Line, entry.Column))
            .Where(found => found.Hit is not null)
            .Select(found => (found.Hit!, found.Line, found.Column))
            .ToList();

        var lines = source.File.Text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var number = index + 1;
            if (hits.All(found => found.Line != number) && ConfigSecretRules.ClassifyLine(lines[index], isTest) is { } hit)
            {
                hits.Add((hit, number, lines[index].Length - lines[index].TrimStart().Length + 1));
            }
        }

        return hits;
    }

    private static IReadOnlyList<ConfigEntry> Read(SourceScan.LoadedSource source) =>
        FormatOf(Path.GetFileName(source.FullPath)) switch
        {
            Format.Json => JsonSettingsReader.Read(source.File.Text),
            Format.Xml => XmlConfigReader.Read(source.File.Text),
            Format.Yaml => YamlReader.Read(source.File.Text),
            Format.DotEnv => DotEnvReader.Read(source.File.Text),
            _ => [],
        };

    private static Format FormatOf(string name)
    {
        if (name.StartsWith(SettingsPrefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Format.Json;
        }

        if (name.EndsWith(ConfigExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Format.Xml;
        }

        if (name.EndsWith(YmlExtension, StringComparison.OrdinalIgnoreCase) || name.EndsWith(YamlExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Format.Yaml;
        }

        return name.Equals(EnvFile, StringComparison.OrdinalIgnoreCase) || name.StartsWith(EnvPrefix, StringComparison.OrdinalIgnoreCase)
            ? Format.DotEnv
            : Format.None;
    }

    // One `git ls-files` per git root per run. Outside a repository, or without git, nothing is tracked.
    private bool IsTracked(string path)
    {
        var full = Path.GetFullPath(path);
        var root = RepoRoot.GitRoot(Path.GetDirectoryName(full) ?? full);
        if (root is null)
        {
            return false;
        }

        if (!_tracked.TryGetValue(root, out var tracked))
        {
            tracked = GitChanges.Tracked(root);
            _tracked[root] = tracked;
        }

        return tracked.Contains(full);
    }

    private enum Format
    {
        None,
        Json,
        Xml,
        Yaml,
        DotEnv,
    }
}
