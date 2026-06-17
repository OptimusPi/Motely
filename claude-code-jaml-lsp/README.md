# JAML LSP Plugin for Claude Code

Provides Claude Code with real-time language intelligence for **JAML** (Jimbo's Ante Markup Language) — the YAML-based DSL used to author Balatro/Motely seed filters.

## What it gives Claude

- **Diagnostics** — instant validation of JAML syntax, unknown keys, and invalid enum values (deck names, jokers, stakes, editions, etc.)
- **Completion** — context-aware suggestions for clause keys, joker names, card properties, and source types
- **Hover** — documentation on any JAML key or value
- **Document symbols** — outline of `must` / `should` / `mustNot` clauses for fast navigation

## Prerequisites

The `jaml-language-server` binary must be in your PATH. Install it from the `jaml-lsp` package in this repo:

```bash
cd /path/to/MotelyJAML/jaml-lsp
npm install -g .
# Or: npm link
```

This creates the `jaml-language-server` command globally.

## Installation

```bash
# Add this repo as a marketplace
claude plugin marketplace add /path/to/MotelyJAML/claude-code-jaml-lsp

# Install the plugin
claude plugin install jaml-lsp@jaml-lsp
```

## How it works

On every Claude Code session start, this plugin registers `jaml-language-server --stdio` as the LSP server for `.jaml` files. Once active, Claude can use its built-in LSP tool for:

| Operation | Use case |
|---|---|
| `hover` | Get docs for a JAML key or value |
| `goToDefinition` | Jump to where a clause is defined (if applicable) |
| `findReferences` | Find usages of a specific joker or tag across filters |
| `documentSymbol` | List all clauses in a filter for quick navigation |
| `completion` | Suggest valid keys/values as you type |

Claude also sees **automatic diagnostics** — real-time error and warning detection after each edit, faster than waiting for a full YAML parse.

## Troubleshooting

**`Executable not found in $PATH`** — Install the server globally:
```bash
npm install -g /path/to/MotelyJAML/jaml-lsp
```

Verify with `which jaml-language-server`.

## License

MIT
