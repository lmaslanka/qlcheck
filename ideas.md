# Check ideas

Candidate Checks, none planned yet. Two families:

- **Dead defensive code**, the family of plan 07 (`redundant-null-check`): guards and conversions that can never change
  the outcome, often added "to be safe".
- **Required vs. optional lookups**: code should say whether a missing object is a bug or a valid state.

Each one should be provable from the compiler or the semantic model rather than guessed from names, unless it is
marked as a judgment call, and none duplicates an existing Check or the plan 01 revival backlog.

## Strongest candidates

High signal, mostly simple semantic checks.

### `redundant-contains-guard`

```csharp
if (!set.Contains(x)) set.Add(x);
if (list.Contains(x)) list.Remove(x);
if (d.ContainsKey(k)) d.Remove(k);
```

`HashSet.Add` and `Remove` already handle both cases and return a bool, so the guard is a second lookup. Resolve the
receiver type so only collections whose `Add`/`Remove` tolerate the missing or present case are reported.

### `double-dictionary-lookup`

```csharp
if (d.ContainsKey(k)) v = d[k];
```

Two lookups where one `TryGetValue` does it. Agents write this constantly.

### `redundant-empty-guard`

```csharp
if (items.Count > 0) { foreach (var x in items) { ... } }
if (items.Any()) { foreach (var x in items) { ... } }
```

A loop over an empty collection already does nothing. The `.Any()` form also enumerates twice. Only report when the
guarded block is nothing but the loop.

### `exception-loses-cause`

```csharp
catch (Exception ex) { throw new MyException(ex.Message); }
```

The wrapped exception drops the original stack trace. The fix is to pass `ex` as the inner exception. Unlike the other
ideas, this one loses information rather than just adding noise.

### `redundant-type-test`

```csharp
Order x = ...;
if (x is Order) { ... }
```

When the static type already is the tested type, `is` is a disguised null check. It pairs naturally with plan 07.

### `redundant-task-wrap`

```csharp
return await Task.FromResult(v);
await Task.Run(() => SyncCall());   // in a method that is async only because of this
```

Fake async: allocations and a state machine for no concurrency.

## Worth considering

More judgment calls.

### `catch-returns-default`

A catch-all that logs and returns `null`, `default` or an empty value. It hides the failure, and the null it returns
creates exactly the defensive checks plan 07 flags downstream. The existing `no-ignored-exception` only covers *empty*
catch blocks.

### `redundant-materialize`

`.ToList()` or `.ToArray()` followed only by `foreach`, `.Count()`, `.Any()` or `.First()` on the copy. The backlog's
`redundant-to-array` covers a slice of this; revive it in this wider form.

## Required vs. optional lookups

A second family, from a team coding rule: code should say whether a missing object is **a bug** (a required lookup:
corrupted data, misconfiguration, a broken invariant) or **a valid state** (an optional lookup the caller handles).

Decision test: *if this returned null, is there a correct thing for the program to do?* Yes means an optional lookup:
branch on it and do something the caller can act on. No means a required lookup: throw a descriptive exception.

```csharp
// Required: absence is a bug. Name the entity and key.
var config = await db.TenantConfigs.SingleOrDefaultAsync(c => c.TenantId == tenantId, ct)
    ?? throw new InvalidOperationException($"TenantConfig missing for tenant {tenantId}");

// Optional: absence is handled.
var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
if (order is null) return NotFound();
```

Which lookups are required is intent, so qlcheck can enforce the mechanics precisely but must *ask* about the
category. Where a Check cannot tell, its message should tell the agent to flag the code and ask the user, not to
rewrite it (like `jwt-strong-signature`).

### `bare-first-or-single`

`First()`, `Single()`, `FirstAsync()` and `SingleAsync()` (LINQ, `IQueryable`, EF Core), resolved by symbol. Their
"Sequence contains no elements" message is not acceptable diagnostics. The fix is `...OrDefault(...) ?? throw new
...($"<Entity> missing for <key> {key}")`. Precise, and no judgment needed.

### `null-forgiving-on-lookup`

```csharp
var user = (await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct))!;
```

A `!` on an `...OrDefault` result silences the nullable warning and hides the decision. Precise: the operand is the
result of a `FirstOrDefault`/`SingleOrDefault`/`LastOrDefault`/`Find` call, directly or through a local.

### `vague-lookup-exception`

`...OrDefault(...) ?? throw new X()` with no message, or with a constant message that names no key. The message should
name the entity and the key, which in practice means an interpolated string. Precise.

### `lookup-silent-fallback`

An `...OrDefault` result null-checked with a branch that only logs, returns `null`/`default`, `continue`s, or
substitutes a default, so execution carries on as if nothing happened. If the lookup is required this hides the broken
invariant; if it is optional, a log-only branch gives the caller nothing to act on. Judgment call: the message asks the
agent to confirm the category with the user and then either throw descriptively or return something actionable
(`NotFound()`, a `Result`, a created default).

### `catch-log-around-lookup`

A `try`/`catch` around a lookup that logs and continues. Required-lookup failures should reach the global handler
(middleware, `IExceptionHandler`, a hosted service's top-level catch), which logs once with context. Overlaps
`catch-returns-default` above; build them as one Check.

### `options-validate-on-start`

`services.AddOptions<T>()...Validate(...)` or `.ValidateDataAnnotations()` without `.ValidateOnStart()`. Startup-critical
configuration should fail the boot, not the first request. Precise, by following the builder chain. A wider version
(reference data the app cannot run without is loaded at startup) needs intent and stays a rule, not a Check.

### `first-picks-a-duplicate`

`First(x => x.Id == id)` or `FirstOrDefault` on a key, where uniqueness is also an invariant: `Single` would expose
duplicates instead of silently picking one. Heuristic: the predicate compares a property named `Id` or `<Entity>Id`, or
the EF key. Judgment call; worth trying only after the precise ones.

## Revive first (already in the plan 01 backlog)

Plan 01 removed three placeholder Checks from this family; each has a revival plan under `docs/plans/01-placeholders/`.

- `redundant-null-forgiving`: a `!` on a value the compiler already knows is not null. The mirror of plan 07, recorded
  as a follow-up in plan 02.
- `redundant-cast`
- `redundant-to-array`

## Deliberately not recommended

- **`string.IsNullOrEmpty` on a never-null string.** Ruled out for plan 07; the idiom is readable.
- **Comments that restate the code.** There is no reliable signal.
- **Eliding `return await`.** Removing it changes stack traces and `using` lifetimes, so it is a style argument, not
  dead code.
- **Reviving `prefer-first-not-or-default`.** Plan 01 already marked it not worth reviving, and it would now contradict
  the lookup rule: it pushes code from `FirstOrDefault` towards a bare `First()`.

## Suggested order

1. `redundant-contains-guard` and `double-dictionary-lookup`: frequent, simple, almost no false positives.
2. `exception-loses-cause`: the only idea that loses information.
3. The precise lookup Checks: `bare-first-or-single`, `null-forgiving-on-lookup`, `vague-lookup-exception`,
   `options-validate-on-start`.
4. The rest of the strongest candidates, then the backlog revivals.
5. The judgment-call lookup Checks (`lookup-silent-fallback` with `catch-log-around-lookup`, then
   `first-picks-a-duplicate`), once the precise ones have shown how teams react.
