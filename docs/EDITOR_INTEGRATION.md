# JAML Editor Integration Guide

How to wire the **JAML Language Server** into every editor that matters.

## Prerequisites

All paths below assume the server is installed globally so `jaml-language-server` is in your PATH:

```bash
cd /path/to/MotelyJAML/jaml-lsp
npm install -g .
```

Or use the full Node path if you prefer not to install globally:

```bash
node /abs/path/to/MotelyJAML/jaml-lsp/dist/server.js --stdio
```

---

## VS Code

The `jaml-lsp` folder **is** a VS Code extension. You have two options:

### Option A: Run from source (development)

1. Open `jaml-lsp/` in VS Code.
2. Press `F5` to launch the **Extension Development Host**.
3. Open any `.jaml` file — diagnostics, completions, hover, and outline will light up.

### Option B: Package as `.vsix` and install

```bash
cd /path/to/MotelyJAML/jaml-lsp
npx vsce package
# Install the resulting .vsix in VS Code:
#   Code → Extensions → … → Install from VSIX
```

### VS Code settings

```json
{
  "jaml.trace.server": "verbose"
}
```

Enable verbose tracing to see raw LSP JSON-RPC in the Output panel.

---

## Neovim

Using `nvim-lspconfig` (recommended):

```lua
-- Add to your init.lua or lspconfig setup
local lspconfig = require('lspconfig')
local configs = require('lspconfig.configs')

if not configs.jaml then
  configs.jaml = {
    default_config = {
      cmd = { 'jaml-language-server', '--stdio' },
      filetypes = { 'jaml' },
      root_dir = lspconfig.util.find_git_ancestor,
      single_file_support = true,
      settings = {},
    },
  }
end

lspconfig.jaml.setup({})
```

Using `vim-lsp` (vimscript):

```vim
if executable('jaml-language-server')
  autocmd User lsp_setup call lsp#register_server({
    \ 'name': 'jaml-language-server',
    \ 'cmd': {server_info->['jaml-language-server', '--stdio']},
    \ 'allowlist': ['jaml'],
    \ })
endif
```

---

## Helix

Add to `~/.config/helix/languages.toml`:

```toml
[[language]]
name = "jaml"
scope = "source.jaml"
injection-regex = "jaml"
file-types = ["jaml"]
comment-token = "#"
indent = { tab-width = 2, unit = " " }

[language-server.jaml]
command = "jaml-language-server"
args = ["--stdio"]

[[grammar]]
name = "jaml"
source = { path = "/path/to/jaml-lsp/syntaxes/jaml.tmLanguage.json" }
```

> Note: Helix uses Tree-sitter grammars, not TextMate. The TextMate grammar in `jaml-lsp/syntaxes/jaml.tmLanguage.json` can be converted to Tree-sitter if needed, or you can use generic YAML highlighting as a fallback.

---

## Zed

Add to `~/.config/zed/settings.json` (or project-local `.zed/settings.json`):

```json
{
  "lsp": {
    "jaml-language-server": {
      "binary": {
        "path": "jaml-language-server",
        "arguments": ["--stdio"]
      }
    }
  },
  "languages": {
    "JAML": {
      "language_servers": ["jaml-language-server"]
    }
  }
}
```

You may also need to register the file extension in Zed’s language config. Zed does not yet support arbitrary extensions without a language definition, but you can open a `.jaml` file and set the language override to YAML as a temporary fallback, or contribute a `jaml` language definition to the Zed repo.

---

## Claude Code

The easiest way: install the dedicated plugin.

```bash
claude plugin marketplace add /path/to/MotelyJAML/claude-code-jaml-lsp
claude plugin install jaml-lsp@jaml-lsp
```

Claude Code will automatically start the LSP server on session start and expose it via the built-in `LSP` tool. Claude gets:

- **Diagnostics** — real-time error/warning detection after each edit
- **Completion** — context-aware suggestions for keys and values
- **Hover** — docs on any JAML key or enum value
- **Document symbols** — outline of `must` / `should` / `mustNot` clauses

### Manual setup (no plugin)

If you prefer not to use the plugin system, you can configure LSP directly in Claude Code via `.lsp.json` in your project root:

```json
{
  "jaml": {
    "command": "jaml-language-server",
    "args": ["--stdio"],
    "extensionToLanguage": {
      ".jaml": "jaml"
    }
  }
}
```

---

## Other editors (generic)

Any editor with LSP support can connect to the server via stdio. The server expects:

- **Transport**: stdio (JSON-RPC with `Content-Length` headers)
- **Trigger characters**: `:`, ` `, `-`, `\n`
- **File extension**: `.jaml`

Point your editor’s LSP client at:

```bash
jaml-language-server --stdio
```

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `Executable not found in $PATH` | `npm install -g /path/to/jaml-lsp` and verify with `which jaml-language-server` |
| No diagnostics on open | Server may not have received `textDocument/didOpen`. Check trace logs. |
| Completions not showing | Ensure the editor sends `textDocument/completion` at the cursor position. |
| Symbols not in outline | The outline provider must request `textDocument/documentSymbol`. |
| Wrong indent behavior | JAML uses 2-space indentation. Ensure your editor matches YAML indent rules. |

---

## Architecture recap

```
Motely C# enums
       │
       ▼
  jaml-lang (parser + vocab + service)
       │
       ▼
  jaml-lsp (LSP wire adapter — stdio/JSON-RPC)
       │
       ▼
  VS Code / Neovim / Helix / Zed / Claude Code
```

The server is **editor-agnostic**. Every editor gets the same diagnostics, completions, and hover because all intelligence lives in `jaml-lang`, which is generated from the actual C# engine enums. No drift, no stale copy-paste schemas.
