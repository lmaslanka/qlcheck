# Plan 05: real secrets scanning

Status: Agreed 2026-10-29 · Depends on: 01 (placeholder cleanup); scheduled after 00 and 01

Probe (outside the repo): `/tmp/claude-1000/-home-lmaslanka-projects-qlcheck/1fadbd8a-84fa-480a-a869-298258cc64bb/scratchpad/probes-secrets/Probe.cs`.

## Goal

Make qlcheck find hard-coded secrets where .NET projects actually keep them, and tell the agent
how to fix the code. Each Finding also says that a human must rotate the secret. **A Finding
never repeats the secret value.** Placeholders, names that only look like secret names, and
values read from configuration or the environment stay quiet.

- **Phase 1:** the four C# Checks (`hardcoded-secret`, `hardcoded-credential`,
  `database-password`, `no-hardcoded-jwt-secret`) become real detectors over C# constants.
- **Phase 2:** a new `config` Language with its own four Checks for `appsettings*.json`,
  `*.config`, YAML and git-tracked `.env*` files. It reuses the same Roslyn-free secret
  classifier. Phase 2 is recorded in ADR 0012.

Out of scope: **git history scanning** (the user's decision). Only the working tree is read.

## Current state (verified at `3272d06`; line numbers may move after Plan 01)

**The four secret rows are placeholders.** Plan 01 leaves them for this plan. Each one is
`Row.Invoke`, which reports *any invocation* with the given method name
(`Match/InvokeMatch.cs:9-25`). None of them looks at a string:

| Check | Row | Fires on | Message (`checks.json`) |
|---|---|---|---|
| `database-password` | `SyntaxEngine.cs:43` `Row.Invoke(..., "Password")` | any `Password()` call | "A secure password should be used when connecting to a database." |
| `hardcoded-credential` | `SyntaxEngine.cs:77` `Row.Invoke(..., "Password")` | any `Password()` call (same predicate as above) | "Do not hard-code a credential." |
| `hardcoded-secret` | `SyntaxEngine.cs:79` `Row.Invoke(..., "Secret")` | any `Secret()` call | "Do not hard-code a secret." |
| `no-hardcoded-jwt-secret` | `SyntaxEngine.cs:144` `Row.Invoke(..., "SigningKey")` | any `SigningKey()` call | "JWT secret keys should not be disclosed." |

Their `fixtures.json` entries only prove the placeholders work: bad = `Password();` /
`Secret();` / `SigningKey();`, good = `return;`.

**Other credential-related rows:**

| Check | State | Owner |
|---|---|---|
| `strong-crypto-key` | Placeholder (`Row.Invoke "GenerateKey"`, `SymbolEngine.cs:173`). Concerns key *size*, not secrecy. | Plan 01, `01-placeholders/crypto.md` |
| `no-plaintext-password`, `password-salt` | Type-aware, but both run the same predicate: every `HashAlgorithm.ComputeHash` (`SymbolPatterns.cs:971-991`). That doubles the noise on every checksum, and neither looks at a password or a salt. | Follow-up (not this plan) |
| `jwt-strong-signature` | Real (type-resolved, `SymbolPatterns.cs:75-98`). It is about the algorithm, not a secret. | — |
| `connection-string-injection` | Real taint (`TaintEngine.cs:14`). It catches untrusted input in a connection string, not literals. | — |

**Probe result.** The probe holds a Stripe-format const, `"Server=db;User Id=sa;Password=Hunter2!;"`,
a `Password` property, a `ghp_` token, a `password:` named argument and a `["password"]`
dictionary entry. It produced **no secret Finding**. It also produced:

```
Probe.cs:9:44  magic-literal
  Magic string "Hunter2!" is used 2 times in this file; make it a named const.
```

`magic-literal` (`MagicLiteralPattern.cs:37-43`, via `LiteralContext.Label`) and
`magic-literal-shared` (`MagicLiteralSharedPattern.cs:40-66`) **put the secret into the Finding
message**. They also tell the agent to gather it into a const. This plan fixes both.

**Plumbing facts the design depends on:**
- `WalkContext.IsTestFile` = `path.Contains("Test", OrdinalIgnoreCase)` (`WalkContext.cs:29`).
- `ctx.Report(id, node, detail)` adds detail text after the catalog message (`WalkContext.cs:66-70`).
- `LiteralContext.TargetOf` / `ArgumentTarget` / `ParameterOf` (`Queries/LiteralContext.cs:48-130`)
  already resolve which field, property, parameter or indexer a literal feeds. They are private
  today.
- `Suppressions` matches `qlcheck-ignore:` anywhere on a line (`Run/Suppressions.cs:73-77`), so it
  does not depend on comment syntax. `#`, `//` and `<!-- -->` all work.
- `Finding.At(checkId, path, line, column, message)` (`Finding.cs`) already exists without Roslyn,
  so a non-Roslyn Language can use it.
- **Discovery is C#-only.** `CheckDiscovery.All()` = `Catalog.Checks` (`Run/CheckDiscovery.cs:14-15`).
  `RejectUnimplemented` / `DisabledError` test `check is CatalogCheck && !CatalogWalker.IsImplemented`
  (`CheckDiscovery.cs:50-56, 73-87`). `CatalogCheck.Language` is hard-wired to `csharp`
  (`CatalogCheck.cs:23`). Only `Languages\CSharp\Catalog\checks.json` is embedded (`Qlcheck.csproj:52`).
- The loader reads every file some `ILanguage.Matches` (`QlcheckApp.cs:32`), and those loaded files
  are what `--stats` counts as "Files checked" (`Report/Report.cs:135`). So a file a Language
  must skip has to be rejected in `Matches`, not later.
- `CONTEXT.md` already defines **Language** as "A kind of file qlcheck can parse and inspect: a
  programming language (C#) or a configuration format (JSON settings, `.env`, YAML, XML config)".
  Phase 2 relies on this and needs no glossary change for it.
- ADR 0009 layering (`tests/Qlcheck.ArchitectureTests/Rules/LayeringTests.cs`):
  - L4: Scan depends on nothing else in qlcheck.
  - L5: `Languages/CSharp/Catalog/` must not use Run/Report/Cli.
  - L6: only `Scan/GitChanges.cs` and `CoverageRunner.cs` start processes.
- qlcheck's own source has false-positive bait:
  `private const string JwtSecurityTokenHandlerTypeName = "JwtSecurityTokenHandler";`
  (`SymbolPatterns.cs:64`). The repo has no real token-format strings and no
  `appsettings*.json` / `*.config` / YAML / `.env` files today (grep checked).

## Design

### The shared classifier: `src/Qlcheck/Secrets/` (Roslyn-free)

All string-only rules live in one namespace, `Qlcheck.Secrets`. Both Languages use it, and it
depends on nothing else in qlcheck and on no Roslyn type. It goes here (not under
`Languages/CSharp`) **from Phase 1 on**, so Phase 2 reuses it without a move. Two new ADR 0009
rules keep it that way (step 2.1).

| File | Does |
|---|---|
| `SecretFormats.cs` | Known token formats → provider name (`[GeneratedRegex]` with a match timeout, as in `Suppressions.cs`, so `regex-timeout` stays quiet). |
| `SecretNames.cs` | Splits names into words (camel case, `_`, `-`, `:`, `.`, `__`) and returns `Password` / `Secret` / `Jwt` / none. Holds the list of non-secret last words. |
| `SecretValues.cs` | `IsPlaceholder`, `LooksLikeIdentifier`, `Entropy`, `LooksRandom` (the gate for secret-named values). |
| `ConnectionStrings.cs` | Parses `key=value;` strings and DB-scheme URIs. `HasPassword(value)`. |
| `SecretKind.cs` | `enum SecretKind { JwtKey, ConnectionPassword, Format, Password, NamedSecret }`, plus `record SecretHit(SecretKind Kind, string Detail)`. The detail never holds the value. |
| `SecretRules.cs` | `Classify(string? name, string? keyPath, string value, SecretContext context) → SecretHit?` applies the priority order below. `SecretContext` carries `IsTestPath`, `IsConnectionStringSlot` and `IsSigningKeySlot`, which each Language works out from its own syntax. |

**Priority: one hit per value, and each Check reports only its own kind:**

| # | Kind | C# Check | Config Check | Rule |
|---|---|---|---|---|
| 1 | `JwtKey` | `no-hardcoded-jwt-secret` | `config-hardcoded-jwt-secret` | The value sits in a signing-key slot. C#: it flows into `SymmetricSecurityKey`. Config: a key path whose segments include `Jwt`/`JwtSettings`/`JwtBearer`/`Bearer` and end in `Key`/`Secret`/`SigningKey`/`IssuerSigningKey`. |
| 2 | `ConnectionPassword` | `database-password` | `config-database-password` | `ConnectionStrings.HasPassword`: ≥2 `k=v` pairs, at least one of `server`/`data source`/`host`/`database`/`initial catalog`/`user id`/`uid`/`username`, and `password`/`pwd` with a non-placeholder value. Also a DB-scheme URI (`postgres(ql)`, `mysql`, `mongodb(+srv)`, `redis(s)`, `amqp(s)`, `sqlserver`) with `user:pass@`. Empty passwords are **not** flagged (D8). |
| 3 | `Format` | `hardcoded-secret` | `config-hardcoded-secret` | A known token format anywhere in the value, no name needed. Detail: "It looks like a <provider>." |
| 4 | `Password` | `hardcoded-credential` | `config-hardcoded-credential` | Password-like name (`password`, `passwd`, `pwd`, `passphrase`, `credential(s)`) and a non-placeholder value. No entropy gate. Skipped on test paths (D5). |
| 5 | `NamedSecret` | `hardcoded-secret` | `config-hardcoded-secret` | Secret-like name (`secret`, `token`, `apikey`/`api key`, `accesskey`, `privatekey`, `clientsecret`, `signingkey`, `sas`, `bearer`) and `LooksRandom` (≥16 chars, no whitespace, ≥2 character classes, Shannon entropy ≥3.0 bits/char). Skipped on test paths (D5). |

**Known formats (D3):**
- AWS `(AKIA|ASIA)[0-9A-Z]{16}`
- GitHub `gh[pousr]_[A-Za-z0-9]{36}` and `github_pat_[A-Za-z0-9_]{82}`
- Slack `xox[abposr]-…` and `https://hooks.slack.com/services/T…/B…/…`
- Stripe live keys only: `sk_live_` / `rk_live_` (not `sk_test_`)
- Google `AIza[0-9A-Za-z_-]{35}`
- PEM `-----BEGIN (RSA |EC |DSA |OPENSSH |ENCRYPTED |PGP )?PRIVATE KEY( BLOCK)?-----`
- JWT `eyJ….eyJ….<sig>`
- Azure `AccountKey=` / `SharedAccessKey=` inside a connection string
- Anthropic `sk-ant-…` and OpenAI `sk-proj-…`

**Non-secret last words in a name**: `Length`, `Policy`, `Hash`, `Salt`, `Regex`,
`Pattern`, `Field`, `Label`, `Prompt`, `Hint`, `Name`, `TypeName`, `Header`, `Path`, `File`,
`Url`, `Endpoint`, `Type`, `Id`, `Expiry`/`Expiration`/`Lifetime`/`Ttl`, `Count`, `Min`/`Max`,
`Required`, `Reset`, `Validator`, `Error`, `Message`, `Placeholder`, `Format`, `Section`,
`Variable`/`EnvVar`, `Column`, `Claim`, `Scheme`, `Provider`, `Handler`, `Options`. A `Key` last
word counts as non-secret when the earlier words already mark a resource or config key. This keeps
`PasswordPolicy`, `PasswordLength`, `TokenTypeName`, `ApiKeyHeader` and `PasswordResetUrl` quiet.

**Values that never count as secrets (`SecretValues.IsPlaceholder` and friends):**
- empty or whitespace
- `changeme`, `change-me`, `password`, `secret`, `example`, `dummy`, `sample`, `test`, `none`,
  `null`, `todo`, `replace_me`, `your-…-here`
- runs of one character (`xxx`, `***`, `000`)
- `<…>`, `{…}`, `${…}` (which covers `${{ secrets.X }}` and `${VAR:-default}`), `$(…)`, `%…%`,
  `#{…}#`, `__X__`
- `@Microsoft.KeyVault(…)` references
- a value that contains `EXAMPLE`, `FAKE` or `DUMMY` (so AWS's documented `AKIAIOSFODNN7EXAMPLE`
  is allowed)
- a value that looks like an identifier: one Pascal/camel/snake word without digits, or the name
  itself (`"JwtSecurityTokenHandler"`, `const string Password = "Password"`)
- an ALL_CAPS env-var name (`"DB_PASSWORD"`)
- a dotted or `:`-sectioned key (`"ConnectionStrings:Default"`)
- a URL without userinfo

**Values read from configuration or the environment are the fix.** In C# they are not
constants, so the classifier never sees them.

**Test paths (D5):** a test path is one that contains `Test`, the same rule as today's
`WalkContext.IsTestFile`. To share that rule, move it to a tiny `Scan/TestPaths.IsTest(path)`
(Scan may be used by every layer, L4) and have `WalkContext` call it. On test paths, kinds 1–3
still fire and kinds 4–5 are skipped.

### Messages (D6; same text pattern for both Languages)

Every message tells the agent four things:
- where the value should come from instead (outside source control: user-secrets or the
  environment, not `appsettings.json`)
- that the user must rotate it
- that the agent cannot rotate it itself
- how to suppress the Finding if the user confirms the value is not a real secret

Details name the kind or provider, never the value. **No `replacement` is ever attached.**

C# (`Languages/CSharp/Catalog/checks.json`, ids unchanged, D7):

- `hardcoded-credential`: "Do not hard-code a password. Read it from configuration kept outside
  source control: `dotnet user-secrets` in development, an environment variable or secret store in
  production. Do not move it into `appsettings.json`. The value is already in source control, so
  tell the user it must be rotated. You cannot rotate it yourself. If the user confirms it is not
  a real secret, add `// qlcheck-ignore: hardcoded-credential checked-on:<YYYY-MM-DD>` on this line
  or the line above."
- `hardcoded-secret`: "Do not hard-code a secret such as an API key, token or private key. Read it
  from configuration kept outside source control: `dotnet user-secrets` in development, an
  environment variable or secret store in production. Do not move it into `appsettings.json`. The
  value is already in source control, so tell the user it must be revoked and rotated. You cannot
  do that yourself. If the user confirms it is not a real secret, add `// qlcheck-ignore:
  hardcoded-secret checked-on:<YYYY-MM-DD>` on this line or the line above." The detail adds
  "It looks like a <provider>." when the format is known.
- `database-password`: "Do not put a password in a connection string literal. Keep the connection
  string outside source control (user-secrets, or the environment variable
  `ConnectionStrings__<Name>`) and read it with `GetConnectionString`. Tell the user the database
  password must be rotated. You cannot rotate it yourself. If the user confirms it is not a real
  password, add `// qlcheck-ignore: database-password checked-on:<YYYY-MM-DD>` on this line or the
  line above."
- `no-hardcoded-jwt-secret`: "Do not hard-code a JWT signing key. Load the key bytes from
  configuration kept outside source control or from a key vault. Anyone with this value can forge
  tokens, so tell the user the key must be rotated. You cannot rotate it yourself. If the user
  confirms it is not a real key, add `// qlcheck-ignore: no-hardcoded-jwt-secret
  checked-on:<YYYY-MM-DD>` on this line or the line above."

Config (`Languages/Config/checks.json`, new ids). The suppression clause in each one reads: "…add a
`qlcheck-ignore: <id> checked-on:<YYYY-MM-DD>` comment on this line or the line above (`//` in
JSON, `#` in `.env` and YAML, `<!-- -->` in XML)."

- `config-hardcoded-credential`: "Do not commit a password in a configuration file. Remove the
  value from this file (leave it empty or delete the key) and supply it at run time: `dotnet
  user-secrets` in development, an environment variable (`Section__Key`) or a secret store in
  production. The value is already in source control, so tell the user it must be rotated. You
  cannot rotate it yourself. If the user confirms it is not a real secret, …"
- `config-hardcoded-secret`: "Do not commit a secret such as an API key, token or private key in a
  configuration file. Remove the value from this file and supply it at run time: `dotnet
  user-secrets` in development, an environment variable (`Section__Key`) or a secret store in
  production. The value is already in source control, so tell the user it must be revoked and
  rotated. You cannot do that yourself. If the user confirms it is not a real secret, …"
  It gets the same provider detail as the C# Check.
- `config-database-password`: "Do not commit a connection string that contains a password.
  Remove the password from this file and supply the connection string at run time (user-secrets,
  or the environment variable `ConnectionStrings__<Name>`). Tell the user the database password
  must be rotated. You cannot rotate it yourself. If the user confirms it is not a real password, …"
- `config-hardcoded-jwt-secret`: "Do not commit a JWT signing key in a configuration file. Remove
  it from this file and supply it at run time from user-secrets, an environment variable or a key
  vault. Anyone with this value can forge tokens, so tell the user the key must be rotated. You
  cannot rotate it yourself. If the user confirms it is not a real key, …"

### Phase 1: C# detectors

`Languages/CSharp/Catalog/SecretPatterns.cs` holds the Roslyn side. It collects constants,
resolves names and slots, and calls `SecretRules.Classify`.

- **Which constants are checked:**
  - string literals, including verbatim, raw and `u8` literals
  - constant-folded `"a" + "b"` concatenations and constant interpolated strings (via
    `ctx.Model.GetConstantValue`). Only the outermost constant expression is classified.
  - the text parts of non-constant interpolated strings, for kinds 2–3
- **Where a name comes from (kinds 4–5):**
  1. a field, const or local declarator
  2. a property initializer, an expression body, or a getter that returns a constant
  3. an assignment or object-initializer member `x.Password = "…"`
  4. a named argument `password: "…"`
  5. a positional argument whose **resolved parameter** is password-like
     (`new NetworkCredential("u", "…")`). This reuses `LiteralContext`'s resolution, made
     `internal`.
  6. a dictionary or indexer entry: `["ApiKey"] = "…"`, `{ "password", "…" }`,
     `.Add("password", "…")`, `new KeyValuePair<,>("password", "…")`. The key's last segment
     after `:` or `__` is the name.
  7. an anonymous-object member
  8. a default parameter value
  9. a comparison `password == "…"` (a hard-coded backdoor check)
  10. `return "…"` inside a method or property whose name matches
- **Slots:**
  - `IsSigningKeySlot`: the argument of `new SymmetricSecurityKey(X)`, resolved by type name with
    `Symbols`. X is `Encoding.*.GetBytes(<const>)`, `Convert.FromBase64String(<const>)`, a
    constant byte array or collection expression, or a const / static-readonly field initialised
    with one of those. An in-memory config key `Jwt:Key` / `Jwt:Secret` / `Jwt:SigningKey` also
    counts.
  - `IsConnectionStringSlot`: `Password = "…"` on a receiver whose type name ends in
    `ConnectionStringBuilder`.
- **Where the Finding points:** the constant expression, so a suppression comment works as it
  does today.
- **The four rows move from `SyntaxEngine` to `SymbolEngine`** as
  `Row.Handle(id, CheckClass.Symbol, SecretPatterns.<Kind>)` (D10), because they use the semantic
  model. Class-count delta: **Syntax −4, Symbol +4.** The C# catalog total is unchanged (D7).
- **`magic-literal` echo fix (D9):** `LiteralContext.IsStringCandidate` (`:75`) skips any literal
  that the classifier reports, or that feeds a password-named target. `magic-literal` and
  `magic-literal-shared` then neither print it nor say "make it a named const".

### Phase 2: the `config` Language (ADR 0012)

**A per-Language catalog seam** (this removes C#-only discovery):
- `ICheck` gets `bool IsImplemented => true;`. `CatalogCheck` overrides it with
  `CatalogWalker.IsImplemented(Id)`.
- `ILanguage` gets `IReadOnlyList<ICheck> Checks { get; }`. `CSharpLanguage` returns
  `Catalog.Checks`, and `ConfigLanguage` returns `ConfigCatalog.Checks`.
- `CheckDiscovery.All()` becomes `LanguageDiscovery.All().SelectMany(l => l.Checks)`, ordered
  by id. It throws on a duplicate id, because ids stay globally unique across Languages (ADR
  0005). `RejectUnimplemented` / `DisabledError` use `check.IsImplemented`, so
  `using Qlcheck.Languages.CSharp.Catalog` leaves `Run/CheckDiscovery.cs`.
- `--check`, `.qlcheck_disable` validation, the `"disabled"` JSON output and `--stats` then
  cover config ids with no further change.

**Files claimed by `ConfigLanguage.Matches(path)` (D2):**
- `appsettings*.json` (case-insensitive file name): always
- `*.config` (`web.config`, `app.config`, `*.exe.config`, `NuGet.config`, which catches
  `ClearTextPassword`): always
- `*.yml` / `*.yaml` (including `docker-compose*.yml` and workflows): always
- `.env` and `.env.*`: **only if git tracks the file**. `Matches` asks
  `ConfigLanguage.IsTracked(path)`, which caches one `GitChanges.Tracked(gitRoot)` result per git
  root (`RepoRoot.GitRoot`). A `.env` outside a git repo, or anywhere when git is missing, is
  not claimed. So it is never loaded and never counted in "Files checked".
- `launchSettings.json` and other JSON are not claimed. `EnsureOneLanguage` stays satisfied,
  because no `.cs` overlap is possible.

**`Scan/GitChanges.Tracked(string gitRoot) → IReadOnlySet<string>`** runs `git -C <root> ls-files
-z` and returns full paths. It is the only new process call, and it lives in the sanctioned file
(L6 unchanged). Unlike `Uncommitted`, it **does not throw**: when git is missing or the folder
is not a repository it returns an empty set, through a `TryRun` helper beside the existing `Run`.

**Readers** (`src/Qlcheck/Languages/Config/Readers/`, in-process, no new package). Each one
yields `ConfigEntry(string KeyPath, string Value, int Line, int Column, bool IsConnectionString)`
with 1-based positions at the start of the value. A file that fails to parse yields no entries
and no crash (a tested choice; malformed config is some other tool's Finding).

| Reader | Format | How |
|---|---|---|
| `JsonSettingsReader` | `appsettings*.json` | `Utf8JsonReader` with `CommentHandling = Skip` and `AllowTrailingCommas` (JSONC, as .NET config reads it). It keeps a property-name stack joined with `:`, uses array indices as segments like .NET config, and emits only string values. Byte offset (`TokenStartIndex`) is turned into line and character column with a line-start table. A UTF-8 BOM is skipped. `IsConnectionString` = the path starts with `ConnectionStrings:`. |
| `XmlConfigReader` | `*.config` | `XmlReader` with `DtdProcessing.Prohibit`, `XmlResolver = null` and `IXmlLineInfo`. `<add key="K" value="V"/>` → `(K, V)`. `<connectionStrings><add name="N" connectionString="V"/>` → `("ConnectionStrings:N", V, IsConnectionString)`. Any other attribute → `(element:attribute, value)`. Element text → `(element, text)`. The position is the attribute's line and column. |
| `DotEnvReader` | `.env*` | Line-based: skip blank and `#` lines, optional `export `, `KEY=VALUE`, matching quotes stripped, ` #` trailing comment stripped when unquoted. The position is the start of the value. |
| `YamlReader` | `*.yml` / `*.yaml` | **Minimal, line-based, no dependency.** It keeps an indent stack of `key:` segments joined with `:` and reads `key: value` scalars (quotes and ` #` comments stripped), list items `- KEY=VALUE` (docker-compose `environment:`) and `- key: value`, block scalars `|`/`>` (value = joined lines, position = first content line), and `---` document breaks. Anchors and aliases, flow mappings `{…}` and tags are skipped, not parsed. The limits go in the file's header comment. |

There is also a **raw-text pass** for PEM headers (multi-line keys inside any format). It reports
the header line as kind `Format`, unless an entry already produced a hit on that line.

**Classification:** `ConfigSecretRules.Classify(ConfigEntry, isTestPath)` calls
`SecretRules.Classify(name: last segment of KeyPath split on ':'/'__', keyPath, value,
context)`. `IsSigningKeySlot` and `IsConnectionStringSlot` come from the key path and from
`IsConnectionString`.

**Files:**
- `src/Qlcheck/Languages/Config/ConfigLanguage.cs`: `Id = "config"`, `Matches`, `Execute` (read,
  classify, report each hit whose kind maps to a selected Check, via `Finding.At(id, path, line,
  column, message)`)
- `src/Qlcheck/Languages/Config/ConfigCatalog.cs`: loads the embedded
  `Qlcheck.Languages.Config.checks.json`, in the same `{id, message}`-only shape as the C# catalog
- `src/Qlcheck/Languages/Config/ConfigCheck.cs`: `ICheck` with `Language => "config"`, enabled by
  default
- `src/Qlcheck/Languages/Config/ConfigSecretRules.cs`
- `src/Qlcheck/Languages/Config/checks.json`: the 4 rows
- `src/Qlcheck/Languages/Config/Readers/{ConfigEntry,JsonSettingsReader,XmlConfigReader,DotEnvReader,YamlReader}.cs`

**New ids (globally unique, D7 + ADR 0005):** `config-hardcoded-secret`,
`config-hardcoded-credential`, `config-database-password`, `config-hardcoded-jwt-secret`.
Discovered-count delta: **+4 checks, +4 enabled by default.**

**New layering rules (ADR 0009 mechanism, empty baselines)** in `LayeringTests.cs`:
- **L7** "Secrets depends on nothing else in qlcheck and on no Roslyn type". Rule:
  `ForbiddenUsings("src/Qlcheck/Secrets/", "Qlcheck", "Microsoft.CodeAnalysis")`.
- **L8** "Languages do not depend on each other". Rule: `Languages/Config/` must not use
  `Qlcheck.Languages.CSharp`, and `Languages/CSharp/` must not use `Qlcheck.Languages.Config`.
- **L9** "The config Language does not depend on Run, Report or Cli". This is the L5 analogue for
  `src/Qlcheck/Languages/Config/`.

### Test fixtures that don't trip secret scanners

GitHub push protection, gitleaks and trufflehog scan *every* committed file, `fixtures.json`,
test `.cs` files and this plan included.

1. **No committed file contains a whole, format-valid token.** In C# `fixtures.json` the `bad`
   source splits the token across a constant concatenation (`"gh" + "p_" + "<body>"`). Kind 3
   runs on the folded constant, so the Check still fires. This also tests constant folding.
2. **Config text can't concatenate**, so `tests/Qlcheck.Tests/Languages/Config/fixtures.json` uses
   markers such as `{{fake:github}}` / `{{fake:aws}}` / `{{fake:pem}}`. The config fixture harness
   replaces them at run time with `FakeSecrets` values.
3. `tests/Qlcheck.Tests/Support/FakeSecrets.cs` builds tokens at run time with
   `string.Concat(<split prefix>, <body>)`, e.g. `"AK" + "IA"`. Bodies are a fixed
   pseudo-random-looking alphabet walk with no `EXAMPLE`/`FAKE` marker (markers are allowlisted
   on purpose), each marked `// fake: not a real credential`. Unit tests insert them with
   `$$"""…{{token}}…"""`.
4. A **guard test** (`tests/Qlcheck.Tests/NoCommittedSecretsTests.cs`) runs
   `SecretFormats` over the raw bytes of every file under `src/`, `tests/` and `docs/`. It fails
   if any committed file holds a whole token.
5. Password and connection-string fixtures (`Password=Hunter2!`) match no provider pattern, so
   they are committed as plain text.
6. C# fixtures follow Plan 01's guard G1 (they must compile with zero errors). `SymmetricSecurityKey`
   (Microsoft.IdentityModel.Tokens) is declared as a small stub inside the fixture.
7. Dogfooding: `./run --human .` must stay clean. That covers `JwtSecurityTokenHandlerTypeName`,
   the format prefixes in `SecretFormats` (each is too short to match), and `regex-timeout`.

## Implementation steps (test-first, one commit each)

### Phase 1: C#

1.1 **Lock in the gap.** Add red tests in `SecretPatternsTests.cs` (via `MatchFixtures.Context`)
for each probe case. Add red tests that `magic-literal`/`magic-literal-shared` do not print a
password-named literal.

1.2 **`Qlcheck.Secrets` pure rules.** `SecretFormats`, `SecretNames`, `SecretValues`,
`ConnectionStrings`, `SecretKind`, `SecretRules`, with table-driven tests in
`tests/Qlcheck.Tests/Secrets/*Tests.cs`. Add `FakeSecrets` and the guard test in this commit.

1.3 **`Scan/TestPaths.IsTest`.** Move the rule out of `WalkContext.cs:29` and keep
`WalkContext.IsTestFile` calling it. No behaviour change. Covered by the existing `Row.Test` tests
plus one new unit test.

1.4 **Constant collection.** `SecretPatterns.Constants(ctx)`. Tests: folding, `u8`, raw and
verbatim strings, interpolation text parts, nested-literal de-duplication.

1.5 **Name and slot resolution.** Expose `LiteralContext`'s argument and parameter resolution as
`internal` (or move it to `Queries/Targets.cs`). Add `SecretPatterns.NameOf` for the 10 places,
plus the two slot detectors. One test per place. Test that literal keys passed to
`Configuration[…]` / `GetEnvironmentVariable` / `GetConnectionString` stay quiet.

1.6 **`hardcoded-credential`.** Add the row to `SymbolEngine` and remove `SyntaxEngine.cs:77`.
New fixture: `NetworkCredential` (bad); `PasswordLength` plus `config["Password"]` (good).
False-positive tests: placeholders, test path, env lookup.

1.7 **`database-password`.** Add the row (remove `:43`), plus connection-string, URI and builder
tests. With all four ids selected, `Password = "Server=…;Password=x"` must give exactly one
Finding.

1.8 **`hardcoded-secret`.** Add the row (remove `:79`), the split-constant fixture, the provider
detail, and the name + entropy gate. Test that the test-path policy keeps kind 3 and drops kind 5.

1.9 **`no-hardcoded-jwt-secret`.** Add the row (remove `:144`), the `SymmetricSecurityKey` stub
fixture and the `Jwt:*` in-memory key.

1.10 **Messages.** Update the 4 C# messages. Add a cross-kind test: the value is never a substring
of the message, and the replacement is null.

1.11 **Stop the echo.** Make `LiteralContext.IsStringCandidate` skip secret values, so the 1.1
magic-literal tests turn green.

1.12 **Docs and counts.** Apply the class delta in README (Syntax −4, Symbol +4, total
unchanged) and update the README `hardcoded-secret` example message. `dotnet test` and
`./run --human .` must both be clean.

### Phase 2: config Language

2.1 **Layering first.** Add L7, L8 and L9 to `LayeringTests.cs` with empty baselines. They should
pass already, since Phase 1 put `Secrets/` in place.

2.2 **Catalog seam, no new Language yet.** Add `ICheck.IsImplemented`, `ILanguage.Checks`,
`CheckDiscovery.All()` over Languages, and the duplicate-id throw. Update `RecordingLanguage` in
`CheckRunTests.cs`, `CheckDiscoveryTests` and `LanguageDiscoveryTests`. Expected: C# results
are byte-for-byte unchanged and every count test still passes.

2.3 **`GitChanges.Tracked`.** Tests in `QlcheckAppTests` style, reusing its `Git(...)` temp-repo
helper: tracked vs untracked vs ignored file, and a non-repo folder returns empty without
throwing.

2.4 **Readers, one commit each:** `DotEnvReader`, `JsonSettingsReader`, `XmlConfigReader`,
`YamlReader`. Each `tests/Qlcheck.Tests/Languages/Config/Readers/<Reader>Tests.cs` asserts key
paths, values and exact line/column, including comments, quotes, nesting, arrays, a BOM, CRLF and
a malformed file (no entries, no throw). The XML tests include a DTD/entity document, which must
be refused.

2.5 **`ConfigLanguage` + `ConfigCatalog` + `checks.json` + csproj `EmbeddedResource`.**
`ConfigLanguageTests`:
- the `Matches` table (incl. `appsettings.Production.json`, `NuGet.config`,
  `docker-compose.override.yml`, a `.env` in and out of git, `launchSettings.json` not claimed)
- each kind per reader
- one hit per value
- the PEM raw-text pass
- the test-path split

Update the count tests by **+4 discovered / +4 enabled**.

2.6 **Config fixtures harness.** `tests/Qlcheck.Tests/Languages/Config/fixtures.json`
(`bad`/`good`/`path` per id, with `{{fake:*}}` markers) and
`Each_config_check_has_a_failing_and_clean_fixture`. The fixture files are written to a temp
directory under their `path` name and run through `QlcheckApp.Run(["--check", id, …])`, so the
`Matches` path is also covered.

2.7 **End-to-end.** One CLI test per format:
- a `// qlcheck-ignore` comment in `appsettings.json`, `#` in `.env`/YAML and `<!-- -->` in
  `web.config` each suppress the Finding
- `.qlcheck_disable` with `config-hardcoded-secret` disables it
- `--stats` counts config files

2.8 **ADR 0012 and docs.** Write `docs/adr/0012-config-files-are-a-language.md`. In README:
- add the `config` Language to "How it's built", the "What it checks" table (+4 checks) and the
  total
- note that `.env*` is scanned only when tracked

`CONTEXT.md` already has the new Language definition, so it needs no edit. Finish with
`dotnet test` and `./run --human .` both clean.

## Files touched

**Phase 1**
- New:
  - `src/Qlcheck/Secrets/{SecretFormats,SecretNames,SecretValues,ConnectionStrings,SecretKind,SecretRules}.cs`
  - `src/Qlcheck/Scan/TestPaths.cs`
  - `src/Qlcheck/Languages/CSharp/Catalog/SecretPatterns.cs`
- Changed:
  - `src/Qlcheck/Languages/CSharp/Catalog/SyntaxEngine.cs` (−4 rows)
  - `src/Qlcheck/Languages/CSharp/Catalog/SymbolEngine.cs` (+4 rows)
  - `src/Qlcheck/Languages/CSharp/Catalog/Queries/LiteralContext.cs`
  - `src/Qlcheck/Languages/CSharp/Catalog/WalkContext.cs`
  - `src/Qlcheck/Languages/CSharp/Catalog/checks.json` (4 messages)
- Tests:
  - new: `tests/Qlcheck.Tests/Secrets/*Tests.cs`,
    `tests/Qlcheck.Tests/Catalog/SecretPatternsTests.cs`,
    `tests/Qlcheck.Tests/Support/FakeSecrets.cs`,
    `tests/Qlcheck.Tests/NoCommittedSecretsTests.cs`
  - changed: `tests/Qlcheck.Tests/Catalog/fixtures.json` (4 entries),
    `MagicLiteralPatternTests.cs`, `MagicLiteralSharedPatternTests.cs`
- `README.md`

**Phase 2**
- New:
  - `src/Qlcheck/Languages/Config/{ConfigLanguage,ConfigCatalog,ConfigCheck,ConfigSecretRules}.cs`
  - `src/Qlcheck/Languages/Config/checks.json`
  - `src/Qlcheck/Languages/Config/Readers/*.cs`
- Changed:
  - `src/Qlcheck/Checks/ICheck.cs`
  - `src/Qlcheck/Languages/ILanguage.cs`
  - `src/Qlcheck/Languages/CSharp/CSharpLanguage.cs`
  - `src/Qlcheck/Languages/CSharp/Catalog/CatalogCheck.cs`
  - `src/Qlcheck/Run/CheckDiscovery.cs`
  - `src/Qlcheck/Scan/GitChanges.cs`
  - `src/Qlcheck/Qlcheck.csproj` (EmbeddedResource)
- Tests:
  - new: `tests/Qlcheck.Tests/Languages/Config/**` (reader tests, `ConfigLanguageTests`,
    `fixtures.json`, the fixture harness)
  - changed: `tests/Qlcheck.Tests/Run/{CheckRunTests,CheckDiscoveryTests,LanguageDiscoveryTests}.cs`,
    `tests/Qlcheck.Tests/QlcheckAppTests.cs`, `tests/Qlcheck.Tests/Catalog/CatalogTests.cs` (counts),
    `tests/Qlcheck.ArchitectureTests/Rules/LayeringTests.cs` (L7–L9)
- `docs/adr/0012-config-files-are-a-language.md`, `README.md`

## ADR needed?

- **Phase 1: no.** It is rows and predicates, as ADR 0006/0007 describe, with the ids unchanged.
  It is easy to reverse.
- **Phase 2: yes, ADR 0012, "Configuration files are a Language; Checks are discovered per
  Language".** It is hard to reverse, because users will put `config-*` ids in `.qlcheck_disable`
  and suppression comments. It is surprising, because a non-programming format is now a
  Language. And it involves a real trade-off: a new Language with duplicate per-Language ids,
  against one cross-language text scan that would break ADR 0005's "a Check runs only on files of
  its Language". It records:
  - `ILanguage.Checks` / `ICheck.IsImplemented` as the discovery seam
  - globally unique ids with a `config-` prefix
  - the shared Roslyn-free `Qlcheck.Secrets` module guarded by L7–L9
  - `.env*` claimed only when git-tracked, via the sanctioned `GitChanges`
  - the minimal YAML reader with no new dependency

  It refines ADR 0005 and does not replace it.

## Risks

- **False positives on names.** `token` is everywhere in C#. Mitigation: the entropy gate on
  kind 5, the identifier-like value filter, the non-secret last-word list and dogfooding.
- **False negatives by design.** A secret in a variable or key with no secret-like name and no
  known format is missed. The Check documents this rather than guessing.
- **`TestPaths` is substring-based**, so `Latest.cs` and `Attestation/` count as tests and lose
  kinds 4–5. This is unchanged behaviour, moved into one place. Possible follow-up: project-based
  test detection (`TestProjectFinder` exists).
- **The minimal YAML reader** misreads exotic YAML (anchors, flow style, multi-line keys). Those
  entries are skipped, not mis-reported. If coverage turns out too weak, adding YamlDotNet is a
  dependency decision for the user, not part of this plan.
- **`.env` tracking costs one `git ls-files` per git root per run.** Outside git or without git,
  `.env` is silently not scanned. ADR 0012 and the README say so.
- **Strict JSON consumers** of `appsettings.json` reject `//` suppression comments. .NET's
  configuration loader accepts them. The message offers the comment, and the user decides.
- **Other Checks may still repeat a secret**: `string-concat` replacements and other
  `ReportCustom` callers. Step 1.11 covers only magic literals. Possible follow-up: audit
  replacements for literal echo.
- **Push protection on fixtures** is covered by split constants, run-time markers and the guard
  test. The guard is only as good as `SecretFormats`.
- **Priority bugs** would give two Findings for one value. Cross-id tests cover this in both
  Languages.
- **Merge drift with Plan 01**, which edits the same engine files and counts. That is why this
  plan states deltas, not absolute numbers, and is scheduled after 01.

## Effort

- Phase 1: about 3–4 focused days. `Secrets` plus tests take about 1 day, name and slot
  resolution about 1 day, and the four detectors, messages, magic-literal fix and docs 1–2 days.
- Phase 2: about 4–5 days. The seam and git tracking take about 1 day, the four readers 2 days,
  and the Language, fixtures, end-to-end tests and ADR 1–2 days.

## Follow-ups (not in this plan)

- `no-plaintext-password` and `password-salt` need real predicates (a password-named input; a
  salt that is missing or constant) instead of the one shared `ComputeHash` predicate.
- Audit `replacement` text from every Check for literal echo.

## Decisions (resolved)

1. **Non-C# config files: option B, as Phase 2 of this plan.** A new `config` Language with its
   own ids, reusing the shared secret classifier, through a per-Language catalog seam. ADR 0012.
2. **Which config files:** `appsettings*.json`, `*.config` and YAML are always scanned. `.env*` is
   scanned only if git tracks the file.
3. **Known-format list:** AWS `AKIA`/`ASIA`, GitHub `gh[pousr]_`/`github_pat_`, Slack `xox?-`
   and Slack webhook URLs, Stripe live keys only (`sk_live_`/`rk_live_`), Google `AIza`, PEM
   private keys, JWTs, Azure `AccountKey=`/`SharedAccessKey=`, Anthropic `sk-ant-`, OpenAI
   `sk-proj-`.
4. **Entropy:** only as a gate for secret/token/key-named values (≥16 chars, ≥2 char classes,
   ≥3.0 bits/char). No entropy for passwords, and no standalone entropy detector.
5. **Test files:** format, JWT-key and connection-string detectors still fire. Name-based password
   and secret detectors are skipped.
6. **Messages:** as in "Messages". Each says to move the value outside source control (not into
   `appsettings.json`), to tell the user it must be rotated, and how to suppress it once the user
   confirms it is not a real secret.
7. **Ids:** keep all four C# ids with the kinds in the priority table. Nothing is renamed. Phase 2
   adds four `config-*` ids.
8. **Empty database passwords** are not flagged.
9. **The `magic-literal` / `magic-literal-shared` echo** is fixed in this plan (step 1.11).
10. **The four rows move from Syntax to Symbol:** class delta Syntax −4, Symbol +4. The absolute
    counts depend on Plan 01, which runs first.
