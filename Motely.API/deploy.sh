#!/bin/bash
# Quick deployment script for both workers to balatrogenie.app

set -e

echo "🚀 Deploying to balatrogenie.app..."

# Step 1: Create Vectorize index (if not exists)
echo ""
echo "📊 Step 1: Creating Vectorize index..."
wrangler vectorize create jaml-examples \
  --dimensions=768 \
  --metric=cosine \
  --description="JAML filter examples for RAG" \
  2>&1 | grep -v "already exists" || echo "Index already exists, skipping..."

# Step 2: Deploy JAML Genie Worker
echo ""
echo "🤖 Step 2: Deploying JAML Genie Worker..."
cd cloudflare-worker-jamlgenie
npm install
wrangler deploy
cd ..

# Step 3: Seed Vectorize Index
echo ""
echo "🌱 Step 3: Seeding Vectorize index..."
cd cloudflare-worker-jamlgenie
export JAMLGENIE_WORKER_URL=https://jamlgenie.balatrogenie.app
node seed-vectorize.js
cd ..

# Step 4: Deploy MCP Server Worker
echo ""
echo "🔌 Step 4: Deploying MCP Server Worker..."
cd cloudflare-worker
npm install
wrangler deploy
cd ..

echo ""
echo "✅ Deployment complete!"
echo ""
echo "Workers deployed:"
echo "  - JAML Genie: https://jamlgenie.balatrogenie.app"
echo "  - MCP Server: https://mcp.balatrogenie.app"
echo ""
echo "Test JAML Genie:"
echo "  curl -X POST https://jamlgenie.balatrogenie.app -H 'Content-Type: application/json' -d '{\"prompt\":\"Blueprint and Brainstorm\"}'"
echo ""
echo "Test MCP Server:"
echo "  curl -X POST https://mcp.balatrogenie.app -H 'Content-Type: application/json' -d '{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}'"
