# MCP Server Implementation Plan

## What We Have vs What We Need

### ✅ What We Have (Conceptually MCP-like)
- Natural language → JAML config generation
- JAML config → Seed search execution
- Validation and error handling
- Search result management

### ❌ What We Need (To Be Real MCP)
1. **JSON-RPC 2.0 Protocol** (not REST)
2. **MCP Initialization Handshake**
3. **Capabilities Declaration** (tools, resources, prompts)
4. **MCP Message Format** (standardized request/response)

---

## MCP Server Architecture

### Tools (Functions AI Can Call)
1. **`generate_jaml_filter`**
   - Input: `{ prompt: string }`
   - Output: `{ jaml: string, config: object, searchId: string }`
   - Description: "Generate JAML filter from natural language prompt"

2. **`search_seeds`**
   - Input: `{ jaml: string, deck?: string, stake?: string }`
   - Output: `{ searchId: string, results: array, status: string }`
   - Description: "Search for Balatro seeds matching JAML filter"

3. **`analyze_seed`**
   - Input: `{ seed: string, deck?: string, stake?: string }`
   - Output: `{ analysis: string, items: array }`
   - Description: "Analyze a specific Balatro seed"

4. **`get_search_status`**
   - Input: `{ searchId: string }`
   - Output: `{ status: string, results: array, progress: number }`
   - Description: "Get status of running search"

### Resources (Data AI Can Access)
1. **`jaml_templates`** - Example JAML filters
2. **`seed_results`** - Search results from searches
3. **`game_mechanics`** - Balatro game rules and mechanics

### Prompts (Pre-built Prompts)
1. **`find_joker_build`** - "Find seeds with specific joker combinations"
2. **`find_economy_build`** - "Find seeds with economy items"
3. **`find_boss_seed`** - "Find seeds with specific boss blinds"

---

## Implementation Steps

### Step 1: JSON-RPC 2.0 Handler
```csharp
// Handle JSON-RPC 2.0 messages
public class JsonRpcHandler
{
    public JsonRpcResponse Handle(JsonRpcRequest request)
    {
        // Parse JSON-RPC 2.0 format
        // Route to appropriate handler
        // Return JSON-RPC 2.0 response
    }
}
```

### Step 2: MCP Initialization
```csharp
// MCP initialization handshake
public class McpInitialization
{
    public McpServerInfo Initialize(McpClientInfo clientInfo)
    {
        return new McpServerInfo
        {
            ProtocolVersion = "2024-11-05",
            Capabilities = new McpCapabilities
            {
                Tools = GetAvailableTools(),
                Resources = GetAvailableResources(),
                Prompts = GetAvailablePrompts()
            }
        };
    }
}
```

### Step 3: Tool Handlers
```csharp
// Map MCP tools to existing functions
public class McpToolHandlers
{
    public async Task<object> HandleGenerateJamlFilter(JsonRpcRequest request)
    {
        // Call existing ProcessPromptAsync logic
        var result = await _mcpServer.ProcessPromptAsync(prompt);
        return new { jaml = result.JamlFilter, searchId = result.SearchId };
    }
    
    public async Task<object> HandleSearchSeeds(JsonRpcRequest request)
    {
        // Call existing SearchManager logic
        // Return search results
    }
}
```

### Step 4: MCP Endpoint
```csharp
// Replace REST endpoint with MCP endpoint
app.MapPost("/mcp", async (HttpRequest request) =>
{
    var jsonRpcRequest = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(request.Body);
    var handler = new JsonRpcHandler();
    var response = handler.Handle(jsonRpcRequest);
    return Results.Json(response);
});
```

---

## MCP Client Configuration

### For Claude Desktop
```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "command": "dotnet",
      "args": ["run", "--project", "Motely.API"],
      "env": {
        "MCP_MODE": "true"
      }
    }
  }
}
```

### For Cursor/Other MCP Clients
- Connect via stdio (stdin/stdout) or HTTP
- Use MCP protocol messages
- Access tools, resources, prompts

---

## What This Enables

**Before (REST API):**
- User → REST endpoint → JAML → Search
- Manual integration required

**After (MCP Server):**
- AI Assistant → MCP Server → Tools → JAML → Search
- AI can autonomously:
  - Generate JAML filters
  - Search for seeds
  - Analyze results
  - Use multiple tools in sequence

---

## Benefits

1. **AI Integration**: Claude, Cursor, and other MCP clients can use it
2. **Autonomous Operation**: AI can chain tools together
3. **Standardized Protocol**: Works with any MCP-compatible client
4. **Tool Discovery**: AI discovers available tools automatically
5. **Context Awareness**: MCP maintains context across interactions

---

## Next Steps

1. Implement JSON-RPC 2.0 handler
2. Add MCP initialization
3. Expose tools as MCP capabilities
4. Test with Claude Desktop or Cursor
5. Document MCP server configuration

**Result**: You can legitimately say "I made an MCP server" because it will be a real MCP protocol implementation! 🎉

