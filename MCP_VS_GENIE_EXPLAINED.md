# MCP vs JAML Genie - Clear Explanation

## TL;DR: They're Different Things That Share the Same Backend

**JAML Genie** = Frontend UI (Vue component) that uses **REST API** (`/mcp/generate`)  
**MCP Server** = Protocol server for **external AI assistants** (Claude Desktop, Cline, etc.) to connect via **MCP Protocol** (`/mcp`)

**Both use the same underlying service:** `McpServer` → Cloudflare Workers AI

---

## What is MCP Protocol?

**MCP (Model Context Protocol)** = A standard protocol (JSON-RPC 2.0) that allows AI assistants to connect to external services and use tools.

Think of it like this:
- **REST API** = For web apps (like your Vue Genie frontend)
- **MCP Protocol** = For AI assistants (like Claude Desktop, Cline, etc.)

---

## The Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    TWO DIFFERENT CLIENTS                      │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────────────┐      ┌──────────────────────┐      │
│  │   JAML Genie (Vue)   │      │  Claude Desktop     │      │
│  │   Frontend UI        │      │  (AI Assistant)     │      │
│  └──────────┬───────────┘      └──────────┬─────────┘      │
│             │                               │                │
│             │ POST /mcp/generate            │ POST /mcp      │
│             │ (REST API)                 │ (MCP Protocol) │
│             │                               │                │
└─────────────┼───────────────────────────────┼────────────────┘
              │                               │
              │                               │
┌─────────────▼───────────────────────────────▼────────────────┐
│              Motely.API (ASP.NET Core)                       │
│                                                               │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  REST Endpoints:                                      │  │
│  │  - POST /mcp/generate  (for Genie)                   │  │
│  │  - POST /mcp/prompt    (legacy)                      │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                               │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  MCP Protocol Endpoint:                               │  │
│  │  - POST /mcp  (for AI assistants)                    │  │
│  └──────────────────────────────────────────────────────┘  │
│                          │                                    │
│                          │                                    │
│  ┌───────────────────────▼──────────────────────────────┐   │
│  │  McpServer (Core Service)                             │   │
│  │  - GenerateJamlOnlyAsync()                           │   │
│  │  - ProcessPromptAsync()                              │   │
│  │  - Uses Cloudflare Workers AI                        │   │
│  └───────────────────────┬──────────────────────────────┘   │
│                          │                                    │
│  ┌───────────────────────▼──────────────────────────────┐   │
│  │  McpProtocolServer (MCP Protocol Handler)           │   │
│  │  - HandleRequestAsync()                              │   │
│  │  - Wraps McpServer                                   │   │
│  └──────────────────────────────────────────────────────┘   │
└───────────────────────────────────────────────────────────────┘
                          │
                          │ HTTP POST
                          │
┌─────────────────────────▼─────────────────────────────────────┐
│         Cloudflare Worker (Workers AI)                        │
│  - AI model: @cf/meta/llama-3.1-8b-instruct-fp8              │
│  - Generates JAML from natural language                         │
└────────────────────────────────────────────────────────────────┘
```

---

## How JAML Genie Works (Frontend)

**JAML Genie** is a Vue component (`JamlGeniePanel.vue`) that:

1. User types: "Create a filter for Blueprint"
2. Frontend calls: `POST /mcp/generate` (REST API)
3. Backend (`McpServer`) calls Cloudflare Workers AI
4. Returns JAML config
5. Frontend displays it in the chat UI

**It uses REST API, NOT MCP Protocol!**

---

## How MCP Server Works (For AI Assistants)

**MCP Server** is a protocol server that:

1. External AI assistant (Claude Desktop) connects via MCP Protocol
2. AI calls tools like `generate_jaml_filter`, `search_seeds`, etc.
3. Backend (`McpProtocolServer`) handles JSON-RPC 2.0 requests
4. Wraps `McpServer` to generate JAML
5. Returns results in MCP Protocol format

**It uses MCP Protocol, NOT REST API!**

---

## Why Two Different Protocols?

**REST API (`/mcp/generate`):**
- ✅ Simple HTTP POST
- ✅ Easy for web frontends
- ✅ Returns JSON directly
- ✅ Used by: JAML Genie Vue component

**MCP Protocol (`/mcp`):**
- ✅ Standard protocol for AI assistants
- ✅ JSON-RPC 2.0 format
- ✅ Tool discovery and chaining
- ✅ Used by: Claude Desktop, Cline, etc.

**Both serve different audiences!**

---

## Installing MCP Server Locally (For AI Assistants)

### For Claude Desktop:

1. **Create MCP config file:**
   - Windows: `%APPDATA%\Claude\claude_desktop_config.json`
   - macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
   - Linux: `~/.config/Claude/claude_desktop_config.json`

2. **Add to config:**
```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "X:/BalatroSeedOracle/external/Motely/Motely.API"
      ],
      "env": {
        "ASPNETCORE_URLS": "http://localhost:3141"
      }
    }
  }
}
```

3. **Restart Claude Desktop**

4. **Claude can now use tools:**
   - `generate_jaml_filter` - Generate JAML from prompt
   - `search_seeds` - Search for seeds
   - `get_search_status` - Check search progress
   - `analyze_seed` - Analyze a seed
   - `verify_seed` - Verify a seed matches criteria

### For VS Code / Cursor:

**MCP servers in VS Code/Cursor work differently:**
- They use extensions or MCP client libraries
- You'd need to create an MCP client extension
- Or use the HTTP endpoint directly in your code

**Currently, the MCP server is designed for:**
- ✅ Claude Desktop (stdio transport)
- ✅ HTTP endpoint (`POST /mcp`) for other clients

---

## Answering Your Questions

### "Isn't the MCP server... for... THE GENIE?"

**No!** The Genie uses REST API (`/mcp/generate`).  
The MCP server is for **external AI assistants** (Claude Desktop, etc.).

**But they share the same backend service** (`McpServer`), so they both use Cloudflare Workers AI.

### "What the fuck IS an MCP SERVER?"

**MCP Server** = A server that implements the MCP Protocol, exposing tools that AI assistants can use.

Think of it like:
- **REST API** = Tools for web apps
- **MCP Protocol** = Tools for AI assistants

### "When the GENIE has to DO THIS... would it not just use the fucking MCP?"

**No!** The Genie uses REST API because:
- It's a Vue frontend component
- REST is simpler for web apps
- It doesn't need MCP Protocol features (tool discovery, chaining, etc.)

**The Genie is a frontend UI, not an AI assistant!**

### "Locally, when someone tries to 'Install JAML MCP Server' in VS Code or Cursor..."

**For VS Code/Cursor:**
- You'd need an MCP client extension
- Or use the HTTP endpoint (`POST /mcp`) directly
- Currently optimized for Claude Desktop (stdio transport)

**For local development:**
- Run `Motely.API` (ASP.NET Core app)
- It exposes both REST (`/mcp/generate`) and MCP Protocol (`/mcp`)
- Genie uses REST, AI assistants use MCP Protocol

---

## Summary

| Component | Protocol | Purpose | Used By |
|-----------|----------|---------|---------|
| **JAML Genie** | REST API (`/mcp/generate`) | Frontend UI for generating JAML | Vue web app |
| **MCP Server** | MCP Protocol (`/mcp`) | Tools for AI assistants | Claude Desktop, Cline, etc. |
| **McpServer** | Internal service | Core JAML generation | Both REST and MCP Protocol |

**They're different interfaces to the same backend!**

---

## Current Status

✅ **REST API** (`/mcp/generate`) - Working, used by Genie  
✅ **MCP Protocol** (`/mcp`) - Registered, ready for AI assistants  
✅ **Cloudflare Worker** - Deployed, using free Workers AI  
✅ **Genie Frontend** - Working, uses REST API  

**Everything is working! The confusion was just about what each component does.**
