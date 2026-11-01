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
│   Lines checked                           29 │
│   Checks run                             335 │
│                                              │
│ Results                                      │
│   Findings                                 7 │
│   Files with findings                      1 │
│   Clean files                              0 │
│                                              │
│ By check                                     │
│   field-private                  1  (1 file) │
│   magic-literal                  2  (1 file) │
│   no-general-exception           1  (1 file) │
│   no-public-field                1  (1 file) │
│   one-variable-per-line          1  (1 file) │
│   sql-injection                  1  (1 file) │
│                                              │
│ By file                                      │
│   Sample.cs                                7 │
│                                              │
│ Duration                              690 ms │
╰──────────────────────────────────────────────╯
```

## Why qlcheck

- **Built for agents first.** A `Check` emits `Finding`s with a stable ID
  (`{check}:{file}:{line}:{column}`), a plain-language fix instruction, and sometimes a
  drop-in `replacement`. No severity levels to argue about, no suppressing-by-config-file
  rituals — just "here's what's wrong and here's one way to fix it."
- **Semantic, not textual.** Taint Checks follow untrusted data through each method's control
  flow (locals, reassignment, concatenation, collections, lambdas) into sinks resolved by symbol:
  `DbCommand.CommandText` fires, a method that merely happens to be named `Execute` or `Start`
  does not. The Finding names the source and the path it took. Flow Checks ask the compiler's own
  control-flow analysis whether a branch can run, a loop can end, or a lock is released.
- **Knows what it doesn't know.** Some findings genuinely need a human judgment call —
  `jwt-strong-signature` flags any signing call and tells the agent to *ask the user* whether
  the algorithm is strong enough, rather than silently guessing. Once a human has made that
  call, a dated suppression comment keeps the finding quiet for a year, then it resurfaces.
- **338 checks, zero config.** Covers naming, style, resource management, exceptions,
  concurrency, ~20 taint-tracked injection classes (SQL, command, path, LDAP, XPath,
  log, SSRF, zip-slip, and more), and hard-coded secrets in code and configuration files
  out of the box.
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
using System;
using System.Data.Common;

namespace Sample;

public class UserRepository
{
    public int MaxRetries = 3, MaxRedirects = 3;

    public void DeleteUser(DbCommand command, string userId)
    {
        var query = $"DELETE FROM Users WHERE Id = '{userId}'";
        command.CommandText = query;
        command.ExecuteNonQuery();
    }

    public void Load(DbCommand command)
    {
        try
        {
            command.ExecuteReader();
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
Sample.cs:9:5  field-private
  Fields should be private.

Sample.cs:9:29  magic-literal
  Magic number 3 is used 2 times in this file; make it a named const or enum member.

Sample.cs:9:47  magic-literal
  Magic number 3 is used 2 times in this file; make it a named const or enum member.

Sample.cs:26:13  no-general-exception
  General or reserved exceptions should never be thrown.

Sample.cs:9:5  no-public-field
  Fields should not have public accessibility.

Sample.cs:9:12  one-variable-per-line
  Multiple variables should not be declared on the same line.

Sample.cs:14:9  sql-injection
  Do not build a database query from untrusted input. `userId` (parameter of public `DeleteUser`, line 11) reaches `DbCommand.CommandText` via `query`. Pass it as a `DbParameter` instead of building the SQL text from it.
```

That `sql-injection` finding isn't a text match on a method name. The assignment's target
resolves to `System.Data.Common.DbCommand.CommandText`, and `userId`, a parameter of a public
method, is traced through the interpolated string in `query` before anything is reported.
The `ExecuteNonQuery()` and `ExecuteReader()` calls take no SQL, so they are not sinks.

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
      "id": "sql-injection:Sample.cs:14:9",
      "check": "sql-injection",
      "file": "Sample.cs",
      "line": 14,
      "column": 9,
      "message": "Do not build a database query from untrusted input. `userId` (parameter of public `DeleteUser`, line 11) reaches `DbCommand.CommandText` via `query`. Pass it as a `DbParameter` instead of building the SQL text from it."
    }
  ]
}
```

| Flag | Effect |
|---|---|
| *(none)* | JSON findings on stdout — the default, agent-consumable format |
| `--human` | Human-readable findings, one block per finding, with `replacement` text inlined |
| `--stats` | Appends the summary box shown above (files/lines/checks/findings, grouped by check and by file) |
| `--check <id>` | Run only the named check(s) (repeatable), ignoring the default/opt-in selection and any `.qlcheck_disable` file |
| `--coverage` | Opt in to the `coverage` check (runs your test suite via `dotnet test`/`coverlet` and reports under-covered methods) |
| `--uncommitted` | Report only files with uncommitted changes (staged, unstaged or untracked). Every file is still compiled and cross-file Checks still see committed files (a new copy of committed code is reported on the new copy), but a Finding an edit causes in an *unchanged* file (such as a new nullable warning at a caller) is not shown |
| `--help` / `-h` | Usage |

Exit codes: `0` clean, `1` findings reported, `2` a usage or environment error (e.g. no
project found for `--coverage`).

## What it checks

338 checks across these classes:

| Class | Checks | What it looks at |
|---|---|---|
| Syntax | 169 | Style, naming, structure — pure syntax tree shape |
| Symbol | 102 | Anything needing type/symbol info — accessibility, overloads, member shape |
| Taint | 27 | Dataflow within each method, over its control-flow graph, from an untrusted source (request input, console input, and for some Checks a public method's parameter) to a sink resolved by symbol |
| Metric | 13 | Size/complexity thresholds (method length, nesting, parameter count) |
| Flow | 11 | Control flow from the compiler's analysis (unreachable branches, loops that never end, locks not released on every path) |
| Compilation | 7 | Whole-compilation facts: unused usings, literals shared across files, and the compiler's nullable warnings (reported only where `dotnet build` would warn and the file has no compile errors) |
| Source set | 2 | Run-wide syntax over every scanned file, across projects: `duplicate-code` (a copied statement sequence, identifiers renamed consistently) and `identical-method` (methods with the same whole body) |
| Architecture | 2 | Your declared architecture, from `.qlcheck_architecture`: `architecture-relationship` (a reference a Constraint forbids) and `architecture-tangle` (components that depend on each other in a cycle). Nothing is reported without the file |
| Config | 4 | Hard-coded secrets in `appsettings*.json`, `*.config`, YAML, and git-tracked `.env*` files |
| *(opt-in)* | 1 | `coverage` — test coverage gaps, via your own test run |

A sample of the taint-tracked injection checks. Each follows untrusted data through locals,
concatenation, `StringBuilder`, collections, `await`, lambdas and local functions to a real sink of
that type, stops at sanitizers and guards (`int.Parse`, `Path.GetFileName`, an allow-list check,
`Url.IsLocalUrl`), and names the source and path in its message. It does not follow data into other
methods:

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
| `hardcoded-secret` | Do not hard-code a secret such as an API key, token or private key (the Finding says where to read it from instead and that the user must rotate it). |
| `no-plaintext-password` | Passwords should not be stored in plaintext or with a fast hashing algorithm. |
| `no-double-dispose` | Objects should not be disposed more than once. |
| `dispose-own-members` | Classes should `Dispose` of members from the classes' own `Dispose` methods. |
| `string-concat` | String concatenation should be string interpolation. |
| `no-empty-nullable-access` | Empty nullable value should not be accessed. |
| `nullable-argument` | Possibly null values should not be passed to non-nullable parameters. |

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

## Disabling checks

To turn a check off for a folder and everything below it, list its id in a `.qlcheck_disable`
file in that folder, one per line. `#` starts a comment, and `!id` turns a check back on below a
deeper folder:

```
# tests/.qlcheck_disable
magic-literal   # tests repeat literals on purpose
```

Disabling a check is a lasting policy, so a human decides it. An agent should not add a line
just to make findings go away. Unknown ids are an error (exit `2`). `--check <id>` and
`--coverage` override the file. Disabled checks are listed in the JSON output (`"disabled"`:
`check` + `under`) and in the `--stats` box, so a clean run can't be mistaken for an unchecked
one. Cross-file Checks still see files in a disabled folder: a copy of code there is not
reported, but `duplicate-code` elsewhere still names it.

## Ignoring files

qlcheck walks the given paths like `git` would, skipping anything matched by a
`.qlcheck_ignore` file (gitignore syntax), in addition to sensible defaults
(`bin/`, `obj/`, `.git/`, `node_modules/`, `dist/`, build/VCS/IDE directories, …):

```
# .qlcheck_ignore
Generated/
**/*.Designer.cs
```

## Declaring your architecture

The one thing qlcheck cannot infer is your intended architecture. Write it down in a
`.qlcheck_architecture` at the repository root and it is checked: each line is one **Constraint**
over dotted names (a namespace or a type, matching itself and everything below it).

```
# .qlcheck_architecture
App.Domain                   must-not-use  App.Infrastructure App.Web
System.Net.Http.HttpClient   only-used-by  App.Infrastructure.Http
no-cycles App.*
```

- `S… must-not-use T…`: code under `S` must not reference anything under `T`, except inside `S`
  itself.
- `T… only-used-by S…`: a type under `T` may be referenced only from code under `S`, or inside `T`.
- `no-cycles P.*`: each direct child of `P` is a component, and components must not depend on each
  other in a cycle. Each cycle is reported at its weakest link, the dependency with the fewest
  references.

References are found semantically (fully qualified names, types from a parent namespace, global
and static usings, extension methods), plus each `using` directive, so restore or build the
repository for full results. A malformed line, or a `.qlcheck_architecture` below the root, is an
error (exit `2`). Like `.qlcheck_disable`, the file is a human decision: an agent should fix the
code, not edit the Constraint. When it applies, the JSON output lists it under `"architecture"`
and `--stats` shows it.

## Where these files are read from

`.qlcheck_ignore` and `.qlcheck_disable` work like `.gitignore`. (`.qlcheck_architecture` does not:
there is one, at the root, for the whole repository.) Every file from the git root
down to a folder applies to that folder. Each file's patterns are relative to its own folder, and
a deeper file overrides a shallower one. Scanning `src/` therefore still honours the files at
the repository root. Outside a git repository, only files at or below the scanned path are read.
See [ADR 0010](docs/adr/0010-repo-files-combine-like-gitignore.md).

## How it's built

- **Two languages: C# and config.** C# goes through Roslyn; the `config` language reads
  `appsettings*.json`, `*.config` and YAML files in process and scans them for hard-coded
  secrets. `.env*` files are scanned only when git tracks them, because an untracked `.env` is
  where secrets belong (and outside a git repository they are not scanned at all). `Language`
  is a first-class concept — a `Check` only ever runs against files of its own language, and
  check ids are unique across languages.
- **Checks are rows, not classes.** Almost every check is a declarative `Row` entry
  (`Row.Flow("sql-injection", ...)`, `Row.Handle("no-double-dispose", ...)`) pointing at a
  small, focused matcher function — see `*Engine.cs` under `Catalog/`.
- **In-process, no subprocess per file.** One parse, one semantic model, every check walks it.
- **Compilations mirror the build.** Each project's `Nullable`, `OutputType`, implicit usings and
  ASP.NET Core framework reference are honoured, so restore or build the repository first: global
  usings and package references are read from `obj/`.
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
