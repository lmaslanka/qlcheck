# Intended architecture is declared in one .qlcheck_architecture file, in dotted names

Supersedes the part of 0006 that left the architecture rows unimplemented. The intended architecture
of a repository is information only a human has, so qlcheck does not infer it: a human writes it
in one `.qlcheck_architecture` at the git root (the scan root outside git), and two Checks enforce
it. With no file, nothing is extracted and nothing is reported.

- **One file, not combined per folder.** Unlike 0010's ignore and disable files, Constraints name
  each other's components and a tangle needs the whole graph, so per-folder overrides would be hard
  to read and to get right. A file below the root is an error, which leaves room to add combining
  later without silently changing meaning.
- **Dotted names, not folders or projects.** Both sides of a Constraint are namespaces or types,
  prefix-matched on dots. They cover project-per-layer and folder-per-layer layouts and type gates,
  and they work for targets from metadata, which have no folder. A csproj unit would restate what
  ProjectReference enforces and put Findings in a file the run filters cannot place.
- **Three kinds.** `must-not-use`, `only-used-by` and `no-cycles P.*`. No allow-lists, mid-name
  wildcards or exceptions yet; each can be a new keyword without breaking a file.
- **Semantic references, plus using directives.** Every name is bound to the type it references,
  which `using` scanning (0009) cannot see; using directives still count so a project that is not
  built reports its usings. Results therefore depend on references resolving, which 0011's
  compilation fidelity provides when the repository is restored.
- **Judged once per run.** Dependencies are extracted per Compilation and judged after all of them,
  so a cycle that only two projects together close is found (0007's engine-per-scope).
- **No baseline.** Legacy breaches are handled by narrowing the Constraint, disabling a tree, or a
  dated suppression comment; a shrink-only baseline waits until users ask for one.

The surprising part is that this is the first Check that needs a file the user writes, against the
"zero config" promise. It applies only when the file exists, and when it does the report says so,
so a clean run cannot be mistaken for an unchecked one.
