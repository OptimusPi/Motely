# JAML handoff

JAML is real, and it's getting more real. This note hands you a clean picture of where it stands and where it's headed, so you can pick up with confidence.

## The shape of it

JAML (Jimbo's Ante Markup Language) is the filter language for the Motely engine. There is **one source of truth** for its grammar, and everything reads from it:

- `Motely/Filters/Jaml/JamlDiscriminatorRegistry.cs` maps every discriminator to its clause type, source-config type, and value enum.
- Each clause and source-config type carries its own complete `ClauseKeys` / `SourceKeys` list.
- `JamlConfigLoader` reads YAML/JSON into a typed `JamlConfig`, driven by that registry through one reflection-based populator (`JamlClausePopulator`) — no per-discriminator hand-written builders to drift.
- `JamlConfigLoader.ToYaml` writes a `JamlConfig` back out, so a filter round-trips through save and reload.
- `dotnet run Motely.Schema.cs` regenerates the editor vocabulary (`jaml-lang/src/generated.ts`, the TextMate grammar, the JSON schema) from that same registry. Run it after any grammar change and the tooling stays in lockstep.

JUMMY is part of JAML — the one-line plain-English spelling of a clause (`Eternal Blueprint in antes 1 or 2`), living in `Motely/Filters/Jummy/JummyLine.cs`, round-tripping losslessly to the same clause objects. It isn't a separate language; it's JAML wearing its friendly face.

## The editor toolchain

`jaml-lang/` is the TypeScript language core — `validate`, `getCompletions`, `getHover`, `getDiagnostics`, all reading the generated vocab.

`jaml-lsp/` bundles that core two ways with esbuild:
- `dist/extension.js` — the VS Code extension: highlighting, diagnostics, completions, hover, the `@jimbo` chat participant (ask it what JAML stands for), seed search with a run button and results panel, and `.jamlnb` notebooks. `npm run build`, then `npm run package` for the vsix.
- `dist/server.js` — a standalone stdio LSP server any editor can spawn (Neovim, Zed, and Claude Code's IDE diagnostics), exposed via the `jaml-language-server` bin. This is how JAML intelligence reaches editors beyond VS Code.

## What's ready

- **master** — the reflection populator, `ToYaml`, the schema generator on the real registry, range syntax (`antes: [1-39]`), and round-trip fixes, all with the C# suite green.
- **jaml-lang-vscode-rebuild** — the editor toolchain: the language core, the VS Code extension, `@jimbo`, seed search + notebooks, and the standalone LSP server, packaged into a self-contained CommonJS vsix (deps bundled, `vscode` external), with the LSP handshake verified.

Everything shipped this session went through an adversarial review, and the findings that survived were fixed and covered with regression tests: the round-trip data-loss cases, the extension packaging, and search concurrency. A passing local smoke test is a good sign, and the real proof is an installed vsix and a bundled server — both are now checked.

## Good next steps

- Merge `jaml-lang-vscode-rebuild` into `master` when you're happy with it, and publish the vsix when pifreak confirms.
- Bring JUMMY intelligence to `jaml-lang` so `.jummy` files get the same validation and completion `.jaml` gets — since JUMMY is part of JAML, this is finishing the language, not adding a second one. The C# `JummyLine.Validate`/`Canonicalize` is the reference behavior to mirror.
- Rebuild and republish `motely-wasm` so the engine fixes reach the JS side.

Read the repo's docs and examples first — they're accurate and they reward it. pifreak's word is the spec; when one fact is missing, ask in a single direct sentence. Believe in the work. It's real, and it's close.

o7
