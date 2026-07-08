# JAML handoff

JAML is real, and it's getting more real. This note hands you a clean picture of where it stands and where it's headed, so you can pick up with confidence.

## Current state — 2026-07-08 (uncommitted work in progress)

Branch `master` at `448d778d` (jaml-lsp 1.2.0). The working tree carries an **in-flight change: discriminator-scoped clause-key validation** in `jaml-lang`. Not yet committed, not yet published.

**The change:** the flat `AllClauseLevelKeys` union is being removed from `jaml-lang/src/generated.ts`. A clause key is now only valid *relative to its discriminator* (e.g. `suit` is invalid on a `joker` clause). `validator.ts` gained `findClauseDiscriminator()` — a prescan of each clause block, because YAML maps are unordered and the discriminator may appear after other keys. Clauses with no discriminator at all are flagged.

**Dirty files:**
- `jaml-lang/src/validator.ts` — scoped-key validation (+85/-~20)
- `jaml-lang/src/generated.ts` — `AllClauseLevelKeys` dropped, `MotelyStake` enum added
- `jaml-lang/test/scoped-keys.test.mjs` — **new**, pins the scoped behavior + engine-parity (every corpus filter the engine accepts must produce zero validator errors)
- `Motely.Schema.cs` — generator updated to match (stake enum, no flat key union)
- `jaml-lsp/schemas/jaml.schema.json`, `jaml-lsp/syntaxes/jaml.tmLanguage.json` — regenerated outputs
- `jaml-lsp/src/chatParticipant.ts` — one-line cleanup
- `claude-plugin/plugins/jaml-lsp/dist/server.js` + `skills/jaml/SKILL.md` — rebuilt bundle; the skill was slimmed (-58 lines) and `.claude/skills/jaml-authoring/SKILL.md` deleted (moved into the plugin)
- `Motely.Wasm/consumer/` — **new** black-box consumer harness: installs `motely-wasm@^24.0.0` from npm, serves a page, Playwright-tests seed finding. Nothing reaches into the repo.
- `Motely.Wasm/README.md` — +54 lines documenting the above

**To finish this slice:**
1. `cd jaml-lang && npm run build && node --test test/` — the scoped-keys suite must pass (it tests against `dist/`, so build first).
2. Re-run `dotnet run Motely.Schema.cs` if the registry changed, confirm generated outputs are stable.
3. Rebuild the LSP bundles (`jaml-lsp` and `claude-plugin`) from the new `jaml-lang`.
4. `cd Motely.Wasm/consumer && npm install && npm test` — Playwright black-box check against published `motely-wasm@24`.
5. Commit; version bump `jaml-lang` (currently 3.14.2) — scoped keys change diagnostics, likely minor or major bump.

**Related, already shipped today (separate repos):** `jaml-ui@4.1.0` published to npm (JAMLyzer clause highlighting, `JamlyzerBulk`); `d:\ErraticDeck.app` fully migrated to registry deps (`jaml-ui@^4.1.0`, `motely-wasm@^24`, `jaml-codemirror@^0.2.1`, `jaml-lang@^3.14.2`), builds green, no local tarballs anywhere.

## OPEN QUESTION — ask pifreak first, do not guess

The previous session burned out on this and its last words were a question that never got answered. Ask it before touching the validator again:

> **What does "JAML is real" look like to you, in one sentence?**

The tension it was circling: the dirty work in this tree improves the **TypeScript heuristic validator** (scoped clause keys, discriminator prescan). But the session's final reading — unconfirmed — was that this framing may itself be the failure: that whole-document validation should come from **one C# source of truth** (`JamlConfigLoader` / `MotelyJaml.validateLine`) with real line/column positions, so no editor ever plays YAML-scalar guessing games again, and `jaml-lang` becomes a thin client over engine truth (via WASM or LSP) rather than a parallel reimplementation.

Do not commit or extend the heuristic-validator work until pifreak answers. The dirty tree may be the right slice, the wrong slice, or a stepping stone — only his answer decides. Verify every claim before stating it.

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
