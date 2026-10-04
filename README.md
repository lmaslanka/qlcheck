# qlcheck

**A static checker for C# that reports findings an AI coding agent can act on, not a pile of
lint noise for a human to triage.**

Every result has a stable ID, a clear fix instruction, and — where it's safe to automate — a
ready-made replacement. Security checks are semantic (type- and taint-aware via Roslyn's
compiler model), not regex guesses, so they catch real bugs and skip the obvious non-issues.

```
╭──────────────────────────────────────────────╮
│ QLCHECK                                      │
├──────────────────────────────────────────────┤
│ Scan                                         │
│   Files checked                            1 │
│   Lines checked                           26 │
│   Checks run                             501 │
│                                              │
│ Results                                      │
│   Findings                                 7 │
│   Files with findings                      1 │
│   Clean files                              0 │
│                                              │
│ By check                                     │
│   catch-only-rethrow             1  (1 file) │
│   field-private                  1  (1 file) │
│   magic-literal                  2  (1 file) │
│   no-general-exception           1  (1 file) │
│   no-public-field                1  (1 file) │
│   sql-injection                  1  (1 file) │
│                                              │
│ By file                                      │
│   Sample.cs                                7 │
│                                              │
│ Duration                              548 ms │
╰──────────────────────────────────────────────╯
```

## Why qlcheck

- **Built for agents first.** A `Check` emits `Finding`s with a stable ID
  (`{check}:{file}:{line}:{column}`), a plain-language fix instruction, and sometimes a
  drop-in `replacement`. No severity levels to argue about, no suppressing-by-config-file
  rituals — just "here's what's wrong and here's one way to fix it."
- **Semantic, not textual.** Security checks use Roslyn's semantic model to trace taint
  through parameters, assignments, and interpolation — `sql-injection` only fires on an actual
  `DbCommand`-derived receiver, not on any method named `Execute`.
- **Knows what it doesn't know.** Some findings genuinely need a human judgment call —
  `jwt-strong-signature` flags any signing call and tells the agent to *ask the user* whether
  the algorithm is strong enough, rather than silently guessing. Once a human has made that
  call, a dated suppression comment keeps the finding quiet for a year, then it resurfaces.
- **509 checks, zero config.** Covers naming, style, resource management, exceptions,
  concurrency, and ~20 taint-tracked injection classes (SQL, command, path, LDAP, XPath,
  log, SSRF, zip-slip, and more) out of the box.
- **Fast.** In-process Roslyn parse/analysis — no external process per file, no network call.

## Install

```bash
git clone git@github.com:lmaslanka/qlcheck.git
cd qlcheck
./install
```

`./install` publishes a release build to `~/.local/share/qlcheck` and symlinks the binary into
`~/.local/bin/qlcheck`. Make sure that directory is on your `PATH`, then run:

```bash
qlcheck .
```

Prefer to build it yourself, or just try it without installing anything:

```bash
dotnet build                      # build everything
dotnet run --project src/Qlcheck -- .   # run against the current directory
```

## Quick example

```csharp
// Sample.cs
public class UserRepository
{
    public int MaxRetries = 3;

    public void DeleteUser(DbCommand command, string userId)
    {
        command.ExecuteReader(userId);
    }

    public void Load(DbCommand command)
    {
        try
        {
            command.ExecuteReader("safe");
        }
        catch (InvalidOperationException)
        {
            throw new Exception("could not connect");
        }
    }
}
```

```bash
$ qlcheck --human Sample.cs
```

```
Sample.cs:12:9  sql-injection
  Do not build a database query from untrusted input.

Sample.cs:21:9  catch-only-rethrow
  `catch` clauses should do more than rethrow.

Sample.cs:23:13  no-general-exception
  General or reserved exceptions should never be thrown.

Sample.cs:8:5  field-private
  Fields should be private.

Sample.cs:8:5  no-public-field
  Fields should not have public accessibility.

Sample.cs:8:29  magic-literal
  Magic number 3 should be a named const or enum member.

Sample.cs:19:35  magic-literal
  Magic string "safe" should be a named const.
```

That `sql-injection` finding isn't a text match on `.Execute(...)` — it resolved `command`'s
actual type against `System.Data.Common.DbCommand` (or any subtype) and traced `userId` back
to a public method parameter before reporting anything.

## Output modes

qlcheck writes structured JSON to stdout by default — the format an agent (or any other
tool) consumes:

```bash
$ qlcheck Sample.cs
```

```json
{
  "findings": [
    {
      "id": "sql-injection:Sample.cs:12:9",
      "check": "sql-injection",
      "file": "Sample.cs",
      "line": 12,
      "column": 9,
      "message": "Do not build a database query from untrusted input."
    }
  ]
}
```

| Flag | Effect |
|---|---|
| *(none)* | JSON findings on stdout — the default, agent-consumable format |
| `--human` | Human-readable findings, one block per finding, with `replacement` text inlined |
| `--stats` | Appends the summary box shown above (files/lines/checks/findings, grouped by check and by file) |
| `--check <id>` | Run only the named check(s) (repeatable), ignoring the default/opt-in selection |
| `--coverage` | Opt in to the `coverage` check (runs your test suite via `dotnet test`/`coverlet` and reports under-covered methods) |
| `--help` / `-h` | Usage |

Exit codes: `0` clean, `1` findings reported, `2` a usage or environment error (e.g. no
project found for `--coverage`).

## What it checks

509 checks across these classes:

| Class | Checks | What it looks at |
|---|---|---|
| Syntax | 264 | Style, naming, structure — pure syntax tree shape |
| Symbol | 184 | Anything needing type/symbol info — accessibility, overloads, member shape |
| Taint | 27 | Dataflow from an untrusted source (a public parameter, `ReadLine`, etc.) to a sink |
| Metric | 14 | Size/complexity thresholds (method length, nesting, parameter count) |
| Flow | 11 | Control-flow shape (unreachable code, always-true conditions) |
| *(opt-in)* | 1 | `coverage` — test coverage gaps, via your own test run |

A sample of the taint-tracked injection checks — each requires untrusted data to actually
reach a real sink of that type, not just a suspicious method name:

| Check | Message |
|---|---|
| `sql-injection` | Do not build a database query from untrusted input. |
| `command-injection` | Do not build an OS command from untrusted input. |
| `path-injection` | Do not build a file path from untrusted input. |
| `log-injection` | Do not write untrusted input into a log message. |
| `xpath-injection` | Do not build an XPath expression from untrusted input. |
| `ssrf` | Do not request a URL taken from untrusted input. |
| `open-redirect` | Do not redirect to a URL taken from untrusted input. |
| `zip-slip` | Do not extract an archive entry whose path escapes the destination. |
| `reflection-injection` | Do not reflect over a name taken from untrusted input. |

And a few non-taint checks that show the range:

| Check | Message |
|---|---|
| `hardcoded-secret` | Do not hard-code a secret. |
| `no-plaintext-password` | Passwords should not be stored in plaintext or with a fast hashing algorithm. |
| `no-double-dispose` | Objects should not be disposed more than once. |
| `dispose-own-members` | Classes should `Dispose` of members from the classes' own `Dispose` methods. |
| `string-concat` | String concatenation should be string interpolation. |
| `no-empty-nullable-access` | Empty nullable value should not be accessed. |

Browse the full catalog in
[`src/Qlcheck/Languages/CSharp/Catalog/checks.json`](src/Qlcheck/Languages/CSharp/Catalog/checks.json).

## Suppressing a finding

Some findings need a human judgment call, not an automatic fix. Rather than silently deleting
or ignoring them, leave a dated suppression comment on the flagged line (or the line above):

```csharp
jwtBuilder.SignWith(key, SecurityAlgorithms.HmacSha256);
// qlcheck-ignore: jwt-strong-signature checked-on:2026-10-03
```

That silences this one finding for one year from `checked-on`, then it resurfaces for
re-evaluation — suppression comments are a snooze button, not a permanent exemption. Use one
only after a human has actually made the call the check's message asked for.

## Ignoring files

qlcheck walks the given paths like `git` would, skipping anything matched by a
`.qlcheck_ignore` file (gitignore syntax) in the scan root, in addition to sensible defaults
(`bin/`, `obj/`, `.git/`, `node_modules/`, `dist/`, build/VCS/IDE directories, …):

```
# .qlcheck_ignore
Generated/
**/*.Designer.cs
```

## How it's built

- **One language today: C#**, via Roslyn, with room to add more (`Language` is a first-class
  concept — a `Check` only ever runs against files of its own language).
- **Checks are rows, not classes.** Almost every check is a declarative `Row` entry
  (`Row.Taint("sql-injection", ...)`, `Row.Handle("no-double-dispose", ...)`) pointing at a
  small, focused matcher function — see `*Engine.cs` under `Catalog/`.
- **In-process, no subprocess per file.** One parse, one semantic model, every check walks it.
- The vocabulary (`Check`, `Finding`, `Language`, suppression semantics, …) is pinned down in
  [`CONTEXT.md`](CONTEXT.md). Design rationale lives in [`docs/adr/`](docs/adr).

## Development

```bash
dotnet build          # build the solution
dotnet test           # run the full test suite
./run --human .       # run the working tree's qlcheck against itself
```

qlcheck dogfoods itself — `./run --human .` from the repo root is a good first thing to try
after any change.
