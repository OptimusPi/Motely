# Test Locally Before Deploying

## Step 1: Test JAML Genie Worker Locally

```bash
cd external/Motely/Motely.API/cloudflare-worker-jamlgenie

# Install dependencies
npm install

# Start local dev server (will prompt for Vectorize index creation)
wrangler dev
```

This starts the worker at `http://localhost:8787`

**Test it:**
```bash
# Test embedding generation
curl -X POST http://localhost:8787/embed \
  -H "Content-Type: application/json" \
  -d '{"text": "Blueprint and Brainstorm"}'

# Test JAML generation (without RAG first)
curl -X POST http://localhost:8787 \
  -H "Content-Type: application/json" \
  -d '{"prompt": "Blueprint and Brainstorm in Ante 1"}'
```

## Step 2: Create Vectorize Index (for RAG)

In a **new terminal**:
```bash
# Create the index
wrangler vectorize create jaml-examples \
  --dimensions=768 \
  --metric=cosine \
  --description="JAML filter examples for RAG"
```

## Step 3: Seed Vectorize Index Locally

```bash
cd external/Motely/Motely.API/cloudflare-worker-jamlgenie

# Make sure wrangler dev is still running in another terminal
# Then seed the index
export JAMLGENIE_WORKER_URL=http://localhost:8787
node seed-vectorize.js
```

## Step 4: Test RAG (Retrieval-Augmented Generation)

Now test with RAG enabled:
```bash
curl -X POST http://localhost:8787 \
  -H "Content-Type: application/json" \
  -d '{"prompt": "Blueprint and Brainstorm in Ante 1"}'
```

The response should include similar JAML examples in the context!

## Step 5: Test MCP Server Locally

```bash
cd external/Motely/Motely.API/cloudflare-worker

# Install dependencies
npm install

# Start local dev server
wrangler dev
```

**Test it:**
```bash
# Test MCP initialize
curl -X POST http://localhost:8788 \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {}
  }'

# Test tools list
curl -X POST http://localhost:8788 \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list"
  }'
```

## Step 6: Deploy to Production

Once local testing works:

```bash
cd external/Motely/Motely.API

# Make script executable (Linux/Mac)
chmod +x deploy.sh
./deploy.sh

# Or on Windows PowerShell:
# Just run each command from deploy.sh manually
```

## Troubleshooting

**"Vectorize not configured" error:**
- Make sure you created the index: `wrangler vectorize list`
- Check `wrangler.toml` has the Vectorize binding

**"No embedding generated":**
- Check Workers AI is enabled in your Cloudflare account
- Check Worker logs: `wrangler tail`

**"MOTELY_API_URL not configured":**
- Update `wrangler.jsonc` or `wrangler.toml` with your API URL
- For local testing, use `http://localhost:3141` (your local Motely API)
