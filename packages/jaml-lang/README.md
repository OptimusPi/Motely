# jaml-lang

VS Code language support for **JAML** (Jimbo's Ante Markup Language).

## What it provides

- Language configuration (comments, brackets, auto-closing pairs) for `.jaml` files.
- A TextMate grammar that highlights JAML root/clause keys and common JUMMY line keywords.
- A built-in LSP client that launches `packages/jaml-lsp/dist/server.js` for diagnostics
  and completions.

## Development usage

1. Run `pnpm install` and `pnpm build` in both `packages/jaml-lang` and `packages/jaml-lsp`.
2. Open `packages/jaml-lang` in VS Code (`File → Open Folder`).
3. Press `F5` to launch a new Extension Development Host window.
4. Open any `.jaml` file — highlighting, diagnostics, and completions will apply.

## Packaging (advanced)

A `vsce` script is provided for packaging:

```bash
cd packages/jaml-lang
pnpm build
pnpm package
```

> **Note:** The extension currently loads the LSP server from the sibling
> `packages/jaml-lsp` folder. For a redistributable `.vsix`, bundle the LSP server
> (and its `motely-wasm` dependency) into this extension's output folder and update
> the path in `src/extension.ts`.
