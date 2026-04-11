# @motely/jaml-mcp

MCP server for JAML: **validate**, **compile Jummy**, **inspect seeds**, and **run searches** via **`motely-wasm-compat`** (Bootsharp). Requires a local publish of `Motely.BrowserWasm` so `../../../Motely.BrowserWasm/motely-wasm-compat` exists.

```bash
# from repo root
dotnet publish Motely.BrowserWasm -c Release /p:MotelyVersion=1.0.0

cd tools/jaml-language
pnpm install
pnpm --filter @motely/jaml-mcp build
```

Run (stdio):

```bash
node dist/server.js
```

Configure your MCP client to launch `node` with argument `…/jaml-mcp/dist/server.js` (or `pnpm exec jaml-mcp` after linking).
