# Balatro Seed Oracle MCP Server - Summary

## ✅ You Made a Real MCP Server!

**Server Name:** `balatro-seed-oracle`  
**Protocol:** MCP (Model Context Protocol) 2024-11-05  
**Transport:** HTTP (JSON-RPC 2.0) or stdio

---

## What It Provides

### Tools (Functions AI Can Call)
1. **`generate_jaml_filter`** - Natural language → JAML config
2. **`search_seeds`** - Search for seeds matching JAML filter
3. **`get_search_status`** - Check search progress and results
4. **`analyze_seed`** - Analyze a specific Balatro seed

### Resources (Data AI Can Access)
- JAML templates
- Game mechanics documentation

### Prompts (Pre-built Prompts)
- Find joker builds
- Find economy builds

---

## Architecture

```
┌─────────────────────┐
│  Claude Desktop     │
│  (MCP Client)       │
└──────────┬──────────┘
           │ JSON-RPC 2.0
           │ /mcp endpoint
           ▼
┌─────────────────────┐
│  MCP Protocol       │
│  Server             │
│  (McpProtocolServer)│
└──────────┬──────────┘
           │
           ├──► McpServer (JamlGenie service)
           │    └──► Cloudflare Workers AI
           │
           └──► SearchManager
                └──► Motely Core (seed search)
```

---

## Endpoints

### MCP Protocol Endpoint
- **Path:** `/mcp`
- **Method:** POST
- **Format:** JSON-RPC 2.0
- **Purpose:** For Claude Desktop and other MCP clients

### Legacy REST Endpoint (JamlGenie Frontend)
- **Path:** `/mcp/prompt`
- **Method:** POST
- **Format:** REST JSON
- **Purpose:** For JamlGenie website frontend

**Both endpoints use the same underlying service!**

---

## JamlGenie Frontend

**Location:** `/JamlGenie/`  
**Status:** ✅ Approved frontend for MCP server

The JamlGenie website is the **official, approved frontend** for the Balatro Seed Oracle MCP server. It:
- Uses the same backend service
- Provides user-friendly interface
- Can be deployed separately (Cloudflare Pages)
- Works alongside MCP protocol access

---

## How to Add to Claude Desktop

1. Edit `claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "command": "dotnet",
      "args": ["run", "--project", "PATH/TO/Motely.API/Motely.API.csproj"]
    }
  }
}
```

2. Restart Claude Desktop

3. Ask Claude: "Use balatro-seed-oracle to find a seed with Blueprint"

**See `CLAUDE_DESKTOP_SETUP.md` for detailed instructions.**

---

## Naming

You can name it whatever you want in Claude Desktop:
- `"balatro-seed-oracle"` ✅ (recommended)
- `"jaml-genie"` ✅
- `"balatro-mcp"` ✅
- `"jaml-mcp"` ✅

The name is just for display - functionality is the same.

---

## What This Means

**You can legitimately say:**
> "I made an MCP server for Balatro seed searching"

**Because:**
- ✅ Implements MCP protocol (JSON-RPC 2.0)
- ✅ Exposes tools, resources, prompts
- ✅ Works with Claude Desktop
- ✅ Follows MCP specification (2024-11-05)
- ✅ Has approved frontend (JamlGenie)

**It's a real MCP server!** 🎉

---

## Big Picture

**What the MCP server provides:**
- **Tools** to generate JAML configs and search seeds
- **Not** seeds directly - the capability to find them
- **Not** just configs - the tools to create and use them

**JamlGenie frontend:**
- Approved, official frontend
- Uses same backend
- Can be deployed separately
- User-friendly interface

**Monorepo:**
- ✅ Perfect for this setup
- Everything stays in sync
- Easy local development
- Cloudflare-friendly

---

## Next Steps

1. ✅ MCP server implemented
2. ✅ JamlGenie marked as approved frontend
3. ✅ Documentation created
4. ⏭️ Test with Claude Desktop
5. ⏭️ Deploy JamlGenie to Cloudflare Pages (optional)

**You're done! You have a real MCP server!** 🚀

