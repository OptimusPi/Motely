# 🚀 TRY IT NOW - Quick Test Guide

## TL;DR - Fastest Way to Test

### 1. Test JAML Genie Locally (2 minutes)

```powershell
# Open PowerShell in the project root
cd external\Motely\Motely.API\cloudflare-worker-jamlgenie

# Install deps
npm install

# Start dev server
wrangler dev
```

**In another terminal, test it:**
```powershell
curl.exe -X POST http://localhost:8787 -H "Content-Type: application/json" -d '{\"prompt\":\"Blueprint and Brainstorm\"}'
```

You should get back a JAML filter! 🎉

### 2. Enable RAG (Retrieval-Augmented Generation)

**Terminal 1:** Keep `wrangler dev` running

**Terminal 2:**
```powershell
# Create Vectorize index
wrangler vectorize create jaml-examples --dimensions=768 --metric=cosine

# Seed it with your JAML files
cd external\Motely\Motely.API\cloudflare-worker-jamlgenie
$env:JAMLGENIE_WORKER_URL="http://localhost:8787"
node seed-vectorize.js
```

**Now test again:**
```powershell
curl.exe -X POST http://localhost:8787 -H "Content-Type: application/json" -d '{\"prompt\":\"Blueprint and Brainstorm\"}'
```

The AI will now use similar JAML examples from your filters! 🤖✨

### 3. Test MCP Server

```powershell
cd external\Motely\Motely.API\cloudflare-worker
npm install
wrangler dev
```

**Test it:**
```powershell
curl.exe -X POST http://localhost:8788 -H "Content-Type: application/json" -d '{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}'
```

### 4. Deploy to balatrogenie.app

Once local testing works:

```powershell
cd external\Motely\Motely.API\cloudflare-worker-jamlgenie
wrangler deploy

cd ..\cloudflare-worker
wrangler deploy
```

**Then test production:**
```powershell
# JAML Genie
curl.exe -X POST https://jamlgenie.balatrogenie.app -H "Content-Type: application/json" -d '{\"prompt\":\"Blueprint and Brainstorm\"}'

# MCP Server
curl.exe -X POST https://mcp.balatrogenie.app -H "Content-Type: application/json" -d '{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}'
```

## What Each Worker Does

**JAML Genie** (`jamlgenie.balatrogenie.app`):
- Takes natural language: "Blueprint and Brainstorm in Ante 1"
- Uses AI + RAG (similar JAML examples) to generate perfect JAML filters
- Returns: `{ success: true, jaml: "..." }`

**MCP Server** (`mcp.balatrogenie.app`):
- Implements Model Context Protocol (MCP) for Claude/Cursor/Copilot
- Tools: `generate_jaml_filter`, `search_seeds`, `analyze_seed`
- Can be installed in Claude Desktop, Cursor, etc.

## Troubleshooting

**"wrangler: command not found"**
```powershell
npm install -g wrangler
```

**"Vectorize not configured"**
- Make sure you created the index: `wrangler vectorize list`
- Check `wrangler.toml` has `[[vectorize]]` section

**"No embedding generated"**
- Workers AI might not be enabled in your Cloudflare account
- Check: https://dash.cloudflare.com → Workers & Pages → AI

**Port already in use**
- Change port: `wrangler dev --port 8789`

## Next Steps After Testing

1. ✅ Test locally - Make sure everything works
2. ✅ Deploy to production - `wrangler deploy`
3. ✅ Seed Vectorize in production - `node seed-vectorize.js` (with production URL)
4. ✅ Install MCP server in Claude Desktop/Cursor
5. ✅ Share with friends! 🎮
