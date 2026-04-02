# JAML Language Tooling (v1 scaffold)

This folder provides a practical starter stack to make JAML feel like a first-class language:

- `core/`: shared tokens, root keys, and tiny helpers.
- `lsp-server/`: diagnostics + completion provider for `jaml` / `jummy` docs.
- `vscode-extension/`: syntax highlighting + language config + LSP client wiring.
- `monaco/`: Monaco registration helper for web editors.

## Quick start

```bash
cd tools/jaml-language
pnpm install
pnpm build
```

## VS Code extension (local)

1. `cd tools/jaml-language && pnpm install && pnpm build`
2. Open `tools/jaml-language/vscode-extension` in VS Code.
3. Press `F5` to launch an Extension Development Host.

## Notes

- This is intentionally a clean v1 scaffold, not a full parser.
- Server-side validation is conservative and friendly (bad roots, malformed docs, common key mistakes).
- Extend `core/src/index.ts` to grow keyword coverage once real-world JAML samples surface edge cases.