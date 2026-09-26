# Catalog Checks are rows, not types

A catalog Check is a row: an id and a message in `checks.json`, plus a predicate on the engine that owns it. Syntax, symbol, flow, taint, and metric are those engines. `CatalogRun` is the seam. A shared predicate is a line in that engine's table. A predicate that is its own analysis is a method on that engine. Discovery appends the rows to the house Checks. It does not add a type per Check. Architecture rows stay discovered and unimplemented; selecting one exits 2. ADR 0002 still holds for house Checks: those remain in-process types.
