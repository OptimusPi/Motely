# JAML language tooling (monorepo workspace)

This folder is a **pnpm workspace** that ships editor support and shared packages for **JAML** (Jimbo’s Ante Markup Language) and **Jummy**. Treat it as three layers: **shared core**, **LSP**, and **VS Code extension** (plus optional Monaco / MCP).

## Who should read what

| Perspective | Goal | Start here |
|-------------|------|------------|
| **End user** | Install from Marketplace or a VSIX, edit `.jaml` / `.jummy`, run in-editor search | [vscode-extension/README.md](./vscode-extension/README.md) |
| **Contributor** | Change diagnostics, completions, grammar, or search behavior | This file → [lsp-server/README.md](./lsp-server/README.md) + extension README “Maintainers” |
| **Release / packaging** | Produce a shippable VSIX aligned with Motely / WASM versions | Extension README “Packaging & release” + repo `Directory.Packages.props` (`MotelyVersion`) |
| **Integrator** | Embed Monaco, MCP, or npm `jaml-schema` elsewhere | Packages `monaco/`, `jaml-mcp/`, `jaml-schema/` |

## Architecture (high level)

```
                    ┌─────────────────────────────────────┐
                    │  VS Code extension (this workspace) │
                    │  • Spawns LSP (Node child process)   │
                    │  • Search: motely-wasm-compat (npm)  │
                    │  • Grammar, snippets, YAML schema    │
                    └───────────────┬─────────────────────┘
                                    │ IPC (Language Client)
                    ┌───────────────▼─────────────────────┐
                    │  LSP server (@motely/jaml-lsp-server)│
                    │  • Diagnostics, completion, hover    │
                    │  • Uses @motely/jaml-language-core   │
                    └───────────────┬─────────────────────┘
                                    │
                    ┌───────────────▼─────────────────────┐
                    │  core (@motely/jaml-language-core)  │
                    │  • Shared parsing / clause metadata  │
                    └─────────────────────────────────────┘
```

- **LSP does not run seed search.** Search is a separate command in the extension; it loads `motely-wasm-compat` and writes to the **JAML Search** output channel.
- **Red Hat YAML** validates structure against `jaml.schema.json` (staged next to the extension at build time). The LSP adds live feedback (unknown keys, enums, etc.) on top of that.
- **Build:** `esbuild` bundles `src/extension.ts` → `dist/extension.js` and `lsp-server/src/server.ts` → `dist/server.js` inside the extension folder. The published VSIX contains those bundles plus assets (schema, syntaxes, snippets).

## Workspace packages

| Package | Role |
|---------|------|
| `core` | Shared TS library for JAML/Jummy (keys, helpers). Consumed by LSP and other tools. |
| `jaml-schema` | Published npm package of the JSON Schema for **external** consumers; the VS Code extension uses the repo-staged `jaml.schema.json`, not this package at runtime. |
| `lsp-server` | `vscode-languageserver` implementation. |
| `vscode-extension` | VS Code UI, client, WASM search, notebooks. |
| `monaco` | Monaco editor integration (separate from VSIX). |
| `jaml-mcp` | MCP server (optional). |

## Common commands (from `tools/jaml-language`)

```bash
pnpm install
pnpm -r build
```

- **Typecheck + bundle extension (recommended before VSIX):**  
  `pnpm --filter jaml-language-support run compile`
- **Watch extension bundles (esbuild only):**  
  `pnpm --filter jaml-language-support run watch`
- **Develop LSP entry alone:** build `lsp-server`, then `pnpm run dev:lsp` (runs `node --watch dist/server.js` in that package — useful without VS Code; the real integration is always via the extension’s bundled `dist/server.js`).

After a clean that removed `core/dist`, build core first:  
`pnpm --filter @motely/jaml-language-core run build` or `pnpm -r build`.

## Version alignment

Motely WASM packages (e.g. `motely-wasm-compat` in the extension) should stay aligned with **`MotelyVersion`** in the repo root `Directory.Packages.props` and with any published **GitHub Pages** / release artifacts you ship alongside the extension. Bump the extension’s `package.json` version when you cut a coordinated release.

## Further reading

- [VS Code extension README](./vscode-extension/README.md) — features, install, maintainer and packaging notes.
- [LSP server README](./lsp-server/README.md) — what the protocol surface is and how it is bundled.
