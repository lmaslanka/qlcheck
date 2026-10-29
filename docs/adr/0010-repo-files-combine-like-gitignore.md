# Repository files combine like .gitignore, and a file's folder is its scope

`.qlcheck_disable` (Disabled checks) and `.qlcheck_ignore` (ignored paths) are found by walking
from the scanned folder up to the git root. Every file found applies, each to its own folder and
everything below it, and a deeper file overrides a shallower one (`!id` / `!pattern` undo a rule
from above). Ignore patterns are relative to the folder their file sits in. Outside a git repository
only files at or below the scan root count.

We chose this over "the nearest file wins" because nearest-wins makes the result depend on which
folder is scanned: `./run src` would silently drop the repo-root rules whenever `src/` had a file of
its own, and agents routinely scan subfolders or use `--uncommitted`. With combining, `./run .` and
`./run src` report the same Findings for the same file. The folder a file sits in is the only
per-path control, so a Disabled check is scoped to a directory tree, never to a glob.

This changed `.qlcheck_ignore`: before, only the file at the scan root was read, and its patterns
were relative to the scan root.
