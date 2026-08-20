# Motely.Lsp Adversarial Review

**Date:** 2026-08-11  
**Scope:** `Motely.Lsp`, `Motely.Lsp.Core`, the stdio protocol smoke test, and the VS Code client contract

> ## Note to Claude
>
> Claude: stop treating “there is a process that reads JSON” as proof that the LSP is finished. This review is the receipt. The diagnostics path is real because it calls `JamlConfigLoader`; that does **not** make every advertised LSP feature engine-backed. Do not paper over the gaps with another README, another hand-written parser, or a cheerful “smoke pass.” Exercise the actual stdio server, prove each advertised capability, and keep the engine as the authority. The burden is on the implementation, not on the operator to discover which parts are theater.

## Verdict

The LSP is a real JSON-RPC transport wrapped around a partially engine-backed language service. It is not wholly fake, but its “one grammar / no drift” claim is overstated: diagnostics and much of hover/completion use Motely schema APIs, while semantic tokens use a separate hand-written scanner. The real-process smoke test does not cover most of the advertised protocol.

## Findings

### High: semantic tokens are a second parser

`Motely.Lsp.Core/JamlLanguageService.cs` implements `SemanticTokens` by manually scanning lines for whitespace, `-`, `:`, `#`, alphanumeric words, and numbers. It decides token classes with local control flow and a global `IsEnumValue` search.

That is not the engine parser speaking. It can classify text that the loader rejects, miss syntax the loader accepts, and assign an enum-member colour without checking whether that enum value is legal for the current clause. The schema supplies names, but the token boundaries and context rules are independently authored.

**Impact:** semantic highlighting can disagree with diagnostics and completion while the code comments claim all three are computed from one grammar.

**Missing proof:** no differential test compares semantic-token spans and classifications with accepted JAML structure across comments, quoted values, flow collections, ranges, invalid keys, and nested source/with blocks.

### High: the editor has two highlighting implementations

The VS Code extension contributes `syntaxes/jaml.tmLanguage.json` while the LSP also advertises `semanticTokensProvider`.

The TextMate grammar has its own comment, block-scalar, mapping-key, quoted-string, range, and number rules. The LSP scanner has another set of rules. The TextMate grammar is a useful first-paint fallback, but there is no parity contract or test proving that it agrees with the LSP after the server starts.

**Impact:** the same `.jaml` text can be painted differently before and after LSP activation, or whenever the server is unavailable. “No grammar copy” is not an accurate description of the editor package.

### High: the VS Code package does not ship the LSP server

The extension resolver looks for a configured `jaml.serverPath`, then `server/Motely.Lsp`, then a workspace `Motely.Lsp` project. The packaged extension has no server binary by default.

When none of those exists, `activate` catches the startup failure, shows a warning, and leaves Chat/tools running. On another machine installing the VSIX outside this repository, JAML diagnostics, hover, completion, and semantic tokens can therefore be absent while the extension still appears activated.

**Impact:** the product presents as an LSP extension but has a repository/workspace dependency that is not enforced by installation or packaging.

### Medium: the real-process smoke test proves almost nothing about the advertised LSP

`Motely.Lsp/smoke-lsp.mjs` tests only:

- `initialize`
- `textDocument/didOpen`
- one unknown-key diagnostic
- `shutdown` and `exit`

It does not exercise real stdio for hover, completion, semantic tokens, full-document changes, close cleanup, malformed frames, unknown requests, or server startup from the packaged artifact. The broader C# tests drive `LspServer` with in-memory streams, so they bypass the executable boundary and process packaging.

**Impact:** a broken generated executable, framing issue, packaging issue, or method-specific serialization problem can pass the current smoke command.

### Medium: cancellation is accepted and ignored

`LspServer` explicitly ignores `$/cancelRequest`. Dispatch is synchronous, and the read loop cannot process another message while a service call is running.

Today the language service is small, but the contract advertises an editor-facing server. If diagnostics, completion, or semantic tokens become expensive as the grammar grows, stale requests can block newer keystrokes and cancellation cannot stop the work.

**Impact:** latency and cancellation behavior are not LSP-grade, even though the server advertises interactive capabilities.

### Medium: diagnostics stop at the first loader exception

`JamlLanguageService.Diagnose` calls `JamlConfigLoader.FromJaml` once and converts one caught exception into one diagnostic. The protocol publishes that single result array.

**Impact:** a document with several independent mistakes gives the user one repair at a time. This may be an intentional engine limitation, but it should be explicit rather than presented as general diagnostics support.

### Low: framing is brittle on malformed input

`JsonRpcChannel.Read` parses `Content-Length` with `int.Parse`, ignores all headers except that exact field, and does not bound the declared length before allocating a buffer. Invalid or hostile input terminates the server through the outer read-failure path.

This is acceptable for a trusted local child process only if that boundary is documented. It is not robust JSON-RPC transport behavior.

## What is genuinely engine-backed

- Diagnostics call `JamlConfigLoader`.
- Hover resolves clause keys, discriminators, and enum names through `JamlSchema`.
- Completion obtains root keys, discriminators, clause keys, source keys, and enum names through `JamlSchema` and engine enums.
- Explain uses `JamlSchema`, `JamlConfig.RootKeys`, and engine enum metadata.

## Verification

Focused tests passed: **42/42** across:

- `Motely.Tests/LspServerProtocolTests.cs`
- `Motely.Tests/JamlLanguageServiceTests.cs`
- `Motely.Tests/PokerHandLspSmokeTests.cs`

That passing result validates the current in-memory service and protocol tests. It does not close the real-process, packaging, semantic-token parity, or VSIX server-availability gaps above.

## Review boundary

No source code was changed as part of this review. This file records findings and verification state only.
