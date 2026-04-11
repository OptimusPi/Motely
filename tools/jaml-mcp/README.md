# balatro-seed-mcp

MCP server for **Balatro** seed search and analysis using MotelyJAML (`motely-wasm`). Deploys cleanly on **Vercel** (HTTP MCP) or runs locally over **stdio** (Claude Desktop, VS Code, Copilot).

## Tools

| Tool | Description |
|------|-------------|
| `search_seeds` | Random search up to 1M seeds against JAML **or Jummy**; returns JSON + **MCP App UI** in supported hosts |
| `analyze_seed` | Full ante-by-ante breakdown for one seed |
| `validate_jaml` | Quick JSON shape check |
| `get_version` | Engine version string |

### Case-insensitive input behavior

- `search_seeds` accepts accidental casing mismatch for known JAML keys/values (for example `DECK`, `stake`, `blueprint`, `anylegendary`) and normalizes to canonical Motely names before search.
- If you prefer freeform/casual input, use `jummy` directly.

### Example Jummy-powered search

Use `search_seeds` with `jummy`:

```json
{
  "jummy": "what: blueprint in ante 1\nwhere: deck red, stake white",
  "seed_count": 50000
}
```

## MCP Apps (inline UI)

`search_seeds` registers an MCP Apps extension ([Model Context Protocol Apps](https://modelcontextprotocol.github.io/ext-apps/api/)):

- **Resource:** `ui://balatro-seed-mcp/jaml-search-app.html` — single-file bundle (`text/html;profile=mcp-app`)
- **View:** **React 19** + [**Vercel json-render**](https://json-render.dev/) — generative UI catalog (`Stack`, `StatsBlock`, `SeedRow`, `Button`, `Text`) so you can evolve layouts from structured JSON
- **Hosts:** Claude, ChatGPT, VS Code, and others that negotiate the Apps extension — behavior falls back to plain JSON text where UI is not available

### Build the UI bundle

The server reads `mcp-ui/dist/jaml-search-app.html` at runtime. **Always build before deploy or local stdio** (unless you commit the dist, which we gitignore by default):

```bash
pnpm install
pnpm run build:mcp-ui
```

`pnpm run build` runs the same step. **Vercel:** set the project **Build Command** to `pnpm run build` (or `pnpm run build:mcp-ui`) so the HTML exists when the function cold-starts. `vercel.json` includes `mcp-ui/dist/**` in the serverless bundle for `api/server.ts`.

### Local MCP (stdio)

```bash
pnpm run build:mcp-ui
pnpm start
```

## HTTP MCP on Vercel

Endpoint: `https://<deployment>.vercel.app/mcp`

Optional: set `MCP_API_KEY` and send `Authorization: Bearer <key>`.

## Public demo API

- `POST /api/search` — body `{ "jaml": "<json string>", "seed_count": 100000 }`

## JAML format

Filters are **JSON objects** (same shape as Motely JAML-on-disk, expressed as JSON). Example:

```json
{
  "deck": "Red",
  "stake": "White",
  "must": [{ "joker": "Blueprint", "antes": [1], "sources": { "shopItems": [0, 1] } }]
}
```

## License

MIT
