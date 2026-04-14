# JAML Language Support for VS Code

**JAML** (Jimbo's Ante Markup Language) and **Jummy** in the editor: highlighting, completions, snippets, schema-backed validation, in-editor seed search, and notebook support. The extension bundles `motely-wasm-compat` for local search execution.

**Requires:** VS Code **≥ 1.97** and the [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) (schema validation for `.jaml` / `.jummy`).

## Features

- **Syntax highlighting** — TextMate grammar for JAML and Jummy
- **Diagnostics** — Basic LSP parse/root-key diagnostics plus Red Hat YAML schema validation
- **Completion** — JAML root keys, clause keys, and schema-derived enum values
- **Snippets** — Common filter boilerplate
- **In-editor search** — Run a 1M-seed search from the command palette, editor title, or CodeLens
- **Notebook support** — Execute `.jamlnb` notebook cells against the bundled WASM engine

## Installation

**Marketplace:** [jaml-language-support](https://marketplace.visualstudio.com/items?itemName=pifreak.jaml-language-support)

**Manual VSIX:**

```bash
code --install-extension jaml-language-support-<version>.vsix
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

Run **JAML: Run JAML Search** from the command palette, use the editor title action, or click **Run Search (1M seeds)** on the first line. `.jamlnb` notebooks execute the same local WASM search engine cell-by-cell.

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
