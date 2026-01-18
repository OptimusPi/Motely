# Test the MCP Server We Made! 🎉

## Quick Test (Right Now!)

### Test via HTTP (Works Immediately)

```powershell
# Test MCP initialize
Invoke-RestMethod -Uri https://mcp.balatrogenie.app -Method POST -ContentType "application/json" -Body '{"jsonrpc":"2.0","id":1,"method":"initialize"}'

# Test tools list
Invoke-RestMethod -Uri https://mcp.balatrogenie.app -Method POST -ContentType "application/json" -Body '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'

# Test generate_jaml_filter tool
Invoke-RestMethod -Uri https://mcp.balatrogenie.app -Method POST -ContentType "application/json" -Body '{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "generate_jaml_filter",
    "arguments": {
      "prompt": "Blueprint and Brainstorm in Ante 1"
    }
  }
}'
```

## Use MCP Server in Cursor (After Deployment)

Once deployed, you can use it directly in Cursor! Here's how:

### Option 1: HTTP Transport (Easiest)

Add to Cursor settings (`.cursor/mcp.json` or Cursor settings UI):

```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "url": "https://mcp.balatrogenie.app",
      "transport": "http"
    }
  }
}
```

### Option 2: Test Locally First

If your API is running locally:

```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "url": "http://localhost:3141/mcp",
      "transport": "http"
    }
  }
}
```

## Available MCP Tools

Once configured, you can use these tools in Cursor:

1. **`generate_jaml_filter`** - Generate JAML from natural language
   - Example: "Blueprint and Brainstorm in Ante 1"
   - Returns: JAML filter config

2. **`search_seeds`** - Search for seeds matching a JAML filter
   - Takes: JAML filter string
   - Returns: Search ID and initial results

3. **`get_search_status`** - Check search progress
   - Takes: Search ID
   - Returns: Current status and results

4. **`analyze_seed`** - Analyze a specific seed
   - Takes: Seed string (e.g., "ALEEB")
   - Returns: Full seed analysis

## Test It Right Now!

**After deploying, run:**

```powershell
cd external\Motely\Motely.API
.\deploy-overwrite.ps1
```

This will:
1. ✅ Deploy JAML Genie worker
2. ✅ Create/update Vectorize index
3. ✅ Seed Vectorize with your JAML files
4. ✅ Deploy MCP Server worker
5. ✅ Test both workers automatically

## What You'll See

**MCP Server Response (tools/list):**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "tools": [
      {
        "name": "generate_jaml_filter",
        "description": "Generate a JAML filter from natural language...",
        "inputSchema": { ... }
      },
      {
        "name": "search_seeds",
        "description": "Search for Balatro seeds matching a JAML filter...",
        "inputSchema": { ... }
      },
      ...
    ]
  }
}
```

**JAML Genie Response:**
```json
{
  "success": true,
  "jaml": "name: Blueprint Brainstorm\ndeck: Red\nstake: White\nmust:\n  - joker: Blueprint\n    antes: [1, 2, 3]\n  - joker: Brainstorm\n    antes: [1, 2, 3]\nshould: []\nmustNot: []\n"
}
```

## Troubleshooting

**"MOTELY_API_URL not configured"**
- Update `wrangler.jsonc` with your actual API URL
- For local testing: `http://localhost:3141`
- For production: `https://api.balatrogenie.app`

**"Worker not found"**
- Check Cloudflare dashboard: https://dash.cloudflare.com → Workers & Pages
- Make sure worker names match in `wrangler.toml`/`wrangler.jsonc`

**"CORS error"**
- MCP server should handle CORS automatically
- Check Worker logs: `wrangler tail`

## Next: Use It in Cursor!

Once deployed and tested, you can actually **use the MCP tools in Cursor**:

1. Configure MCP server in Cursor settings
2. Restart Cursor
3. Use tools like: "Generate a JAML filter for Blueprint and Brainstorm"
4. Cursor will call the MCP server automatically!

🎉 **YES, IT'S POSSIBLE TO TEST IT RIGHT NOW!** Just deploy and configure!
