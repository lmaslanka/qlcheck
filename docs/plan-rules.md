# Plan: 508 C# Checks, reprocessed

`../rules.txt` is read once. Do not embed it. Do not copy its ids. Do not keep severity, type, repo, scope, tags, debt, standards, impacts, or parameters.

A Check here has an id and a message. Language is `csharp` for every one of these, so the catalog does not store it. A number that matters (7 parameters, complexity 10, 80 lines) is written into the message and becomes a const in the Check. No parameter object. No config file.

Do not reference `SonarAnalyzer.CSharp`. That package is Sonar Source-Available License v1.0, limited to a Non-competitive Purpose. Implement detection in this repo.

## Already a Check

Five source inspections collapse onto Checks this system already has, or onto one new Check. Do not add a second id.

| Id | Why |
|---|---|
| `magic-literal` | Magic numbers. |
| `unused-using` | Unnecessary usings. |
| `string-concat` | `+` concatenation, including concatenation in a loop. |
| `line-length` | Line length. Already specified in `docs/plan-line-length-check.md`. Max is 120. |
| `redundant-parentheses` | Two source inspections of the same defect. One Check. |

503 new Checks remain. 508 source inspections are accounted for.

## Catalog

`src/Qlcheck/Languages/CSharp/Catalog/checks.json` is an array of `{ "id", "message" }` and nothing else.

`CheckDiscovery.All()` appends those rows, still ordered by id. Each row is an `ICheck` with `Language` = `CSharpLanguage.LanguageId`. `EnabledByDefault` is false until that Check's detection exists, then true. That flag is code, not a copied field.

`--check` uses these ids. An id that is not in the catalog and not a house Check is still `Unknown check`. A known id with no detection yet exits 2: `Check 'method-complexity' has no implementation.` A missing implementation must not look like a clean run.

Default `qlcheck` stays the house Checks until a batch enables ids. `inline-sql` stays opt-in. End state is 6 house Checks plus the 496 enabled catalog Checks (503 minus the 7 architecture Checks, which stay off).

## How a Check runs

`CSharpLanguage` throws today unless a Check is `IFileCheck` or `ICompilationCheck`. Catalog Checks are neither. Hand their ids to one `CatalogRun` instead of throwing.

`CatalogRun` runs once per csproj group, not once per Check. It parses with `CSharpTrees` and uses the compilation already built for `unused-using`. It returns Findings only for selected ids.

`Finding.At(checkId, path, location, message)`. The message is the catalog message, made specific when the Check knows the literal or the count. Id shape stays `{check}:{file}:{line}:{column}`. No severity. No `Replacement` in this plan.

A Check that only applies to tests says so in its message (`test-has-assertion`, `no-sleep-in-test`, and the other test messages). It skips other files inside its own detection. That is not a stored scope.

## Compilation

Syntax Checks can run on the platform-only compilation. Symbol Checks will under-report until the csproj's references are on the compilation.

Before the symbol batch: if `CSharpCompilations.FindCsproj` hits, add that project's references. Adhoc files stay platform-only. House Checks keep today's compilation. Do not shell out to `dotnet build`.

## Engines

One engine, many ids. A Check is a predicate, not a copied walker. Do not add 503 files.

| Engine | Examples |
|---|---|
| Metrics | `method-complexity`, `cognitive-complexity`, `nesting-depth`, `too-many-parameters`, `file-length`, `function-length`, `inheritance-depth`, `class-coupling`, `generic-arity`, `case-length`, `duplicate-string` |
| Syntax | `empty-statement`, `no-goto`, `braces-required`, `no-tabs`, `newline-at-eof`, `redundant-parentheses`, `commented-code`, `no-todo` |
| Symbols | Needs references. `unused-local`, `unused-private-member`, `pascal-case-type`, `dispose-created`, `async-not-void` |
| Flow | `null-dereference`, `lock-released`, `reachable-branch`, `no-infinite-loop` |
| Taint | The 27 ids below. One source and sink table. |
| Architecture | The 7 ids below. Not enabled. The source has no model. Do not invent one. |

Taint ids: `xslt-injection`, `regex-dos`, `no-stack-trace-disclosure`, `untrusted-environment-variable`, `ldap-injection`, `command-injection`, `connection-string-injection`, `ssrf`, `sql-injection`, `command-argument-injection`, `filesystem-oracle`, `deserialization-injection`, `dynamic-code-injection`, `reflected-xss`, `path-injection`, `untrusted-session-cookie`, `loop-bound-injection`, `open-redirect`, `argument-injection`, `xml-injection`, `log-injection`, `nosql-injection`, `allocation-dos`, `ssrf-traversal`, `reflection-injection`, `zip-slip`, `xpath-injection`.

Architecture ids: `architecture-interface`, `architecture-component`, `architecture-rename`, `architecture-move`, `architecture-tangle`, `architecture-location`, `architecture-relationship`.

## Tests

- The catalog has 503 rows, each only `id` and `message`.
- Discovered ids contain every catalog id and no id from the source file.
- `--check method-complexity` exits 2 until that Check is implemented, then reports a fixture.
- A batch test names the ids it implemented, with one failing fixture and one clean fixture each.
- House tests stay green. `Checks run` stays 6 until the first batch enables ids.

## Tracer order

1. **Catalog.** Write `checks.json` from the inventory below. Discovery returns the 503 plus house Checks. Unimplemented `--check` exits 2. Default run unchanged.
2. **Metrics.** The threshold Checks. Enable them. One fixture per id.
3. **Syntax.** Classify every remaining catalog id as syntax, symbol, or flow. The manifest fails if an id has no class. Implement syntax. Enable those ids.
4. **Project references.** A symbol fixture that needs a referenced type fails before this and passes after.
5. **Symbols.** Implement and enable.
6. **Flow.** Implement and enable.
7. **Taint.** The 27 ids above. Enable them.
8. **Architecture.** The 7 ids stay discovered and unimplemented. `--check architecture-relationship` exits 2.
9. **ADR 0006.** Catalog Checks are rows, not types. ADR 0002 still holds for house Checks. Run tests. `qlcheck` on every changed `.cs` path.

## Done when

- The inventory below is the catalog. 503 ids, no extras, no source ids.
- Every non-architecture catalog Check is enabled and has a fixture.
- The 7 architecture Checks are discovered, not enabled, and a selected one exits 2.
- `--check` still rejects an unknown id.
- No analyzer package reference. No 503-file dump.
- Default `Checks run` is 6 plus every enabled catalog Check.

## Out of scope

- Code fixes / `Replacement`
- Severity on `Finding`
- A config file or quality profile
- Deleting the house Checks that already cover a source inspection
- An architecture model
- TypeScript

## Inventory

503 Checks. Id and message only.

| Id | Message |
|---|---|
| `abstract-class-mixed` | An abstract class should have both abstract and concrete methods. |
| `abstract-no-public-constructor` | `abstract` classes should not have `public` constructors. |
| `accessor-expected-field` | Getters and setters should access the expected fields. |
| `allocation-dos` | Do not size an allocation from untrusted input. |
| `architecture-component` | An intended architecture component is missing. |
| `architecture-interface` | A relationship does not match the intended architecture. |
| `architecture-location` | Move this class to the location the architecture requires. |
| `architecture-move` | Move this component as the architecture requires. |
| `architecture-relationship` | This relationship does not match the intended architecture. |
| `architecture-rename` | Rename this component as the architecture requires. |
| `architecture-tangle` | Remove this relationship. It tangles the architecture. |
| `argument-exception-name` | The name passed to `ArgumentException` must be a real parameter. |
| `argument-injection` | Do not pass untrusted input as an OS command argument. |
| `argument-order` | Arguments should be passed in the same order as the method parameters. |
| `assembly-version` | Assemblies should have version information. |
| `assert-argument-order` | Assertion arguments should be passed in the correct order. |
| `assert-no-side-effect` | Expressions used in `Debug.Assert` should not produce side effects. |
| `async-name-suffix` | Name an async method with the `Async` suffix. |
| `async-not-void` | `async` methods should not return `void`. |
| `attribute-usage` | Custom attributes should be marked with `System.AttributeUsageAttribute`. |
| `auto-property` | Trivial properties should be auto-implemented. |
| `azure-function-error-handling` | Azure Functions should use Structured Error Handling. |
| `azure-function-logs-failure` | Azure Functions should log all failures. |
| `begin-invoke-end-invoke` | Calls to delegate's method `BeginInvoke` should be paired with calls to `EndInvoke`. |
| `blazor-query-param-type` | This Blazor query parameter type is not supported. Change it. |
| `brace-at-line-start` | A close curly brace should be located at the beginning of a line. |
| `braces-required` | Control structures should use curly braces. |
| `caller-info-last` | Caller information parameters should come at the end of the parameter list. |
| `cartesian-explosion` | This query can multiply rows. Shape it so it does not. |
| `case-length` | A switch case exceeds 8 lines. Extract a method. |
| `catch-only-rethrow` | `catch` clauses should do more than rethrow. |
| `check-model-state` | ModelState.IsValid should be called in controller actions. |
| `check-stream-read` | The length returned from a stream read should be checked. |
| `class-coupling` | A class depends on more than 30 other classes. |
| `client-evaluated-default` | Client-evaluated default values should use database functions. |
| `cls-compliant` | Assemblies should be marked as CLS compliant. |
| `cognitive-complexity` | Cognitive complexity exceeds 15 for a method, or 3 for a property. |
| `collection-self-argument` | Collections should not be passed as arguments to their own methods. |
| `collection-size-compare` | Collection sizes and array length comparisons should make sense. |
| `com-visible-explicit` | Assemblies should explicitly specify COM visibility. |
| `command-argument-injection` | Validate OS command arguments taken from untrusted input. |
| `command-injection` | Do not build an OS command from untrusted input. |
| `commented-code` | Remove commented-out code. |
| `comparable-equals` | `Equals` and the comparison operators should be overridden when implementing `IComparable`. |
| `complete-assertion` | Assertions should be complete. |
| `concise-declaration` | Declarations and initializations should be as concise as possible. |
| `conditional-on-new-line` | Conditionals should start on new lines. |
| `configure-await-false` | `ConfigureAwait(false)` should be used. |
| `connection-string-injection` | Do not build a connection string from untrusted input. |
| `const-not-static-readonly` | `static readonly` constants should be `const` instead. |
| `constant-log-template` | Logging templates should be constant. |
| `constructor-argument-exists` | `ConstructorArgument` parameters should exist in constructors. |
| `constructor-non-virtual` | Constructors should only call non-overridable methods. |
| `consume-valuetask` | `ValueTask` should be consumed correctly. |
| `controller-base` | API Controllers should derive from ControllerBase instead of Controller. |
| `controller-route` | A Route attribute should be added to the controller when a route template is specified at the action level. |
| `controller-single-responsibility` | Controllers should not have mixed responsibilities. |
| `cookie-httponly` | Cookies should have the `HttpOnly` flag. |
| `cookie-secure` | Cookies should have the `secure` flag. |
| `copyright-header` | Add a copyright header comment at the top of the file. |
| `coverage-exclusion-reason` | `ExcludeFromCodeCoverage` attributes should include a justification. |
| `csrf-enabled` | CSRF protections should not be disabled. |
| `database-password` | A secure password should be used when connecting to a database. |
| `datetime-kind` | Always set the `DateTimeKind` when creating new `DateTime` instances. |
| `datetime-utc` | Use UTC when recording DateTime instants. |
| `debugger-display-member` | `DebuggerDisplayAttribute` strings should reference existing members. |
| `default-first-or-last` | `default` clauses should be first or last. |
| `default-parameter-value` | ``DefaultValue`` should not be used when ``DefaultParameterValue`` is meant. |
| `default-value-type` | Default values should be compatible with their entity property type. |
| `deserialization-injection` | Do not deserialize untrusted input. |
| `digit-separator` | Underscores should be used to make large numbers readable. |
| `discarded-include` | `Include` calls discarded by query reshaping should be fixed. |
| `disposable-finalizer` | Disposable types should declare finalizers. |
| `disposable-members` | Classes with `IDisposable` members should implement `IDisposable`. |
| `disposable-pattern` | `IDisposable` should be implemented correctly. |
| `dispose-created` | `IDisposables` should be disposed. |
| `dispose-implements-interface` | Methods named `Dispose` should implement `IDisposable.Dispose`. |
| `dispose-own-members` | Classes should `Dispose` of members from the classes' own `Dispose` methods. |
| `duplicate-string` | This string literal is repeated 3 or more times. Name it. |
| `durable-entity-interface` | Interfaces for durable entities should satisfy the restrictions. |
| `dynamic-code-injection` | Do not execute code built from untrusted input. |
| `dynamic-sql` | SQL queries should not be dynamically formatted. |
| `else-required` | `if ... else if` constructs should end with `else` clauses. |
| `empty-class` | Classes should not be empty. |
| `empty-method` | Methods should not be empty. |
| `empty-statement` | Empty statements should be removed. |
| `enum-int32` | Enumerations should have `Int32` storage. |
| `enum-name` | Name this enumeration in PascalCase. |
| `enum-name-suffix` | Enumeration type names should not have `Flags` or `Enum` suffixes. |
| `enum-not-reserved` | Enumeration members should not be named `Reserved`. |
| `equals-and-hashcode` | `Equals(Object)` and `GetHashCode()` should be overridden in pairs. |
| `equatable-sealed` | Classes implementing `IEquatable<T>` should be sealed. |
| `equatable-value-type` | Value types should implement `IEquatable<T>`. |
| `escape-extension-keyword` | Identifiers should not conflict with the C# 14 `extension` contextual keyword. |
| `escape-field-keyword` | Identifiers should not conflict with the C# 14 `field` contextual keyword. |
| `escape-partial-return` | Return types named `partial` should be escaped with `@`. |
| `escape-scoped` | `scoped` should be escaped when used as an identifier or type name in parenthesized lambda parameter lists. |
| `event-args` | Events should have proper arguments. |
| `event-handler-signature` | Event Handlers should have the correct signature. |
| `exception-name-extends` | Classes named like `Exception` should extend `Exception` or a subclass. |
| `exception-public` | Exception types should be `public`. |
| `exception-standard-constructors` | Exceptions should provide standard constructors. |
| `explicit-whitespace` | Whitespace and control characters in string literals should be explicit. |
| `export-implements-contract` | Classes should implement their `ExportAttribute` interfaces. |
| `expression-complexity` | An expression exceeds complexity 3. |
| `extension-own-namespace` | Extensions should be in separate namespaces. |
| `field-private` | Fields should be private. |
| `field-readonly` | Fields that are only assigned in the constructor should be `readonly`. |
| `field-used-as-local` | Private fields only used as local variables in methods should become local variables. |
| `file-length` | A file exceeds 1000 lines. Split it. |
| `filesystem-oracle` | Do not let file access reveal whether a path exists to an untrusted caller. |
| `filter-before-sort` | The collection should be filtered before sorting by using `Where` before `OrderBy`. |
| `finalizer-no-throw` | Finalizers should not throw exceptions. |
| `flags-attribute-only-flags` | Non-flags enums should not be marked with `FlagsAttribute`. |
| `flags-initialized` | Flags enumerations should explicitly initialize all their members. |
| `flags-none` | Flags enumerations zero-value members should be named `None`. |
| `float-equality` | Floating point numbers should not be tested for equality. |
| `format-provider-on-parse` | Use a format provider when parsing date and time. |
| `format-string-args` | Composite format strings should not lead to unexpected behavior at runtime. |
| `format-string-valid` | Composite format strings should be used correctly. |
| `function-length` | A function exceeds 80 lines. |
| `generic-arity` | A type has more than 2 type parameters, or a method has more than 3. |
| `generic-collection` | Collections should implement the generic interface. |
| `generic-event-handler` | Generic event handlers should be used. |
| `group-overloads` | Method overloads should be grouped together. |
| `hardcoded-credential` | Do not hard-code a credential. |
| `hardcoded-ip` | IP addresses should not be hardcoded. |
| `hardcoded-secret` | Do not hard-code a secret. |
| `hashcode-immutable` | `GetHashCode` should not reference mutable fields. |
| `http-client-factory` | You should pool HTTP connections with HttpClientFactory. |
| `http-verb-attribute` | REST API actions should be annotated with an HTTP verb attribute. |
| `identical-branch` | Two branches in a conditional structure should not have exactly the same implementation. |
| `identical-branches` | All branches in a conditional structure should not have exactly the same implementation. |
| `identical-if-condition` | Related `if/else if` statements should not have the same condition. |
| `identical-method` | Methods should not have identical implementations. |
| `identical-operands` | Identical expressions should not be used on both sides of operators. |
| `implement-partial` | Implementations should be provided for `partial` methods. |
| `indent-conditional` | A conditionally executed single line should be denoted by indentation. |
| `indexer-string-or-int` | Strings or integral types should be used for indexers. |
| `indexof-not-positive` | `IndexOf` checks should not be for positive numbers. |
| `inheritance-depth` | An inheritance chain is deeper than 5. Flatten it. |
| `integer-division-to-float` | Results of integer division should not be assigned to floating point variables. |
| `interface-method-callable` | Interface methods should be callable by derived types. |
| `invariant-loop-bound` | `for` loop stop conditions should be invariant. |
| `invoke-event` | Events should be invoked. |
| `iserializable-correct` | `ISerializable` should be implemented correctly. |
| `js-invokable-public` | `JSInvokable` attribute should only be used on public methods. |
| `jwt-strong-signature` | JWT should be signed and verified with strong cipher algorithms. |
| `ldap-authenticated` | LDAP connections should be authenticated. |
| `ldap-injection` | Do not build an LDAP query from untrusted input. |
| `literal-suffix-upper` | Literal suffixes should be upper case. |
| `lock-readonly-field` | Blocks should be synchronized on read-only fields. |
| `lock-released` | Locks should be released on all paths. |
| `log-argument-position` | Logging arguments should be passed to the correct parameter. |
| `log-caught-exception` | Logging in a catch clause should pass the caught exception as a parameter. |
| `log-injection` | Do not write untrusted input into a log message. |
| `log-or-rethrow` | Exceptions should be either logged or rethrown but not both. |
| `log-placeholder-order` | Log message template placeholders should be in the right order. |
| `log-placeholder-pascal-case` | Use PascalCase for named placeholders. |
| `log-template-syntax` | Log message template should be syntactically correct. |
| `logger-generic-matches-type` | Generic logger injection should match enclosing type. |
| `logger-matches-type` | Loggers should be named for their enclosing types. |
| `logger-name` | Name the logger field `log` or `_log`. |
| `logger-private-static` | Logger fields should be `private static readonly`. |
| `loop-bound-injection` | Do not take a loop bound from untrusted input. |
| `loop-condition-reachable` | For-loop conditions should be true at least once. |
| `loop-counter-direction` | A `for` loop update clause should move the counter in the right direction. |
| `loop-more-than-once` | Loops with at most one iteration should be refactored. |
| `loop-must-change-counter` | `for` loop increment clauses should modify the loops' counters. |
| `matching-lock-release` | A write lock should not be released when a read lock has been acquired and vice versa. |
| `member-not-more-visible` | Types should not have members with visibility set higher than the type's visibility. |
| `merge-identical-catch` | `try` statements with identical `catch` and/or `finally` blocks should be merged. |
| `merge-if` | Mergeable `if` statements should be combined. |
| `merge-include` | `Include` and `ThenInclude` chains of reference navigations should be merged into a single `Include` call. |
| `method-complexity` | A method or property exceeds complexity 10. |
| `migration-narrow-column` | Migrations should not narrow column types without converting existing data. |
| `model-binding` | Use model binding instead of reading raw request data. |
| `modulus-not-equality` | Modulus results should not be checked for direct equality. |
| `move-method-to-inner-class` | `private` methods called only by inner classes should be moved to those classes. |
| `multiline-needs-braces` | Multiline blocks should be enclosed in curly braces. |
| `named-namespace` | Types should be defined in named namespaces. |
| `nesting-depth` | Control flow is nested more than 3 levels. Flatten it. |
| `neutral-resources-language` | Assemblies should be marked with `NeutralResourcesLanguageAttribute`. |
| `newline-at-eof` | Files should end with a newline. |
| `no-ambiguous-params` | Method calls should not resolve ambiguously to overloads with `params`. |
| `no-anonymous-unsubscribe` | Anonymous delegates should not be used to unsubscribe from Events. |
| `no-array-covariance` | Array covariance should not be used. |
| `no-array-for-params` | Arrays should not be created for params parameters. |
| `no-assignment-in-expression` | Assignments should not be made from within sub-expressions. |
| `no-async-identifier` | `async` and `await` should not be used as identifiers. |
| `no-backslash-route` | Backslash should be avoided in route templates. |
| `no-base-equals` | Classes directly extending `object` should not call `base` in `GetHashCode` or `Equals`. |
| `no-base-equals-for-reference` | `base.Equals` should not be used to check for reference equality in `Equals` if `base` is not `object`. |
| `no-bitwise-non-flags` | Non-flags enums should not be used in bitwise operations. |
| `no-blocking-async` | Calls to `async` methods should not be blocking. |
| `no-blocking-async-function` | Calls to `async` methods should not be blocking in Azure Functions. |
| `no-boolean-literal-assert` | Literal boolean values should not be used in assertions. |
| `no-case-only-field-difference` | Child class fields should not differ from parent class fields only by capitalization. |
| `no-catch-exception` | `Exception` should not be caught. |
| `no-catch-null-reference` | NullReferenceException should not be caught. |
| `no-cleartext-protocol` | Clear-text protocols should not be used. |
| `no-collection-copy-property` | Properties should not make collection or array copies. |
| `no-colliding-interface-inherit` | Interfaces should not simply inherit from base interfaces with colliding members. |
| `no-compare-before-assign` | Variables should not be checked against the values they're about to be assigned. |
| `no-conflicting-transparency` | Members should not have conflicting transparency annotations. |
| `no-console-log` | Standard outputs should not be used directly to log anything. |
| `no-constant-return` | Methods should not return constants. |
| `no-custom-crypto` | Custom cryptographic algorithms should not be used. |
| `no-dangerous-get-handle` | `SafeHandle.DangerousGetHandle` should not be called. |
| `no-datetime-now-timing` | Avoid using `DateTime.Now` for benchmarking or timing operations. |
| `no-datetime-primary-key` | Date and time should not be used as a type for primary keys. |
| `no-debug-in-production` | Debugging features should not be enabled in production. |
| `no-default-argument` | Default parameter values should not be passed as arguments. |
| `no-default-initializer` | Members should not be initialized to default values. |
| `no-delegate-subtraction` | Delegates should not be subtracted. |
| `no-double-dispose` | Objects should not be disposed more than once. |
| `no-double-prefix` | Doubled prefix operators `!!` and `~~` should not be used. |
| `no-duplicate-cast` | Duplicate casts should not be made. |
| `no-duplicate-condition` | Sequential tests should not check the same condition. |
| `no-empty-block` | Nested blocks of code should not be left empty. |
| `no-empty-collection-access` | Empty collections should not be accessed or iterated. |
| `no-empty-comment` | Comments should not be empty. |
| `no-empty-default` | Empty `default` clauses should be removed. |
| `no-empty-fallthrough-case` | Empty `case` clauses that fall through to the `default` should be omitted. |
| `no-empty-finalizer` | Finalizers should not be empty. |
| `no-empty-interface` | Interfaces should not be empty. |
| `no-empty-namespace` | Namespaces should not be empty. |
| `no-empty-nullable-access` | Empty nullable value should not be accessed. |
| `no-equality-on-reference` | `operator==` should not be overloaded on reference types. |
| `no-exit` | Exit methods should not be called. |
| `no-expected-exception` | ``ExpectedException`` should not be used. |
| `no-explicit-caller-info` | Caller information arguments should not be provided explicitly. |
| `no-explicit-foreach-cast` | `Explicit` conversions of `foreach` loops should not be used. |
| `no-explicit-rethrow` | Exceptions should not be explicitly rethrown. |
| `no-extend-object` | Extension methods should not extend `object`. |
| `no-fake-operator` | Non-existent operators like `=+` should not be used. |
| `no-fixme` | Remove the FIXME comment. |
| `no-gc-collect` | `GC.Collect` should not be called. |
| `no-general-exception` | General or reserved exceptions should never be thrown. |
| `no-get-executing-assembly` | `Assembly.GetExecutingAssembly` should not be called. |
| `no-goto` | `goto` statement should not be used. |
| `no-gratuitous-boolean` | Boolean expressions should not be gratuitous. |
| `no-hardcoded-date-format` | Don't hardcode the format when turning dates and times to strings. |
| `no-hardcoded-jwt-secret` | JWT secret keys should not be disclosed. |
| `no-hardcoded-uri` | URIs should not be hardcoded. |
| `no-hide-base-method` | Base class methods should not be hidden. |
| `no-ignored-exception` | Generic exceptions should not be ignored. |
| `no-ignored-initial-value` | Method parameters, caught exceptions and foreach variables' initial values should not be ignored. |
| `no-ignored-test` | Tests should not be ignored. |
| `no-increment-in-expression` | Increment (++) and decrement (--) operators should not be used in a method call or mixed with other operators in an expression. |
| `no-infinite-loop` | Loops and recursions should not be infinite. |
| `no-inline-suppression` | Do not suppress a finding in source. |
| `no-insecure-random` | Pseudorandom number generators (PRNGs) should not be used in security contexts. |
| `no-interface-cast` | `interface` instances should not be cast to concrete types. |
| `no-inverted-boolean` | Boolean checks should not be inverted. |
| `no-is-this` | `is` should not be used with `this`. |
| `no-lambda-in-blazor-loop` | Using lambda expressions in loops should be avoided in Blazor markup section. |
| `no-list-in-public-api` | `Generic.List` instances should not be part of public APIs. |
| `no-literal-localized-arg` | Literals should not be passed as localized parameters. |
| `no-lock-local` | Blocks should not be synchronized on local variables. |
| `no-multidimensional-array` | Multidimensional arrays should not be used. |
| `no-nan-compare` | `NaN` should not be used in comparisons. |
| `no-narrow-visibility` | Inherited member visibility should not be decreased. |
| `no-nested-block` | Nested code blocks should not be used. |
| `no-nested-generic-signature` | Method signatures should not contain nested generic types. |
| `no-nested-switch` | `switch` statements should not be nested. |
| `no-nested-ternary` | Ternary operators should not be nested. |
| `no-new-guid` | `new Guid()` should not be used. |
| `no-new-shared-part` | `Shared` parts should not be created with `new`. |
| `no-not-implemented` | Do not throw `NotImplementedException`. |
| `no-null-and-is` | Null checks should not be combined with `is` operator checks. |
| `no-null-compare-unconstrained` | Generic parameters not constrained to reference types should not be compared to `null`. |
| `no-obsolete-base-type` | Types should not extend outdated base types. |
| `no-optional-parameter` | Optional parameters should not be used. |
| `no-optional-ref` | ``Optional`` should not be used on `ref` or `out` parameters. |
| `no-overflow` | Calculations should not overflow. |
| `no-overlapping-overload` | Method overloads with default parameter values should not overlap. |
| `no-params-on-override` | `params` should not be introduced on overrides. |
| `no-path-resolution` | OS commands should not rely on PATH resolution. |
| `no-plaintext-password` | Passwords should not be stored in plaintext or with a fast hashing algorithm. |
| `no-private-only-constructors` | Classes should not have only `private` constructors. |
| `no-public-const` | Public constant members should not be used. |
| `no-public-field` | Fields should not have public accessibility. |
| `no-public-multidimensional` | Public methods should not have multidimensional array parameters. |
| `no-public-pointer` | Pointers to unmanaged memory should not be visible. |
| `no-public-static-field` | Non-constant static fields should not be visible. |
| `no-public-static-mutable` | Mutable fields should not be `public static`. |
| `no-public-temp-file` | Temporary files should not be created in publicly writable directories. |
| `no-recursive-inheritance` | Type inheritance should not be recursive. |
| `no-redundant-base` | Inheritance list should not be redundant. |
| `no-redundant-constructor` | Constructor and destructor declarations should not be redundant. |
| `no-redundant-initializer` | Member initializer values should not be redundant. |
| `no-redundant-jump` | Jump statements should not be redundant. |
| `no-ref-parameter` | `out` and `ref` parameters should not be used. |
| `no-reference-equals-value` | `Object.ReferenceEquals` should not be used for value types. |
| `no-reflection-accessibility` | Reflection should not be used to increase accessibility of classes, methods, or fields. |
| `no-return-from-using` | `IDisposables` created in a `using` statement should not be returned. |
| `no-rooted-action-route` | ASP.NET controller actions should not have a route template starting with `/`. |
| `no-self-assignment` | Variables should not be self-assigned. |
| `no-shadow` | Local variables should not shadow class fields or properties. |
| `no-shadow-outer-static` | Inner class members should not shadow outer class `static` or type members. |
| `no-sleep-in-test` | `Thread.Sleep` should not be used in tests. |
| `no-stack-trace-disclosure` | Do not send a stack trace to a caller. |
| `no-static-in-generic` | Static fields should not be used in generic types. |
| `no-static-write-from-instance` | Instance members should not write to `static` fields. |
| `no-static-write-in-constructor` | Static fields should not be updated in constructors. |
| `no-suppress-finalize` | `GC.SuppressFinalize` should not be called. |
| `no-tabs` | Tabulation characters should not be used. |
| `no-this-from-constructor` | `this` should not be exposed from constructors. |
| `no-thread-static-initializer` | `ThreadStatic` fields should not be initialized. |
| `no-thread-suspend` | `Thread.Resume` and `Thread.Suspend` should not be used. |
| `no-throw-in-finally` | Exceptions should not be thrown in finally blocks. |
| `no-throw-in-getter` | Exceptions should not be thrown from property getters. |
| `no-todo` | Remove the TODO comment. |
| `no-trace-write` | `Trace.Write` and `Trace.WriteLine` should not be used. |
| `no-trace-write-line-if` | `Trace.WriteLineIf` should not be used with `TraceSwitch` levels. |
| `no-type-on-type` | Type should not be examined on `System.Type` instances. |
| `no-unconditional-replace` | Collection elements should not be replaced unconditionally. |
| `no-unexpected-throw` | Exceptions should not be thrown from unexpected methods. |
| `no-unsafe` | Unsafe code blocks should not be used. |
| `no-useless-bitwise` | Unnecessary bit operations should not be performed. |
| `no-useless-comparison` | Unnecessary mathematical comparisons should not be made. |
| `no-useless-increment` | Values should not be uselessly incremented. |
| `no-virtual-field-event` | Field-like events should not be virtual. |
| `no-weak-hash` | Weak hashing algorithms should not be used. |
| `no-weak-lock` | Shared resources should not be used for locking. |
| `no-weak-lock-object` | Threads should not lock on objects with weak identity. |
| `no-weak-tls` | Weak SSL/TLS protocols should not be used. |
| `no-world-accessible-file` | File permissions should not be set to world-accessible values. |
| `no-write-only-property` | Write-only properties should not be used. |
| `no-xxe` | Do not parse XML that can load external entities. |
| `normalize-uppercase` | Strings should be normalized to uppercase. |
| `nosql-injection` | Do not build a NoSQL query from untrusted input. |
| `null-dereference` | Null pointers should not be dereferenced. |
| `null-forgiving-needs-nullable` | Null-forgiving operators should not be used when nullable warnings are disabled. |
| `obsolete-needs-reason` | `Obsolete` attributes should include explanations. |
| `omit-redundant-name` | Redundant property names should be omitted in anonymous classes. |
| `one-statement-per-line` | Statements should be on separate lines. |
| `one-variable-per-line` | Multiple variables should not be declared on the same line. |
| `one-way-operation-void` | One-way `OperationContract` methods should have `void` return type. |
| `open-redirect` | Do not redirect to a URL taken from untrusted input. |
| `operator-named-alternative` | Operator overloads should have named alternatives. |
| `operators-consistent` | Operators should be overloaded consistently. |
| `optional-attribute-pair` | Parameters with ``DefaultParameterValue`` attributes should also be marked ``Optional``. |
| `optional-field-deserialize` | Deserialization methods should be provided for `OptionalField` members. |
| `override-does-more` | Overriding members should do more than simply call the same member in the base class. |
| `override-keeps-defaults` | Method overrides should not change parameter defaults. |
| `parameter-base-type` | Method parameters should be declared with base types. |
| `parameter-name-matches-base` | Parameter names should match base declaration and other partial definitions. |
| `parameter-name-not-method` | Parameter names should not duplicate the names of their methods. |
| `params-on-override` | `params` should be used on overrides. |
| `part-creation-policy` | `PartCreationPolicyAttribute` should be used with `ExportAttribute`. |
| `pascal-case-member` | Methods and properties should be named in PascalCase. |
| `pascal-case-type` | Types should be named in PascalCase. |
| `pass-optional-to-base` | Optional parameters should be passed to `base` calls. |
| `password-salt` | Password hashing functions should use an unpredictable salt. |
| `path-injection` | Do not build a file path from untrusted input. |
| `prefer-any` | `Any()` should be used to test for emptiness. |
| `prefer-assembly-load` | `Assembly.Load` should be used. |
| `prefer-awaitable` | Awaitable method should be used. |
| `prefer-cancellation-token` | The overload accepting a `CancellationToken` should be used. |
| `prefer-char-overload` | `StartsWith` and `EndsWith` overloads that take a `char` should be used instead of the ones that take a `string`. |
| `prefer-contains` | `Contains` should be used instead of `Any` for simple equality checks. |
| `prefer-datetimeoffset` | Use `DateTimeOffset` instead of `DateTime`. |
| `prefer-exists` | Collection-specific `Exists` method should be used instead of the `Any` extension. |
| `prefer-find` | `Find` method should be used instead of the `FirstOrDefault` extension. |
| `prefer-first-not-or-default` | First/Single should be used instead of FirstOrDefault/SingleOrDefault on collections that are known to be non-empty. |
| `prefer-format-provider` | Overloads with a `CultureInfo` or an `IFormatProvider` parameter should be used. |
| `prefer-generic` | Generics should be used when appropriate. |
| `prefer-index` | Prefer indexing instead of `Enumerable` methods on types implementing `IList`. |
| `prefer-isnullorempty` | `string.IsNullOrEmpty` should be used. |
| `prefer-linked-list-ends` | `First` and `Last` properties of `LinkedList` should be used instead of the `First()` and `Last()` extension methods. |
| `prefer-linq` | Loops should be simplified with `LINQ` expressions. |
| `prefer-nameof` | `nameof` should be used. |
| `prefer-params` | `params` should be used instead of `varargs`. |
| `prefer-property` | Properties should be preferred. |
| `prefer-range` | Start index should be used instead of calling Substring. |
| `prefer-set-min-max` | `Min/Max` properties of `Set` types should be used instead of the `Enumerable` extension methods. |
| `prefer-string-comparison` | Overloads with a `StringComparison` parameter should be used. |
| `prefer-string-create` | `string.Create` should be used instead of `FormattableString`. |
| `prefer-trueforall` | The collection-specific `TrueForAll` method should be used instead of the `All` extension. |
| `prefer-while` | A `while` loop should be used instead of a `for` loop. |
| `produces-response-type` | Actions that return a value should be annotated with ProducesResponseTypeAttribute containing the return type. |
| `property-not-get-method` | Property names should not match get methods. |
| `pure-returns-value` | Methods with `Pure` attribute should return a value. |
| `reachable-branch` | Conditionally executed code should be reachable. |
| `readonly-collection-property` | Collection properties should be readonly. |
| `readonly-field-assignment` | Property assignments should not be made for `readonly` fields not constrained to reference types. |
| `readonly-not-mutable` | Mutable, non-private fields should not be `readonly`. |
| `redundant-boolean` | Boolean literals should not be redundant. |
| `redundant-cast` | Redundant casts should not be used. |
| `redundant-include` | Redundant `Include` calls should be removed. |
| `redundant-modifier` | Redundant modifiers should not be used. |
| `redundant-null-forgiving` | Null-forgiving operators should not be redundant. |
| `redundant-nullable-compare` | Nullable type comparison should not be redundant. |
| `redundant-parentheses` | Remove redundant parentheses. |
| `redundant-to-array` | `string.ToCharArray()` and `ReadOnlySpan<T>.ToArray()` should not be called redundantly. |
| `redundant-tostring` | `ToString()` calls should not be redundant. |
| `reflected-xss` | Do not write untrusted input into a response. |
| `reflection-injection` | Do not reflect over a name taken from untrusted input. |
| `regex-dos` | Do not run an untrusted regular expression without a timeout. |
| `regex-timeout` | Regular expressions should be executed with a timeout. |
| `regular-number-pattern` | Number patterns should be regular. |
| `release-lock-same-method` | Locks should be released within the same method. |
| `remove-obsolete` | Remove obsolete code. |
| `request-size-limit` | Limit the HTTP request body to 8 MB. |
| `request-validation` | ASP.NET Request Validation should not be disabled. |
| `required-value-input` | Value type property used as input in a controller action should be nullable, required or annotated with the JsonRequiredAttribute to avoid under-posting. |
| `restrict-cors` | Cross-Origin Resource Sharing (CORS) policy should be restricted to trusted origins. |
| `restrict-deserialized-types` | Types allowed to be deserialized should be restricted. |
| `restrictive-csp` | Content Security Policies should be restrictive. |
| `return-empty-not-null` | Empty arrays and collections should be returned instead of null. |
| `reuse-client` | Client instances should not be recreated on each Azure Function invocation. |
| `robust-cipher` | Cipher algorithms should be robust. |
| `route-constraint-type` | Component parameter type should match the route parameter type constraint. |
| `safe-cast` | Invalid casts should be avoided. |
| `seal-attribute` | Non-abstract attributes should be sealed. |
| `seal-private-class` | Non-derived `private` classes and records should be `sealed`. |
| `sealed-no-protected` | `sealed` classes should not have `protected` members. |
| `secure-cipher-mode` | Encryption algorithms should be used with secure mode and padding scheme. |
| `secure-temp-file` | Insecure temporary file creation methods should not be used. |
| `serialization-callback` | Serialization event handlers should be implemented correctly. |
| `service-and-operation-contract` | `ServiceContract` and `OperationContract` attributes should be used together. |
| `set-locale` | Locales should be set for data types. |
| `shift-out-of-range` | Integral numbers should not be shifted by zero or more than their number of bits-1. |
| `shift-right-integer` | Right operands of shift operators should be integers. |
| `short-circuit` | Short-circuit logic should be used in boolean contexts. |
| `simplest-condition` | The simplest possible condition syntax should be used. |
| `simplify-linq` | LINQ expressions should be simplified. |
| `simplify-type-check` | Runtime type checking should be simplified. |
| `single-key-attribute` | Multiple ``Key`` attributes should not be used to define a composite key. |
| `single-orderby` | Multiple `OrderBy` calls should not be used. |
| `sql-injection` | Do not build a database query from untrusted input. |
| `sql-keyword-whitespace` | SQL keywords should be delimited by whitespace. |
| `ssrf` | Do not request a URL taken from untrusted input. |
| `ssrf-traversal` | Do not request a URL from untrusted input that can leave the intended host. |
| `sta-thread` | Windows Forms entry points should be marked with STAThread. |
| `stateless-azure-function` | Azure Functions should be stateless. |
| `static-field-inline-init` | `static` fields should be initialized inline. |
| `static-if-no-instance` | Methods and properties that don't access instance data should be static. |
| `static-init-order` | Static fields should appear in the order they must be initialized. |
| `string-culture` | Culture should be specified for `string` operations. |
| `strong-crypto-key` | Cryptographic keys should be robust. |
| `sum-overflow-check` | Overflow checking should not be disabled for `Enumerable.Sum`. |
| `suppress-finalize-needs-destructor` | `GC.SuppressFinalize` should not be invoked for types without destructors. |
| `switch-at-least-three` | `switch` statements should have at least 3 `case` clauses. |
| `switch-default` | `switch/Select` statements should contain a `default/Case Else` clauses. |
| `switch-too-many-cases` | A switch has more than 30 cases. Split it. |
| `task-not-null` | Non-async `Task/Task<T>` methods should not return null. |
| `test-has-assertion` | Tests should include assertions. |
| `test-has-case` | Test classes should contain at least one test case. |
| `test-signature` | Test method signatures should be correct. |
| `testable-clock` | Use a testable date/time provider. |
| `thread-static-on-static` | `ThreadStatic` should not be used on non-static fields. |
| `throw-created-exception` | Exceptions should not be created without being thrown. |
| `timezone-id-direct` | Use `TimeZoneInfo.FindSystemTimeZoneById` without converting the timezones with `TimezoneConverter`. |
| `too-many-logs` | This block logs too often. |
| `too-many-parameters` | A method has more than 7 parameters. |
| `tostring-not-null` | `ToString()` method should not return null. |
| `type-name-not-namespace` | Type names should not match namespaces. |
| `type-name-suffix` | Attribute, EventArgs, and Exception type names should end with the type being extended. |
| `type-param-in-signature` | All type parameters should be used in the parameter list to enable type inference. |
| `typed-equals-interface` | Classes that provide `Equals(<T>)` should implement `IEquatable<T>`. |
| `unassigned-member` | Unassigned members should be removed. |
| `unchanged-is-const` | Unchanged variables should be marked as `const`. |
| `unique-log-placeholder` | Message template placeholders should be unique. |
| `unpredictable-iv` | Cipher Block Chaining IVs should be unpredictable. |
| `unpredictable-random` | Secure random number generators should not output predictable values. |
| `unread-private-field` | Unread `private` fields should be removed. |
| `untrusted-environment-variable` | Do not set an environment variable from untrusted input. |
| `untrusted-session-cookie` | Do not create a session cookie from untrusted input. |
| `unused-assignment` | Unused assignments should be removed. |
| `unused-local` | Unused local variables should be removed. |
| `unused-parameter` | Unused method parameters should be removed. |
| `unused-private-member` | Unused private types or members should be removed. |
| `unused-return` | Methods should not return values that are never used. |
| `unused-type-parameter` | Unused type parameters should be removed. |
| `uri-not-string-argument` | `System.Uri` arguments should be used instead of strings. |
| `uri-overload-calls-uri` | String URI overloads should call `System.Uri` overloads. |
| `uri-parameter-not-string` | URI Parameters should not be strings. |
| `uri-property-not-string` | URI properties should not be strings. |
| `uri-return-not-string` | URI return values should not be strings. |
| `use-created-object` | Objects should not be created to be dropped immediately without being used. |
| `use-equals-not-operator` | `==` should not be used when `Equals` is overridden. |
| `use-lambda-parameter` | The lambda parameter should be used instead of capturing arguments in `ConcurrentDictionary` methods. |
| `use-return-value` | Methods without side effects should not have their return values ignored. |
| `use-stringbuilder` | `StringBuilder` data should be used. |
| `use-unix-epoch` | Use the `UnixEpoch` field instead of creating `DateTime` instances that point to the beginning of the Unix epoch. |
| `use-value-keyword` | `value` contextual keyword should be used. |
| `utility-private-constructor` | Utility classes should not have public constructors. |
| `valid-regex` | Regular expressions should be syntactically valid. |
| `validate-before-async` | Parameter validation in `async`/`await` methods should be wrapped. |
| `validate-before-yield` | Parameter validation in yielding methods should be wrapped. |
| `validate-on-deserialize` | Serializable objects should validate data during deserialization. |
| `validate-public-arguments` | Arguments of public methods should be validated against null. |
| `validate-xml-signature` | XML signatures should be validated securely. |
| `variant-type-parameter` | Generic type parameters should be co/contravariant when possible. |
| `verify-server-certificate` | Server certificates should be verified during SSL/TLS connections. |
| `wrap-native-method` | Native methods should be wrapped. |
| `xml-injection` | Do not build XML from untrusted input. |
| `xpath-injection` | Do not build an XPath expression from untrusted input. |
| `xslt-injection` | Do not build an XSLT transform from untrusted input. |
| `zip-slip` | Do not extract an archive entry whose path escapes the destination. |
