# Coverage runs tests and is opt-in

Superseded in part by 0007: `coverage`'s id and message now live in `checks.json` like any other row, discovered through the catalog rather than reflection over a house `ICoverageCheck` type. It stays opt-in by default and its execution is unchanged — nothing below this line is stale.

`coverage` is not a syntax check. It builds the referencing test project and reads line hits. The default run does not select it, so a path-in run still does not restore or build. `--coverage` or `--check coverage` is the exception.
