# Quick Start: Deploy MCP Server & JAML Genie

## What Was Done

✅ **JAML Genie Worker** - Updated with RAG/Vectorize support
- Enabled `retrieveSimilarExamples()` function
- Added `/embed` endpoint for embedding generation
- Added `/index` endpoint for Vectorize insertion
- Configured Vectorize binding in `wrangler.toml`

✅ **MCP Server Worker** - Ready for deployment
- Updated `wrangler.jsonc` with balatrogenie.app routes
- Configured to proxy to production API

✅ **Vectorize Seeding Script** - Created `seed-vectorize.js`
- Reads all JAML files from `JamlFilters/`
- Generates embeddings via Worker `/embed` endpoint
- Inserts into Vectorize index

## Deploy Now

### Option 1: Use the deployment script
```bash
cd external/Motely/Motely.API
chmod +x deploy.sh
./deploy.sh
```

### Option 2: Manual deployment

**1. Create Vectorize index:**
```bash
wrangler vectorize create jaml-examples \
  --dimensions=768 \
  --metric=cosine \
  --description="JAML filter examples for RAG"
```

**2. Deploy JAML Genie:**
```bash
cd cloudflare-worker-jamlgenie
npm install
wrangler deploy
```

**3. Seed Vectorize:**
```bash
export JAMLGENIE_WORKER_URL=https://jamlgenie.balatrogenie.app
node seed-vectorize.js
```

**4. Deploy MCP Server:**
```bash
cd ../cloudflare-worker
npm install
wrangler deploy
```

## Verify Deployment

**Test JAML Genie (with RAG):**
```bash
curl -X POST https://jamlgenie.balatrogenie.app \
  -H "Content-Type: application/json" \
  -d '{"prompt": "Blueprint and Brainstorm in Ante 1"}'
```

**Test MCP Server:**
```bash
curl -X POST https://mcp.balatrogenie.app \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/list"
  }'
```

## Configuration Files Updated

- `cloudflare-worker-jamlgenie/wrangler.toml` - Added Vectorize binding and routes
- `cloudflare-worker-jamlgenie/src/index.ts` - Enabled RAG functionality
- `cloudflare-worker/wrangler.jsonc` - Added balatrogenie.app routes
- `cloudflare-worker-jamlgenie/seed-vectorize.js` - Seeding script

## Next Steps

1. **Update API URL** - If your API isn't at `api.balatrogenie.app`, update `MOTELY_API_URL` in both wrangler configs
2. **Test RAG** - Generate a JAML filter and verify similar examples are retrieved
3. **Monitor Costs** - Check Cloudflare dashboard for Workers AI and Vectorize usage
