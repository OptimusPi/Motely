# jaml-language-support

A **real** language server for JAML (Jimbo's Ante Markup Language) — the language
Motely seed filters are authored in. Not a toy: it speaks the
[Language Server Protocol](https://microsoft.github.io/language-server-protocol/)
over stdio, so any LSP-capable editor (VS Code, Neovim, Helix, Zed, Claude Code)
can drive it.

## What it gives you

| Feature | Source |
|---|---|
| **Diagnostics** — YAML syntax + structural (bad enum, unknown key) | `getDiagnostics` |
| **Completion** — context-aware keys + enum values (joker names, vouchers, decks…) | `getCompletions` |
| **Hover** — key documentation | `getHover` |
| **Document symbols** — `must` / `should` / `mustNot` outline | `getDocumentSymbols` |
| **Token coloring** — TextMate grammar | `syntaxes/jaml.tmLanguage.json` |

## One source of truth

This package contains **zero** language logic. Every diagnostic, completion, and
hover comes from [`jaml-lang`](../jaml-lang), whose vocab is **generated from the
Motely C# engine enums** (`codegen/gen-vocab.mjs` → `vocab.generated.ts`). The
engine is the authority; this server is a thin wire adapter. No editor surface
ever carries its own hand-frozen schema again — that drift is exactly what this
replaces.

```
Motely C# enums  ──gen──▶  jaml-lang (Zod + vocab + service)  ──▶  this LSP server  ──▶  any editor
```

## Build & verify

```sh
cd ../jaml-lang && npm install && npm run gen && npm run build   # foundation
cd ../jaml-lsp  && npm install && npm run build                  # server + client
npm run smoke                                                    # drives the server over real LSP JSON-RPC
```

`npm run smoke` spawns the built server and exercises initialize / didOpen /
completion / hover / documentSymbol / publishDiagnostics over the wire.

## Use it in an editor

**VS Code** — this package *is* an extension (`main` → `dist/extension.js`).
Package it with `vsce package` and install the `.vsix`, or run the Extension
Development Host (F5) from this folder.

**Anything else (Neovim / Helix / Zed / Claude Code)** — point the editor's LSP
client at the standalone server binary:

```sh
node /abs/path/to/jaml-lsp/dist/server.js --stdio
```

The server is editor-agnostic; the VS Code extension is just one client wrapper
around that same binary.

## License

MIT.
