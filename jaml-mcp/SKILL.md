# jaml-mcp

MCP server that wraps `jaml-lang` — the typed language service generated from `JamlVocab.cs`.
Use these tools **before writing or editing any JAML filter**. Don't guess syntax; ask the server.

## Tools

| Tool | When to use |
|------|-------------|
| `jaml_vocab` | First. Get valid discriminators, clause keys, source keys, enum values. |
| `jaml_validate` | After drafting. Must return zero errors before handing to the user. |
| `jaml_complete` | When unsure what key or value to use at a position. |
| `jaml_hover` | To get human-readable docs for a token. |

## Workflow

1. `jaml_vocab(topic="discriminators")` — pick the right discriminator.
2. `jaml_vocab(discriminator="legendaryJoker")` — see its valid clause + source keys.
3. Draft the JAML.
4. `jaml_validate(text=<draft>)` — fix every error. Warnings are ok to ship.
5. Hand the clean filter to the user or load it into the seedfinder app.

## Running

```powershell
# build once (or after jaml-lang changes)
cd jaml-lang && npm run build
cd ../jaml-mcp && npm install && npm run build

# register in Claude Code ~/.claude/settings.json:
# {
#   "mcpServers": {
#     "jaml": {
#       "command": "node",
#       "args": ["<repo>/jaml-mcp/dist/index.js"]
#     }
#   }
# }
```

## Source of truth chain

`JamlVocab.cs` → `dotnet run --project Motely.Schema` → `jaml-lang/src/generated.ts` → `npm run build` → `jaml-mcp` serves it.

Never hand-edit `generated.ts`. Never ask the user for a JSON schema. It's already in `jaml-lsp/schemas/jaml.schema.json`.
