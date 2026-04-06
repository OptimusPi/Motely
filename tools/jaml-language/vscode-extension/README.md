# JAML Language Support for VS Code

**JAML** (Jimbo's Ante Markup Language) and **Jummy** in the editor: highlighting, diagnostics, completion, snippets, and schema-backed validation. This extension is **language tooling only** — it does not run seed searches or bundle the WASM engine.

**Requires:** VS Code **≥ 1.97** and the [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) (schema validation for `.jaml` / `.jummy`).

## Features

- **Syntax highlighting** — TextMate grammar for JAML and Jummy
- **LSP diagnostics** — Validation and errors in the editor
- **Completion** — JAML root keys and clause types
- **Snippets** — Common filter boilerplate

## Installation

**Marketplace:** [jaml-language-support](https://marketplace.visualstudio.com/items?itemName=pifreak.jaml-language-support)

**Manual VSIX:**

```bash
code --install-extension jaml-language-support-1.1.7.vsix
```

## Usage

Open or create `.jaml` or `.jummy` files. Use the schema for structure and the language server for feedback.

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
  - boss: The Psychic

should:
  - joker: Brainstorm
    score: 50
```

Full reference: [jaml.schema.json](https://github.com/OptimusPi/MotelyJAML/blob/master/jaml.schema.json).

### Jummy (shorthand)

```
Eternal Blueprint in Ante 1
The Psychic boss
```

### Running searches

Use **Motely** (CLI, site, or any tool that consumes `motely-wasm` / your own runner). This extension does not execute searches.

## Settings

None required.

## For maintainers

- **Schema:** Canonical file is repo-root [`jaml.schema.json`](https://github.com/OptimusPi/MotelyJAML/blob/master/jaml.schema.json). `esbuild.mjs` copies it into this folder for Red Hat YAML (`./jaml.schema.json`). The npm package `jaml-schema` is only for **external** consumers of the schema on npm, not for this extension.
- **Build (matches [VS Code bundling guidance](https://code.visualstudio.com/api/working-with-extensions/bundling-extension)):** `pnpm run compile` — `tsc --noEmit` on the extension + LSP server, then esbuild (source maps, no minify). `pnpm run watch` rebuilds on change (esbuild only). After a clean that removed `core/dist`, run `pnpm --filter @motely/jaml-language-core run build` first (or `pnpm -r build` under `tools/jaml-language`).
- **VSIX:** `vsce package` runs **`vscode:prepublish`** first (`check-types` + production esbuild: minified, no source maps). **`--no-dependencies`** is required with this pnpm workspace because `vsce`’s `npm list` check does not match pnpm’s layout; runtime deps are bundled into `dist/*.js`.
- **Marketplace VSIX:** run `package-vscode-extension.ps1` at repo root, then `pnpm install` and `pnpm --filter ./vscode-extension run package`.

## Contributing

[MotelyJAML on GitHub](https://github.com/OptimusPi/MotelyJAML)

## License

MIT
