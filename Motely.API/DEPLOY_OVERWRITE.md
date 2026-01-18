# Deploy & Overwrite Old Prototype on balatrogenie.app

## Step 1: Check What's Currently Deployed

Go to https://dash.cloudflare.com → Workers & Pages

Look for:
- Old JAML Genie worker (might be named differently)
- Old MCP server worker

**Note the names** - we'll overwrite them or delete and redeploy.

## Step 2: Deploy JAML Genie (Overwrites Old One)

```powershell
cd external\Motely\Motely.API\cloudflare-worker-jamlgenie

# Make sure wrangler.toml has the right name
# It should be: name = "jamlgenie"

# Deploy (this will overwrite if name matches)
wrangler deploy
```

**If it asks about routes:**
- Say YES to overwrite existing routes
- Or manually update routes in Cloudflare dashboard

## Step 3: Deploy MCP Server (Overwrites Old One)

```powershell
cd ..\cloudflare-worker

# Make sure wrangler.jsonc has the right name
# It should be: "name": "balatro-seed-oracle-mcp"

# Deploy
wrangler deploy
```

## Step 4: Create/Update Vectorize Index

```powershell
# Check if index exists
wrangler vectorize list

# If it doesn't exist, create it:
wrangler vectorize create jaml-examples --dimensions=768 --metric=cosine

# If it exists but is wrong, delete and recreate:
# wrangler vectorize delete jaml-examples
# wrangler vectorize create jaml-examples --dimensions=768 --metric=cosine
```

## Step 5: Seed Vectorize Index (Production)

```powershell
cd cloudflare-worker-jamlgenie

# Set production URL
$env:JAMLGENIE_WORKER_URL="https://jamlgenie.balatrogenie.app"

# Seed it
node seed-vectorize.js
```

## Step 6: Test Production

**Test JAML Genie:**
```powershell
Invoke-RestMethod -Uri https://jamlgenie.balatrogenie.app -Method POST -ContentType "application/json" -Body '{"prompt":"Blueprint and Brainstorm"}'
```

**Test MCP Server:**
```powershell
Invoke-RestMethod -Uri https://mcp.balatrogenie.app -Method POST -ContentType "application/json" -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

## Step 7: Configure MCP in Cursor/Claude Desktop

Once deployed, you can add the MCP server to Cursor:

**For Cursor:**
Add to Cursor settings (MCP servers):
```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "command": "npx",
      "args": [
        "-y",
        "@modelcontextprotocol/server-everything"
      ],
      "env": {
        "MCP_SERVER_URL": "https://mcp.balatrogenie.app"
      }
    }
  }
}
```

**Or use HTTP transport:**
The MCP server is HTTP-based, so you might need to configure it differently. Check Cursor's MCP documentation for HTTP-based servers.

## Troubleshooting

**"Worker name conflict"**
- Delete old worker in Cloudflare dashboard first
- Or change name in wrangler config

**"Route already exists"**
- Update route in Cloudflare dashboard
- Or delete old route first

**"Vectorize index not found"**
- Make sure index name matches in wrangler.toml: `index_name = "jaml-examples"`
