# Coverage runs tests and is opt-in

`coverage` is not a syntax check. It builds the referencing test project and reads line hits. The default run does not select it, so a path-in run still does not restore or build. `--coverage` or `--check coverage` is the exception.
