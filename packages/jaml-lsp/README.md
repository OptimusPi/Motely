# jaml-lsp

A minimal Language Server Protocol server for **JAML** (Jimbo's Ante Markup Language),
powered by the real `motely-wasm` validator.

## Features

- **Diagnostics** — validates the whole document against `MotelyJaml.validate` on open,
  change, and save.
- **Completions** — offers known JAML root keys, clause keys, logic keys, and native
  filter names.

## Usage

```bash
pnpm install
pnpm build
node dist/server.js --stdio
```

Then connect any LSP client (VS Code, Neovim, etc.) to that stdio process.

Example VS Code `settings.json` snippet using a generic LSP client extension:

```json
{
  "languageServerExample.trace.server": "verbose",
  "languageServerExample.languages": ["jaml"],
  "languageServerExample.command": ["node", "packages/jaml-lsp/dist/server.js", "--stdio"]
}
```

> This is intentionally lightweight. It does not attempt a full YAML AST; it leans on
> the same Motely loader that runs the seed finder.
