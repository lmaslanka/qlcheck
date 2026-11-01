# Taint Checks are intra-procedural dataflow with sinks resolved by symbol

Supersedes the name matching of 0001 for taint. A Taint Check used to fire when a sink's argument
was literally a parameter of a public method, and it took any method with the sink's name for the
sink: any `Start` was `Process.Start`, any `Write` an HTTP response. On compiling ADO.NET code
`sql-injection` could not fire at all. Now:

- **Dataflow over the control-flow graph, inside one member.** Untrusted data is followed through
  locals, reassignment (a clean assignment kills it), concatenation, `StringBuilder`, collections,
  `await`, fields of `this`, lambdas and local functions, to a fixpoint over Roslyn's
  `ControlFlowGraph`. Calls the model does not know return clean data.
- **Sinks are symbols.** A row is data: its sinks name a type by qualified metadata name, a member and
  a parameter by name, with derived types and interfaces matching. A user class that only shares a
  name does not match.
- **Labels, not deletion.** A sanitizer changes a value's label (`int.Parse` leaves `Numeric`, which
  only the allocation and loop rows accept), so one pass serves every row. Guards act on one branch.
- **Each row decides whether public parameters count.** Entry points (ASP.NET request input, the
  console) always do. A public method's parameter counts only for rows where a public API is an
  entry point (SQL, commands, XML); for file paths, reflection, sizes and the like, wrappers that take
  the value on purpose were nearly every Finding in real repositories.

Cross-method and cross-file taint is out by decision: summaries of callees, and fields written in one
member and read in another, would multiply the cost and the false positives, and an agent fixing a
Finding needs the source and the sink in front of it. Data passed to a helper is therefore not
followed.

The surprising part is that results now depend on references resolving, which 0001 avoided. When a
package is not restored, a call on its types does not bind; such a call still matches a sink by
member name and by the unresolved type's name ending in the sink type's core name (`SqlCommand` for
`IDbCommand`), with only its first argument counted. The cost is binding: a body is bound only when a
source can reach it and one of its names resolves to a sink, which keeps a full run within 2 % of
the name-matching engine.
