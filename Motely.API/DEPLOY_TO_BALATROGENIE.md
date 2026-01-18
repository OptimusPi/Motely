# Deploy MCP Server and JAML Genie to balatrogenie.app

## Prerequisites

1. Cloudflare account with Workers and Vectorize enabled
2. Domain `balatrogenie.app` added to Cloudflare
3. Cloudflare API token with Workers and Vectorize permissions
4. Node.js and Wrangler CLI installed

## Step 1: Create Vectorize Index

```bash
# Create the Vectorize index for JAML examples
wrangler vectorize create jaml-examples \
  --dimensions=768 \
  --metric=cosine \
  --description="JAML filter examples for RAG"
```

## Step 2: Deploy JAML Genie Worker

```bash
cd cloudflare-worker-jamlgenie

# Install dependencies
npm install

# Deploy to production
wrangler deploy --env production
```

The worker will be available at `jamlgenie.balatrogenie.app`.

## Step 3: Seed Vectorize Index

```bash
# Set the worker URL (use production URL)
export JAMLGENIE_WORKER_URL=https://jamlgenie.balatrogenie.app

# Install js-yaml if needed
npm install js-yaml

# Run the seeding script
node seed-vectorize.js
```

This will:
- Read all `.jaml` files from `../../JamlFilters`
- Generate embeddings using the Worker's `/embed` endpoint
- Insert them into the Vectorize index

## Step 4: Deploy MCP Server Worker

```bash
cd ../cloudflare-worker

# Install dependencies
npm install

# Deploy to production
wrangler deploy --env production
```

The worker will be available at `mcp.balatrogenie.app`.

## Step 5: Update Backend API URL

Update `MOTELY_API_URL` in both workers' wrangler configs to point to your production API:
- `https://api.balatrogenie.app` (or your actual API URL)

## Step 6: Test Deployments

### Test JAML Genie:
```bash
curl -X POST https://jamlgenie.balatrogenie.app \
  -H "Content-Type: application/json" \
  -d '{"prompt": "Blueprint and Brainstorm in Ante 1"}'
```

### Test MCP Server:
```bash
curl -X POST https://mcp.balatrogenie.app \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {}
  }'
```

## Environment Variables

### JAML Genie Worker
- `VECTORIZE` - Automatically bound from wrangler.toml
- `AI` - Automatically bound (Workers AI)

### MCP Server Worker
- `MOTELY_API_URL` - Set in wrangler.jsonc vars
- `API_KEY` - Optional, set in wrangler.jsonc vars if using auth

## Troubleshooting

### Vectorize Index Not Found
- Ensure the index name matches in `wrangler.toml` (`index_name = "jaml-examples"`)
- Check that the index exists: `wrangler vectorize list`

### Embedding Generation Fails
- Verify Workers AI is enabled in your Cloudflare account
- Check Worker logs: `wrangler tail`

### RAG Not Working
- Verify Vectorize index is seeded: Check index size
- Test embedding endpoint: `curl -X POST https://jamlgenie.balatrogenie.app/embed -d '{"text":"test"}'`
- Check Worker logs for RAG errors

## Cost Estimates

- **Workers**: Free tier includes 100,000 requests/day
- **Workers AI**: ~$0.01 per 1K tokens (embeddings are cheap)
- **Vectorize**: Free tier includes 5M vector operations/month
- **Total**: Should be well under $25/month for moderate usage
