# Cryptography - revival plans

Status: Agreed 2026-10-29 · Depends on: 00 (compilation fidelity: global usings, csproj Nullable, ASP.NET refs, which lands first)

Part of `../01-placeholder-cleanup.md`. Every check here was removed in that plan because its
`Row.Invoke`/`Row.Attribute`/`Row.Ident` placeholder was DEAD or NOISY (see `../01-placeholder-triage.md`).

Each section is a standalone plan to bring the check back for real. 6 removed checks: 6 to revive, 0 not worth reviving.

Conventions for every section (not repeated below):
- **Revival = three edits:** restore the `{id, message}` entry in `checks.json` (original message
  quoted below; reword if the plan changes scope), add the row to the named engine, add the fixture.
- **Fixtures:** `bad` = the positive example, `good` = the negative example, wrapped in a namespace
  and type with the `using`s they need. Both must compile with zero errors (guard G1 in the main plan);
  framework types missing from the platform reference set (ASP.NET Core, EF Core, Logging, WinForms,
  test frameworks) are declared as small stubs inside the fixture.
- **Tests first:** add the fixture, watch `Each_implemented_check_has_a_failing_and_clean_fixture` fail,
  add the row, plus focused unit tests in the matching `*PatternsTests.cs` for each edge case listed.
- **Engine:** Syntax = `SyntaxEngine.Rows` (syntax-tree only), Symbol = `SymbolEngine.Rows` (needs
  `ctx.Model`), Compilation = `CompilationEngine` (needs other files), Flow = `FlowEngine` (CFG/dataflow).
- **Size:** S = one predicate, under a day. M = semantic model or several members, 1-2 days. L = dataflow,
  cross-file or new infrastructure.

## `no-custom-crypto`

Original message: "Custom cryptographic algorithms should not be used."  
Removed row: `Row.Invoke` on `Create on DES` (SymbolEngine.cs:92).

**Detect.** A class deriving from `HashAlgorithm`, `KeyedHashAlgorithm`, `SymmetricAlgorithm`, `AsymmetricAlgorithm` or `ICryptoTransform` implementations - i.e. a home-made algorithm.

Positive (finding):
```csharp
class XorCipher : SymmetricAlgorithm { /* ... */ }
```
Negative (no finding):
```csharp
using var aes = Aes.Create();
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `ClassDeclaration`; walk `BaseType` chain for those `System.Security.Cryptography` types; `AllInterfaces` for `ICryptoTransform`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Wrappers that delegate to a framework algorithm also derive from these bases - exempt classes whose members construct a framework algorithm? Recommend no exemption in v1; the message asks the agent to confirm.

## `no-insecure-random`

Original message: "Pseudorandom number generators (PRNGs) should not be used in security contexts."  
Removed row: `Row.Invoke` on `Next on Random` (SymbolEngine.cs:103).

**Detect.** `System.Random` used where the value feeds a security artifact: result assigned to/returned from a member whose name contains `token`, `password`, `secret`, `salt`, `nonce`, `key`, `otp`, `code`, or passed into such a parameter.

Positive (finding):
```csharp
string NewToken() => new Random().Next().ToString("x");
```
Negative (no finding):
```csharp
string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `InvocationExpression` whose receiver type is `System.Random` (instance, `Random.Shared`, or `new Random()`); walk up to the enclosing member/assignment and test its name against the list.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Name-based "security context" is a heuristic; alternative is to report every `System.Random` use with an ask-the-user message (like `jwt-strong-signature`). Recommend the name heuristic - fewer findings an agent cannot act on.

## `secure-cipher-mode`

Original message: "Encryption algorithms should be used with secure mode and padding scheme."  
Removed row: `Row.Invoke` on `CreateEncryptor` (SymbolEngine.cs:160).

**Detect.** `CipherMode.ECB` (assignment or argument), `PaddingMode.None` with a block cipher, `RSAEncryptionPadding.Pkcs1` for encryption, or legacy `rsa.Encrypt(data, false)`.

Positive (finding):
```csharp
aes.Mode = CipherMode.ECB;
```
Negative (no finding):
```csharp
aes.Mode = CipherMode.CBC; // or AesGcm
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `SimpleMemberAccessExpression` resolving to those enum fields/static properties, plus `Encrypt` invocations on `RSACryptoServiceProvider` with a literal `false` second argument.

**Fixtures.** bad = positive, good = negative.

## `strong-crypto-key`

Original message: "Cryptographic keys should be robust."  
Removed row: `Row.Invoke` on `GenerateKey` (SymbolEngine.cs:173).

**Detect.** Key sizes below policy: RSA/DSA `< 2048` (`RSA.Create(1024)`, `new RSACryptoServiceProvider(1024)`, `KeySize = 1024`), EC curves below 224 bits, `new Rfc2898DeriveBytes(...)` iteration counts below 100 000.

Positive (finding):
```csharp
using var rsa = RSA.Create(1024);
```
Negative (no finding):
```csharp
using var rsa = RSA.Create(3072);
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over creations/invocations/assignments on those types; constant-evaluate the size argument with `model.GetConstantValue`; thresholds in `Thresholds.cs`.

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Include the PBKDF2 iteration count here or as its own check? Recommend own check later; keep this to key sizes.

## `unpredictable-iv`

Original message: "Cipher Block Chaining IVs should be unpredictable."  
Removed row: `Row.Invoke` on `GenerateIV` (SymbolEngine.cs:180).

**Detect.** A CBC-mode cipher whose `IV` is set from a constant: `aes.IV = new byte[16]`, a byte-array literal, `Encoding.*.GetBytes("literal")`, or a static readonly field initialised from a literal.

Positive (finding):
```csharp
aes.IV = new byte[16];
```
Negative (no finding):
```csharp
aes.GenerateIV(); // or aes.IV = RandomNumberGenerator.GetBytes(16);
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `SimpleAssignmentExpression` to `IV` on a `SymmetricAlgorithm`; classify the right side (array creation without `RandomNumberGenerator`, literal-backed `GetBytes`, readonly field with constant initializer).

**Fixtures.** bad = positive, good = negative.

**Open questions.**
- Also `CreateEncryptor(key, constantIv)` arguments? Recommend yes - same classifier.

## `validate-xml-signature`

Original message: "XML signatures should be validated securely."  
Removed row: `Row.Invoke` on `CheckSignature` (SymbolEngine.cs:197).

**Detect.** `SignedXml.CheckSignature()` called with no arguments (trusts the key embedded in the document), or a result that is discarded.

Positive (finding):
```csharp
if (signed.CheckSignature()) { Accept(doc); }
```
Negative (no finding):
```csharp
if (signed.CheckSignature(trustedCert, verifySignatureOnly: true)) { Accept(doc); }
```

**Engine / scope.** Symbol.  **Size.** M.

**Matcher.** `Row.Handle` over `InvocationExpression` named `CheckSignature` whose method symbol is on `System.Security.Cryptography.Xml.SignedXml`, with zero arguments or as an `ExpressionStatement`.

**Fixtures.** bad = positive, good = negative. `System.Security.Cryptography.Xml` is a NuGet package, not in the TPA set; stub `SignedXml` in the fixture.

