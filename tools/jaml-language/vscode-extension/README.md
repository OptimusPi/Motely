# JAML Language Support for VS Code

**JAML** (Jimbo's Ante Markup Language) and **Jummy** in the editor: highlighting, completions, snippets, schema-backed validation, in-editor seed search, and notebook support. The extension bundles `motely-wasm` (NativeAOT-LLVM WASM build of Motely via Bootsharp) for local search execution.

**Requires:** VS Code **≥ 1.97** and the [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) (schema validation for `.jaml` / `.jummy`).

## Features

- **Syntax highlighting** — TextMate grammar for JAML and Jummy
- **Diagnostics** — LSP parse/root-key validation plus Red Hat YAML schema validation (150+ jokers, all bosses, vouchers, tags, editions, seals, enhancements)
- **Completion** — JAML root keys and clause keys
- **Snippets** — Common filter boilerplate
- **In-editor search** — Run a seed search from the command palette, editor title button, or `Ctrl+Shift+Enter`
- **Notebook support** — Execute `.jamlnb` notebook cells against the bundled WASM engine

## Installation

**Marketplace:** [jaml-language-support](https://marketplace.visualstudio.com/items?itemName=pifreak.jaml-language-support)

**Manual VSIX:**

```bash
code --install-extension jaml-language-support-<version>.vsix
```

## Usage

Open or create `.jaml` or `.jummy` files. The schema auto-validates structure and provides enum completions for all game items.

### JAML syntax

```yaml
id: my_filter
name: "My Balatro Search"
author: "Your Name"
deck: Red
stake: Gold

must:
  - joker: Blueprint
    stickers: [Eternal]
  - boss: ThePsychic

should:
  - joker: Brainstorm
    score: 50
```

### Jummy (shorthand)

```
Eternal Blueprint in Ante 1
ThePsychic boss
```

### Running searches

Use the `▶` button in the editor title bar, press `Ctrl+Shift+Enter`, or run **JAML: Run JAML Search** from the command palette. `.jamlnb` notebooks execute the same local WASM search engine cell-by-cell.

## For maintainers

- **Schema:** Generated at build time from `motely-wasm/types/bindings.g.d.ts` via `scripts/gen-schema.mjs`. Never hand-edit `schemas/jaml.schema.json` — it is overwritten on every build. The C# source → `bindings.g.d.ts` → `gen-schema.mjs` → `jaml.schema.json` is the authoritative chain.
- **Build:** `pnpm run compile` — runs `gen-schema`, then `tsc --noEmit` on the extension + LSP server, then esbuild (source maps, no minify). `pnpm run watch` rebuilds on change (esbuild only, no schema regen).
- **VSIX:** `pnpm run package` — generates schema, production esbuild (minified, no source maps), then `vsce package --no-dependencies`. The `--no-dependencies` flag is required because `vsce`'s `npm list` check does not understand pnpm's virtual store layout; runtime deps are fully bundled into `dist/*.js`.
- **Versioning:** The extension version tracks motely-wasm major versions. Only bump `motely-wasm` dep when the extension actually needs newer engine behavior.

## Contributing

[MotelyJAML on GitHub](https://github.com/OptimusPi/MotelyJAML)

## License

MIT
