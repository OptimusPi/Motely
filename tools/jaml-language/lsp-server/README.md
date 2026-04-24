# `@motely/jaml-lsp-server`

Node implementation of the **JAML / Jummy language server** using `vscode-languageserver` and `@motely/jaml-language-core`.

## Responsibility boundary

| In scope (LSP) | Out of scope (handled elsewhere) |
|----------------|-----------------------------------|
| Diagnostics, completion for `.jaml` / `.jummy` | Seed search (VS Code extension + `motely-wasm`) |
| Key lists from `@motely/jaml-language-core` | Enum value lists (use types / engine elsewhere) |
| Text document sync over LSP | Notebook execution, CodeLens, toolbar commands |

## How it reaches the editor

The **VS Code extension** does not run this package as a separate npm install at runtime. Its build (`vscode-extension/esbuild.mjs`) bundles `lsp-server/src/server.ts` to:

`vscode-extension/dist/server.js`

The extension activates `vscode-languageclient` with `TransportKind.ipc` pointing at that file. So: **source of truth for protocol behavior is this folder; the shippable artifact lives under `vscode-extension/dist/`.**

## Local development

```bash
# from tools/jaml-language
pnpm install
pnpm --filter @motely/jaml-language-core run build
pnpm --filter @motely/jaml-lsp-server run build
pnpm run dev:lsp
```

`dev:lsp` runs the compiled `dist/server.js` with `node --watch`. That is useful for quick server-only experiments; for full integration (client capabilities, document sync), use **Extension Development Host** with the vscode-extension `watch` script.

## Typechecking

The vscode-extension `check-types` script runs `tsc --noEmit` for **both** the extension and this package (`../lsp-server/tsconfig.json`). Run:

`pnpm --filter jaml-language-support run check-types`

before packaging if you want a guarantee that both sides typecheck (the `vscode:prepublish` hook used by `vsce` only runs production esbuild — see extension README).
