# Configuration files are a Language; Checks are discovered per Language

Refines 0005. .NET projects keep secrets in `appsettings*.json`, `*.config`, YAML and `.env` files
as often as in code, so qlcheck scans those files too. We made them a second Language, `config`,
rather than adding a cross-language text scan, which would break 0005's rule that a Check runs only
on files of its own Language.

- **Discovery goes through Languages.** `ILanguage` lists its Checks and `ICheck` says whether it is
  implemented. `CheckDiscovery` collects ids from every Language and throws if two Languages claim
  one, so `--check`, `.qlcheck_disable`, suppression comments and `--stats` work for config ids with
  no further change. Run no longer refers to the C# catalog.
- **Ids are globally unique, with a `config-` prefix.** The four config Checks
  (`config-hardcoded-secret`, `config-hardcoded-credential`, `config-database-password`,
  `config-hardcoded-jwt-secret`) mirror the four C# secret Checks. Users will put these ids in
  `.qlcheck_disable` files and suppression comments, which is why this is hard to reverse.
- **One shared classifier.** `Qlcheck.Secrets` holds every string-only rule (token formats, names,
  placeholders, connection strings, priority) and depends on nothing else in qlcheck and on no
  Roslyn type. Each Language works out names and slots from its own syntax and passes them in.
  Layering rules L7–L9 keep it that way and keep the two Languages apart.
- **`.env*` only when git tracks it.** An untracked `.env` is where secrets belong, so flagging it
  would be noise. The config Language asks `GitChanges.Tracked` (one `git ls-files` per git root per
  run, in the sanctioned process file). Outside git, or without git, `.env` files are not claimed,
  not loaded and not counted.
- **Readers are in process, with no new dependency.** JSON uses `Utf8JsonReader` with comments and
  trailing commas, XML refuses DTDs and resolves nothing, and YAML is a minimal line-based reader
  that skips anchors, aliases, flow collections and tags rather than misreading them. Adding a full
  YAML library is a dependency decision left for later.

The trade-off: a second Language means a second set of ids for the same four kinds of secret, and
a YAML reader that misses exotic syntax. In return each Language stays a plain `ILanguage`, and the
CLI, scan and dispatcher still never name one.
