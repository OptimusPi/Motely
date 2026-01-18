# 🧪 Quick Testing Guide

## Super Quick Start (Copy-Paste These!)

### Test JAML Genie Locally

**PowerShell:**
```powershell
cd external\Motely\Motely.API
.\test-jamlgenie.ps1
```

**Or manually:**
```powershell
cd external\Motely\Motely.API\cloudflare-worker-jamlgenie
npm install
wrangler dev
```

**Then in another terminal:**
```powershell
Invoke-RestMethod -Uri http://localhost:8787 -Method POST -ContentType "application/json" -Body '{"prompt":"Blueprint and Brainstorm"}'
```

### Test MCP Server Locally

```powershell
cd external\Motely\Motely.API
.\test-mcp.ps1
```

**Or manually:**
```powershell
cd external\Motely\Motely.API\cloudflare-worker
npm install
wrangler dev
```

**Then test:**
```powershell
Invoke-RestMethod -Uri http://localhost:8788 -Method POST -ContentType "application/json" -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

## Enable RAG (Makes JAML Genie Smarter!)

1. **Create Vectorize index:**
```powershell
wrangler vectorize create jaml-examples --dimensions=768 --metric=cosine
```

2. **Seed it with your JAML files:**
```powershell
cd external\Motely\Motely.API\cloudflare-worker-jamlgenie
$env:JAMLGENIE_WORKER_URL="http://localhost:8787"
node seed-vectorize.js
```

3. **Test again** - Now it uses similar examples! 🎉

## Deploy to Production

```powershell
cd external\Motely\Motely.API\cloudflare-worker-jamlgenie
wrangler deploy

cd ..\cloudflare-worker
wrangler deploy
```

**Test production:**
```powershell
# JAML Genie
Invoke-RestMethod -Uri https://jamlgenie.balatrogenie.app -Method POST -ContentType "application/json" -Body '{"prompt":"Blueprint and Brainstorm"}'

# MCP Server  
Invoke-RestMethod -Uri https://mcp.balatrogenie.app -Method POST -ContentType "application/json" -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

## What You'll See

**JAML Genie Response:**
```json
{
  "success": true,
  "jaml": "name: Blueprint Brainstorm\ndeck: Red\nstake: White\nmust:\n  - joker: Blueprint\n    antes: [1, 2, 3]\n  - joker: Brainstorm\n    antes: [1, 2, 3]\nshould: []\nmustNot: []\n"
}
```

**MCP Server Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "tools": [
      {
        "name": "generate_jaml_filter",
        "description": "Generate a JAML filter from natural language..."
      },
      ...
    ]
  }
}
```

## Troubleshooting

- **"wrangler: command not found"** → `npm install -g wrangler`
- **Port in use** → `wrangler dev --port 8789`
- **Vectorize errors** → Make sure index exists: `wrangler vectorize list`

See `TRY_IT_NOW.md` for more details!
