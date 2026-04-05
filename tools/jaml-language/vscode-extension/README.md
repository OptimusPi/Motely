# JAML Language Support for VS Code

Curate Balatro seeded runs using **JAML** (Jimbo's Ante Markup Language) and **Jummy** syntax. This extension provides syntax highlighting, LSP diagnostics, completion, snippets, and in-editor seed search via **`motely-wasm-compat`** (same engine as `motely-wasm`; that package also ships schema, Monaco, and full assets).

**Requires:** VS Code **≥ 1.97** and the [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) (schema validation for `.jaml` / `.jummy`).

## Features

- **Syntax highlighting** — TextMate grammar for JAML and Jummy
- **LSP diagnostics** — Validation and errors in the editor
- **Completion** — JAML root keys and clause types
- **Seed search** — From `.jaml` files (Ctrl+Shift+Enter)
- **Notebooks (`.jamlnb`)** — Markdown docs + **filter** cells; each filter cell runs a full WASM search with live results in the notebook
- **Snippets** — Common filter boilerplate

## Installation

**Marketplace:** [jaml-language-support](https://marketplace.visualstudio.com/items?itemName=pifreak.jaml-language-support)

**Manual VSIX:**

```bash
code --install-extension jaml-language-support-1.1.7.vsix
```

## Usage

### Quick search

1. Open or create a `.jaml` file (YAML).
2. Write your filter (see [JAML syntax](#jaml-syntax)).
3. Use **▶ Run Search** or **Ctrl+Shift+Enter**.
4. Results open in the side panel.

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

### JAML notebooks (`.jamlnb`)

A `.jamlnb` file is a **JSON array of cells** — each cell is either documentation or an executable filter.

| `kind` | Role |
|--------|------|
| `markdown` | Notes, headings, explanations (not executed) |
| `filter` | Full JAML document; **Run** on this cell runs a **1M-seed search** (same engine as [Quick search](#quick-search)) |

**On run:** the cell shows **live** match count, seeds searched, elapsed time, and a **ranked seed/score table** (HTML). When finished, output also includes a **JSON** summary of the run.

**Cells are independent** — run different filters in different cells to compare strategies without leaving the notebook.

**Format** (on disk, pretty-printed JSON):

```json
[
  { "kind": "markdown", "source": "# Title\n\n..." },
  { "kind": "filter", "source": "id: my_filter\nname: \"Example\"\ndeck: Red\nstake: Gold\n\nmust:\n  - joker: Perkeo\n" }
]
```

If the file isn’t valid JSON, the whole file is treated as a **single filter** cell.

**Real example:** see [`example.jamlnb`](https://github.com/OptimusPi/MotelyJAML/blob/master/tools/jaml-language/vscode-extension/example.jamlnb) in the repo (multiple filters + markdown walkthrough).

In a notebook, use the cell **Run** control (kernel **JAML Seed Search**). The **Run JAML Search** / **Stop JAML Search** commands apply to `.jaml` / `.jummy` editors and the side panel, not notebook cells.

## Commands

| Command | When |
|---------|------|
| **Run JAML Search** | Toolbar on `.jaml` / `.jummy` editors |
| **Stop JAML Search** | Stops the side-panel search started from a `.jaml` / `.jummy` file |

## Keybindings

| Keys | Action |
|------|--------|
| Ctrl+Shift+Enter | Run search (JAML / Jummy editor only) |

## Settings

None required.

## For maintainers

- **WASM:** `motely-wasm-compat` — `file:` to `Motely.BrowserWasm/motely-wasm-compat` in dev, or `^<version>` from npm. Run `dotnet publish Motely.BrowserWasm` before packaging when using `file:`.
- **Schema:** `@motely/jaml-schema` — workspace package under `tools/jaml-language/jaml-schema` (`prepare` copies repo-root `jaml.schema.json`).
- **VSIX:** `esbuild.mjs` stages `index.mjs` → `dist/motely-wasm-compat.mjs` and schema → `jaml.schema.json`. **`vsce package --no-dependencies`** is required because `npm list` does not work with this pnpm layout.
- **Publish npm:** root `publish.ps1 -Publish` publishes `@motely/jaml-schema`, then `motely-wasm` / `motely-wasm-compat`.
- **Marketplace VSIX:** run `package-vscode-extension.ps1` at repo root (bumps wasm + schema semver), then `pnpm install` and `pnpm --filter ./vscode-extension run package`.

## Contributing

[MotelyJAML on GitHub](https://github.com/OptimusPi/MotelyJAML)

## License

MIT
