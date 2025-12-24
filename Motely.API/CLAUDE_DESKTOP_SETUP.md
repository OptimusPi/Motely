# Claude Desktop MCP Server Setup

## What This Is

**Balatro Seed Oracle MCP Server** - A real MCP (Model Context Protocol) server that lets Claude Desktop (and other MCP clients) search for Balatro seeds using natural language.

**Server Name:** `balatro-seed-oracle`  
**Protocol Version:** 2024-11-05

---

## Installation for Claude Desktop

### Step 1: Find Claude Desktop Config

**macOS:**
```
~/Library/Application Support/Claude/claude_desktop_config.json
```

**Windows:**
```
%APPDATA%\Claude\claude_desktop_config.json
```

**Linux:**
```
~/.config/Claude/claude_desktop_config.json
```

### Step 2: Add MCP Server Configuration

Edit `claude_desktop_config.json` and add:

```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "X:\\BalatroSeedOracle\\external\\Motely\\Motely.API\\Motely.API.csproj",
        "--",
        "--host-api"
      ],
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**Important:** 
- Update the path to match your actual project location!
- The `--host-api` flag tells it to run API server mode
- Stdio mode is **automatically detected** when stdin is redirected (Claude Desktop does this)
- No need to set `ASPNETCORE_URLS` in stdio mode (no HTTP server needed)

### Step 3: Restart Claude Desktop

Close and reopen Claude Desktop for changes to take effect.

---

## Alternative: HTTP Transport (If stdio doesn't work)

If you prefer HTTP transport instead of stdio:

```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "url": "http://localhost:3141/mcp"
    }
  }
}
```

Then start the API server separately:
```bash
dotnet run --project Motely.API
```

---

## Available MCP Tools

Once connected, Claude can use these tools:

### 1. `generate_jaml_filter`
Generate JAML filter from natural language.

**Example:**
```
"Find me a seed with Blueprint and Brainstorm in Ante 1"
```

### 2. `search_seeds`
Search for seeds matching a JAML filter.

### 3. `get_search_status`
Check status of a running search.

### 4. `analyze_seed`
Analyze a specific Balatro seed to see all items.

---

## Usage Example

Once configured, you can ask Claude:

> "Use the balatro-seed-oracle MCP server to find me a seed with Perkeo and Negative tags in Ante 2"

Claude will:
1. Call `generate_jaml_filter` with your prompt
2. Get the JAML config
3. Call `search_seeds` to find matching seeds
4. Return the results

---

## Troubleshooting

### "Server not found"
- Check the path in `claude_desktop_config.json`
- Make sure `dotnet` is in your PATH
- Verify the project builds: `dotnet build Motely.API`

### "Connection refused"
- Make sure the API server is running
- Check the port (default: 3141)
- Verify firewall settings

### "Invalid JSON-RPC"
- Check Claude Desktop logs
- Verify MCP protocol version matches (2024-11-05)

---

## JamlGenie Frontend

The JamlGenie website (`/JamlGenie/`) is the **approved frontend** for this MCP server. It:
- Uses the same `/mcp/prompt` endpoint (legacy REST)
- Can be deployed separately to Cloudflare Pages
- Provides a user-friendly interface
- Shows search results and JAML configs

**Access:** `http://localhost:3141/JamlGenie/` (when API is running)

---

## Server Name Options

You can name it whatever you want in Claude Desktop config:

- `"balatro-seed-oracle"` ✅ (recommended)
- `"jaml-genie"` ✅
- `"balatro-mcp"` ✅
- `"jaml-mcp"` ✅

The name is just for display in Claude Desktop - it doesn't affect functionality.

---

## Next Steps

1. ✅ Configure Claude Desktop
2. ✅ Restart Claude Desktop
3. ✅ Test with: "Use balatro-seed-oracle to find a seed with Blueprint"
4. ✅ Enjoy AI-powered Balatro seed searching! 🎉

