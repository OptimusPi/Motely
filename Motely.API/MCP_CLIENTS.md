# MCP Clients That Can Use Balatro Seed Oracle

## ✅ Supported Clients

### 1. **Claude Desktop** (Anthropic)
- **Status:** ✅ Fully Supported (stdio + HTTP)
- **Setup:** See `CLAUDE_DESKTOP_SETUP.md`
- **Transport:** stdio (command-based) or HTTP (URL-based)
- **Protocol:** MCP 2024-11-05

### 2. **Cursor IDE** (Cursor AI)
- **Status:** ✅ Supported (HTTP transport)
- **Setup:** Add MCP server URL to Cursor settings
- **Transport:** HTTP
- **Config:** `http://localhost:3141/mcp`

### 3. **GitHub Copilot** (Microsoft)
- **Status:** ✅ Supported (HTTP transport)
- **Setup:** Configure MCP server in Copilot settings
- **Transport:** HTTP
- **Note:** May require Copilot Chat extension

### 4. **Any MCP-Compatible Client**
- **Status:** ✅ Supported
- **Protocol:** MCP 2024-11-05 (JSON-RPC 2.0)
- **Transport:** HTTP or stdio
- **Endpoint:** `/mcp` (HTTP) or stdio (command-based)

## Transport Modes

### HTTP Transport
- **URL:** `http://localhost:3141/mcp` (or your server URL)
- **Method:** POST
- **Format:** JSON-RPC 2.0
- **Works with:** Cursor, Copilot, web-based clients

### Stdio Transport
- **Method:** Command-based subprocess
- **Format:** Line-delimited JSON-RPC 2.0
- **Works with:** Claude Desktop (command mode)
- **Auto-detected:** When stdin is redirected

## Local Test First

Before any public deployment or registry listing, verify the local MCP endpoint:

```bash
# 1) Start the API (from repo root)
dotnet run --project external/Motely/Motely.API/Motely.API.csproj

# 2) Initialize
curl -X POST http://localhost:3141/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{}}}'

# 3) Tools list
curl -X POST http://localhost:3141/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'

# 4) Tool call (generate JAML)
curl -X POST http://localhost:3141/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"generate_jaml_filter","arguments":{"prompt":"Perkeo in Ante 1"}}}'
```

## Configuration Examples

### Claude Desktop (stdio)
```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "command": "dotnet",
      "args": ["run", "--project", "Motely.API/Motely.API.csproj"]
    }
  }
}
```

### Claude Desktop (HTTP)
```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "url": "http://localhost:3141/mcp"
    }
  }
}
```

### Cursor IDE
Add to Cursor settings:
```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "url": "http://localhost:3141/mcp"
    }
  }
}
```

## Available Tools

All clients can use these 4 MCP tools:

1. **`generate_jaml_filter`** - Generate JAML from natural language
2. **`search_seeds`** - Search for seeds matching JAML
3. **`get_search_status`** - Check search progress
4. **`analyze_seed`** - Analyze a specific seed

## Testing

Test with any MCP client:
```bash
curl -X POST http://localhost:3141/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "2024-11-05",
      "capabilities": {},
      "clientInfo": {
        "name": "test-client",
        "version": "1.0.0"
      }
    }
  }'
```

