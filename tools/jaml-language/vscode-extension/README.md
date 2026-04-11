# JAML Language Support for VS Code

**JAML** (Jimbo's Ante Markup Language) and **Jummy** in the editor: highlighting, diagnostics, completion, snippets, schema-backed validation, and **in-editor seed search** (bundled **`motely-wasm-compat`** + Bootsharp â€” no separate WASM path to configure).

**Requires:** VS Code **â‰¥ 1.97** and the [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) (schema validation for `.jaml` / `.jummy`).

## Features

- **Syntax highlighting** â€” TextMate grammar for JAML and Jummy
- **LSP diagnostics** â€” Validation and errors in the editor
- **Completion** â€” JAML root keys and clause types
- **Snippets** â€” Common filter boilerplate
- **Run JAML Search** â€” Toolbar play / `Ctrl+Shift+Enter`: random search over the current documentâ€™s JAML (output in **JAML Search** panel). Uses the same engine as the Motely WASM stack; nothing to point at manually.

## Installation

**Marketplace:** [jaml-language-support](https://marketplace.visualstudio.com/items?itemName=pifreak.jaml-language-support)

**Manual VSIX:**

```bash
code --install-extension jaml-language-support-7.2.0.vsix
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

From a `.jaml` / `.jummy` file: **Run JAML Search** (editor title) or **Ctrl+Shift+Enter**. For CLI, sites, or full `motely-wasm`, use the main Motely repo / npm packages.

**Developers:** always install from the monorepo root â€” `cd tools/jaml-language && pnpm install` â€” then `pnpm --filter jaml-language-support run compile`. Do **not** run `npm install` inside `vscode-extension` alone (workspace protocol / hoisting).

## Settings

None required.

## For maintainers

- **Schema:** Canonical file is repo-root [`jaml.schema.json`](https://github.com/OptimusPi/MotelyJAML/blob/master/jaml.schema.json). `esbuild.mjs` copies it into this folder for Red Hat YAML (`./jaml.schema.json`). The npm package `jaml-schema` is only for **external** consumers of the schema on npm, not for this extension.
- **Build (matches [VS Code bundling guidance](https://code.visualstudio.com/api/working-with-extensions/bundling-extension)):** `pnpm run compile` â€” `tsc --noEmit` on the extension + LSP server, then esbuild (source maps, no minify). `pnpm run watch` rebuilds on change (esbuild only). After a clean that removed `core/dist`, run `pnpm --filter @motely/jaml-language-core run build` first (or `pnpm -r build` under `tools/jaml-language`).
- **VSIX:** `vsce package` runs **`vscode:prepublish`** first (`check-types` + production esbuild: minified, no source maps). **`--no-dependencies`** is required with this pnpm workspace because `vsce`â€™s `npm list` check does not match pnpmâ€™s layout; runtime deps are bundled into `dist/*.js`.
- **Marketplace VSIX:** run `package-vscode-extension.ps1` at repo root, then `pnpm install` and `pnpm --filter ./vscode-extension run package`.

## Contributing

[MotelyJAML on GitHub](https://github.com/OptimusPi/MotelyJAML)

## License

MIT
