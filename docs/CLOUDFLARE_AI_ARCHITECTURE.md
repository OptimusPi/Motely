# Cloudflare Workers AI Architecture - MCP vs Genie

## 🎯 Overview

**Cloudflare Workers AI is the AI engine** that generates JAML filters from natural language. Both **Genie (REST)** and **MCP Protocol** use the same underlying `McpServer` class, which calls Cloudflare Workers AI.

---

## 📊 Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Frontend Clients                          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐              ┌──────────────────────┐   │
│  │  Genie UI    │              │  MCP Clients         │   │
│  │  (Vue 3)     │              │  (Claude Desktop)    │   │
│  │              │              │  (Cline, etc.)       │   │
│  └──────┬───────┘              └──────────┬───────────┘   │
│         │                                  │                │
│         │ POST /mcp/generate               │ POST /mcp     │
│         │ POST /mcp/prompt                │ (JSON-RPC)    │
│         │ (REST API)                       │               │
│         │                                  │               │
└─────────┼──────────────────────────────────┼───────────────┘
          │                                  │
          │                                  │
┌─────────▼──────────────────────────────────▼──────────────────┐
│              Motely.API (ASP.NET Core)                       │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Endpoints:                                           │   │
│  │  - POST /mcp/generate  (REST - for Genie)            │   │
│  │  - POST /mcp/prompt    (REST - for Genie)            │   │
│  │  - POST /mcp           (JSON-RPC - for MCP clients) │   │
│  └──────────────────────────────────────────────────────┘   │
│                        │                                      │
│                        │ Both use same service:               │
│                        ▼                                      │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  McpServer (Core Service)                             │   │
│  │  - GenerateJamlOnlyAsync()                            │   │
│  │  - ProcessPromptAsync()                                │   │
│  │                                                        │   │
│  │  ┌──────────────────────────────────────────────┐    │   │
│  │  │  GenerateJamlWithAIAsync()                   │    │   │
│  │  │  ↓                                            │    │   │
│  │  │  HTTP POST to Cloudflare Worker URL          │    │   │
│  │  │  { "prompt": "Blueprint in Ante 1" }        │    │   │
│  │  └──────────────────────────────────────────────┘    │   │
│  └──────────────────────────────────────────────────────┘   │
│                        │                                      │
│                        │ HTTP POST                            │
│                        ▼                                      │
└───────────────────────────────────────────────────────────────┘
                        │
                        │
┌───────────────────────▼───────────────────────────────────────┐
│         Cloudflare Workers (Edge Network)                      │
│  ┌──────────────────────────────────────────────────────┐     │
│  │  JamlGenie Worker                                     │     │
│  │  (cloudflare-worker-jamlgenie/)                      │     │
│  │                                                       │     │
│  │  Receives: { "prompt": "..." }                       │     │
│  │                                                       │     │
│  │  ┌─────────────────────────────────────────────┐    │     │
│  │  │  Workers AI Binding (env.AI)                │    │     │
│  │  │  ↓                                           │    │     │
│  │  │  Calls: env.AI.run()                         │    │     │
│  │  │  Model: @cf/meta/llama-3.1-8b-instruct-fp8  │    │     │
│  │  │  System Prompt: (hardcoded in Worker)        │    │     │
│  │  │  User Prompt: (from request)                 │    │     │
│  │  └─────────────────────────────────────────────┘    │     │
│  │                                                       │     │
│  │  Returns: { "success": true, "jaml": "..." }        │     │
│  └──────────────────────────────────────────────────────┘     │
└───────────────────────────────────────────────────────────────┘
                        │
                        │ HTTP Response
                        │ { "success": true, "jaml": "..." }
                        │
┌───────────────────────▼───────────────────────────────────────┐
│              Motely.API (Backend)                              │
│  ┌──────────────────────────────────────────────────────┐     │
│  │  McpServer processes response:                       │     │
│  │  - Validates JAML                                    │     │
│  │  - Cleans markdown                                  │     │
│  │  - Adds JAML header                                 │     │
│  │  - Returns to caller                                │     │
│  └──────────────────────────────────────────────────────┘     │
│                        │                                      │
│                        │ Returns JAML                          │
│                        ▼                                      │
└───────────────────────────────────────────────────────────────┘
                        │
                        │
┌───────────────────────▼───────────────────────────────────────┐
│                    Frontend Clients                            │
│  - Genie UI receives JAML → displays in editor                 │
│  - MCP Client receives JAML → can use in conversation         │
└───────────────────────────────────────────────────────────────┘
```

---

## 🔄 Two Paths, One AI Engine

### Path 1: Genie (REST API)
```
Vue Genie Panel
  → POST /mcp/generate
    → McpServer.GenerateJamlOnlyAsync()
      → GenerateJamlWithAIAsync()
        → HTTP POST to Cloudflare Worker
          → Workers AI (env.AI.run())
            → Returns JAML
```

### Path 2: MCP Protocol (JSON-RPC 2.0)
```
Claude Desktop / Cline
  → POST /mcp (JSON-RPC)
    → McpProtocolServer.HandleRequestAsync()
      → HandleToolCall() → "generate_jaml_filter"
        → McpServer.GenerateJamlOnlyAsync()
          → GenerateJamlWithAIAsync()
            → HTTP POST to Cloudflare Worker
              → Workers AI (env.AI.run())
                → Returns JAML
```

**Key Point:** Both paths use **the exact same `McpServer` class** and **the same Cloudflare Worker URL**.

---

## 🎯 Where Cloudflare Workers AI Fits

### 1. **Cloudflare Worker** (`cloudflare-worker-jamlgenie/`)
- **Location:** `Motely.API/cloudflare-worker-jamlgenie/src/index.ts`
- **Purpose:** Receives HTTP POST requests with prompts
- **AI Binding:** Uses `env.AI` (Workers AI binding - no API keys needed)
- **Model:** `@cf/meta/llama-3.1-8b-instruct-fp8` (default)
- **System Prompt:** Hardcoded in Worker (includes JAML schema, game mechanics)
- **Returns:** `{ success: true, jaml: "...", reasoning: "..." }`

### 2. **McpServer** (`Motely.API/McpServer.cs`)
- **Purpose:** Orchestrates JAML generation
- **Calls:** Cloudflare Worker via HTTP POST
- **Configuration:** `appsettings.json` → `Cloudflare:WorkersAI:WorkerUrl`
- **Methods:**
  - `GenerateJamlOnlyAsync()` - Generate JAML only (no search)
  - `ProcessPromptAsync()` - Generate JAML + optionally start search
  - `GenerateJamlWithAIAsync()` - Internal method that calls Worker

### 3. **Two Entry Points:**

#### A. **Genie (REST)**
- **Endpoints:** `/mcp/generate`, `/mcp/prompt`
- **Used By:** Vue Genie Panel (`JamlGeniePanel.vue`)
- **Format:** REST JSON
- **Flow:** Frontend → REST endpoint → `McpServer` → Cloudflare Worker

#### B. **MCP Protocol (JSON-RPC 2.0)**
- **Endpoint:** `/mcp`
- **Used By:** Claude Desktop, Cline, other MCP clients
- **Format:** JSON-RPC 2.0
- **Flow:** MCP Client → `McpProtocolServer` → `McpServer` → Cloudflare Worker

---

## 🔑 Key Differences: Genie vs MCP

| Aspect | Genie (REST) | MCP Protocol |
|--------|--------------|--------------|
| **Protocol** | REST (HTTP POST) | JSON-RPC 2.0 |
| **Endpoint** | `/mcp/generate`, `/mcp/prompt` | `/mcp` |
| **Request Format** | `{ "prompt": "..." }` | `{ "jsonrpc": "2.0", "method": "tools/call", ... }` |
| **Response Format** | `{ "success": true, "jaml": "..." }` | `{ "jsonrpc": "2.0", "result": { ... } }` |
| **Used By** | Vue Genie Panel (web UI) | Claude Desktop, Cline (AI assistants) |
| **AI Engine** | ✅ Same: Cloudflare Workers AI | ✅ Same: Cloudflare Workers AI |
| **Core Service** | ✅ Same: `McpServer` | ✅ Same: `McpServer` |

---

## 💡 The Shared Component

**`McpServer` is the shared service** that both Genie and MCP Protocol use:

```csharp
// Used by BOTH Genie and MCP Protocol
public class McpServer
{
    // Called by Genie REST endpoint
    public async Task<(string jaml, string reasoning, string? error)> 
        GenerateJamlOnlyAsync(string prompt)
    {
        // Calls Cloudflare Worker
        var jaml = await GenerateJamlWithAIAsync(prompt);
        // ... validation, cleaning, etc.
        return (jaml, reasoning, error);
    }
    
    // Called by MCP Protocol Server
    // (same method, different entry point)
}
```

---

## 🎯 Cloudflare Workers AI Role

**Cloudflare Workers AI is the AI engine** that:
1. Runs on Cloudflare's edge network (fast, global)
2. Uses Workers AI binding (`env.AI`) - no API keys needed
3. Has system prompt hardcoded (security best practice)
4. Generates JAML from natural language prompts
5. Returns structured JSON response

**It's called by:**
- `McpServer.GenerateJamlWithAIAsync()` → HTTP POST to Worker URL
- Worker receives prompt → Calls `env.AI.run()` → Returns JAML

---

## 📝 Configuration

**`appsettings.json`:**
```json
{
  "Cloudflare": {
    "WorkersAI": {
      "WorkerUrl": "https://jamlgenie.optimuspi.workers.dev",
      "Model": "@cf/meta/llama-3.1-8b-instruct-fp8"
    }
  }
}
```

**Both Genie and MCP Protocol read from the same config** - they use the same Worker URL.

---

## 🔄 Complete Flow Example

### Genie Flow:
1. User types: "Blueprint in Ante 1" in Vue Genie Panel
2. Frontend calls: `POST /mcp/generate` with `{ "prompt": "..." }`
3. `MotelyApiHost.cs` → `McpServer.GenerateJamlOnlyAsync()`
4. `McpServer` → HTTP POST to `https://jamlgenie.optimuspi.workers.dev`
5. Cloudflare Worker → `env.AI.run()` with system prompt + user prompt
6. Workers AI → Generates JAML
7. Worker → Returns `{ "success": true, "jaml": "..." }`
8. `McpServer` → Validates, cleans, returns JAML
9. Frontend → Displays JAML in editor

### MCP Protocol Flow:
1. Claude Desktop calls: `POST /mcp` with JSON-RPC request
2. `McpProtocolServer.HandleRequestAsync()` → Routes to `HandleToolCall()`
3. `HandleToolCall()` → Calls `McpServer.GenerateJamlOnlyAsync()`
4. **Same steps 4-8 as Genie flow**
5. `McpProtocolServer` → Wraps in JSON-RPC response
6. Claude Desktop → Receives JAML, can use in conversation

---

## 🎯 Summary

**Cloudflare Workers AI is the shared AI engine** used by both:
- ✅ **Genie (REST)** - via `/mcp/generate` endpoint
- ✅ **MCP Protocol** - via `/mcp` JSON-RPC endpoint

**Both paths:**
1. Use the same `McpServer` class
2. Call the same Cloudflare Worker URL
3. Use the same Workers AI model
4. Get the same JAML generation quality

**The only difference is the API protocol:**
- Genie = REST (simpler, for web UIs)
- MCP = JSON-RPC 2.0 (standard, for AI assistants)

**Cloudflare Workers AI sits in the middle** - it's the actual AI that generates JAML from natural language, regardless of which entry point is used.

---

**Last Updated:** 2024-12-26
