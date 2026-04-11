# JAML Language Support for VS Code

**JAML** (Jimbo’s Ante Markup Language) and **Jummy** in the editor: syntax highlighting, schema-backed validation (via the YAML extension), **LSP** diagnostics and completion, snippets, **in-editor seed search** (bundled **`motely-wasm-compat`** + Bootsharp — no separate WASM path to configure).

**Requires:** VS Code **≥ 1.97** and the [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) (this extension declares it as an `extensionDependency`). Schema validation for `.jaml` / `.jummy` uses the staged `jaml.schema.json`.

---

## What you get (two subsystems)

Understanding this split avoids confusion when debugging or extending the tool.

1. **Language Server (LSP)** — Runs as a Node child process (`dist/server.js`). Provides diagnostics, completion, and hover while you type. It uses `@motely/jaml-language-core` and reads enum metadata from `jaml.schema.json`. It does **not** execute Motely seed search.
2. **Extension host commands** — Same VSIX, different code path: **Run JAML Search** / **Ctrl+Shift+Enter** loads `motely-wasm-compat`, runs the random search for the current document text, and streams results to the **JAML Search** output channel.

**Red Hat YAML** handles JSON Schema validation against `jaml.schema.json`; the LSP adds live feedback (e.g. unknown keys, enums) consistent with the same schema.

See also: [tools/jaml-language/README.md](../README.md) (workspace overview) and [lsp-server/README.md](../lsp-server/README.md) (LSP-only details).

---

## Features

- **Syntax highlighting** — TextMate grammar for JAML and Jummy
- **LSP** — Validation-style diagnostics, completion, hover
- **Snippets** — Common filter boilerplate
- **Run JAML Search** — Editor toolbar / `Ctrl+Shift+Enter`: random search over the current document’s JAML (output in **JAML Search** panel). Same engine family as Motely WASM; bundled — nothing to point at manually.

---

## Installation

**Marketplace:** [jaml-language-support](https://marketplace.visualstudio.com/items?itemName=pifreak.jaml-language-support)

**Manual VSIX** (replace version with the file you built or downloaded):

```bash
code --install-extension jaml-language-support-7.2.0.vsix
```

---

## Usage

Open or create `.jaml` or `.jummy` files. Use the schema for structure, YAML extension messages for schema violations, and the language server for inline assistance.

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

From a `.jaml` / `.jummy` file: **Run JAML Search** (editor title) or **Ctrl+Shift+Enter**. For CLI, sites, or full `motely-wasm` workflows, use the main Motely repo / npm packages.

---

## Settings

None required for basic use.

---

## For maintainers

### Schema and assets

- **Canonical schema:** repo-root [`jaml.schema.json`](https://github.com/OptimusPi/MotelyJAML/blob/master/jaml.schema.json). `esbuild.mjs` copies it into this folder for Red Hat YAML (`./jaml.schema.json`).
- The npm package `jaml-schema` is for **external** npm consumers, not for this extension’s runtime.

### Build (aligned with [VS Code bundling guidance](https://code.visualstudio.com/api/working-with-extensions/bundling-extension))

| Script | What it does |
|--------|----------------|
| `pnpm run check-types` | `tsc --noEmit` for **both** the extension and `../lsp-server` |
| `pnpm run compile` | `check-types` then `node esbuild.mjs` (source maps, no minify) |
| `pnpm run watch` | `esbuild` watch only (no `tsc`; faster iteration) |
| `vscode:prepublish` | `node esbuild.mjs --production` (minify, no source maps) — **does not** run `tsc` |

After a clean that removed `core/dist`, run `pnpm --filter @motely/jaml-language-core run build` first, or `pnpm -r build` under `tools/jaml-language`.

**Developers:** install from **`tools/jaml-language`** — `pnpm install` — not `npm install` only inside `vscode-extension` (workspace protocol / hoisting).

### Packaging & release (VSIX)

1. From `tools/jaml-language`, ensure dependencies are installed: `pnpm install`.
2. Run a full typecheck + bundle: `pnpm --filter jaml-language-support run compile`.
3. Package: `pnpm --filter jaml-language-support run package`  
   (runs production esbuild, then `vsce package --no-dependencies`).

**pnpm + vsce:** This workspace uses pnpm. `vsce`’s default dependency check can disagree with pnpm’s layout (`npm list` noise). The `package` script passes **`--no-dependencies`** so a normal `pnpm run package` succeeds; bundles already include runtime code. If you invoke `vsce` by hand, add the same flag unless you are on a layout `vsce` accepts.

**Note:** You may see `npm warn Unknown env config "recursive"` when npm is spawned under pnpm — it is harmless for this flow.

**Version alignment:** Bump `version` in this `package.json` together with **`MotelyVersion`** in repo `Directory.Packages.props` and `motely-wasm-compat` compatibility when you ship a coordinated WASM + extension release.

---

## Contributing

[MotelyJAML on GitHub](https://github.com/OptimusPi/MotelyJAML)

## License

MIT
