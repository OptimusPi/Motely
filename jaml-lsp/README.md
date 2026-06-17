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
cd ../jaml-lang && npm install && npm run build   # foundation
cd ../jaml-lsp  && npm install && npm run build   # server + client
npm run smoke                                      # drives the server over real LSP JSON-RPC
```

`npm run smoke` spawns the built server and exercises initialize / didOpen /
completion / hover / documentSymbol / publishDiagnostics over the wire.

## Install the server globally

For most editors, the server needs to be callable as `jaml-language-server`:

```sh
cd /path/to/jaml-lsp
npm install -g .
# Verify:
jaml-language-server --stdio
```

This creates the `jaml-language-server` command in your PATH.

## License

MIT.
