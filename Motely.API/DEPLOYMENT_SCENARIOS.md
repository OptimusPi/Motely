# Deployment Scenarios

This document explains how to deploy Motely.API in different configurations using feature flags.

## Feature Flags

All features can be enabled/disabled via `appsettings.json`:

```json
{
  "Features": {
    "EnableSearchQueue": true,   // Multiplayer seed search queue
    "EnableSignalR": true,       // Real-time updates via SignalR
    "EnableMcp": true,           // MCP protocol for AI assistants
    "EnableSwagger": true        // API documentation
  }
}
```

## Deployment Scenarios

### 1. Full API (Everything Enabled)
**Use case:** Main production server with all features

```bash
dotnet run --configuration Release
```

**Features:**
- ✅ Core API endpoints (filters, analyze, seed-sources)
- ✅ Search queue endpoints (multiplayer searches)
- ✅ SignalR hub (real-time updates)
- ✅ MCP endpoints (AI assistant integration)
- ✅ Swagger documentation

**Ports:**
- HTTP: 3141
- SignalR: /searchHub

---

### 2. MCP-Only Server
**Use case:** Lightweight server for AI assistants (Claude Desktop, Cursor)

```bash
dotnet run --configuration Release -- --environment McpOnly
# Or use: ASPNETCORE_ENVIRONMENT=McpOnly dotnet run
```

**Configuration:** `appsettings.McpOnly.json`

**Features:**
- ✅ Core API endpoints (filters, analyze)
- ✅ MCP endpoints (`/mcp`, `/mcp/prompt`, `/mcp/generate`)
- ❌ Search queue (disabled)
- ❌ SignalR (disabled)
- ❌ Swagger (disabled)

**Deploy as:** Cloudflare Worker, lightweight container, or serverless function

---

### 3. API-Only Server (No MCP)
**Use case:** Web UI server without AI assistant features

```bash
dotnet run --configuration Release -- --environment ApiOnly
```

**Configuration:** `appsettings.ApiOnly.json`

**Features:**
- ✅ Core API endpoints
- ✅ Search queue endpoints
- ✅ SignalR hub
- ❌ MCP endpoints (disabled)

**Useful for:** Reducing attack surface, smaller deployments

---

### 4. Search Queue Worker
**Use case:** Background worker for processing search queue

**Configuration:**
```json
{
  "Features": {
    "EnableSearchQueue": true,
    "EnableSignalR": false,
    "EnableMcp": false,
    "EnableSwagger": false
  }
}
```

**Features:**
- ✅ Search queue processing
- ✅ Core API endpoints (for status checks)
- ❌ SignalR (not needed for worker)
- ❌ MCP (not needed for worker)

**Deploy as:** Background service, Kubernetes job, or separate container

---

## Testing Different Configurations

### Test MCP-Only Locally
```bash
# Set environment variable
$env:ASPNETCORE_ENVIRONMENT="McpOnly"
dotnet run

# Test MCP endpoint
curl -X POST http://localhost:3141/mcp -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

### Test API-Only Locally
```bash
$env:ASPNETCORE_ENVIRONMENT="ApiOnly"
dotnet run

# Test search endpoint
curl -X POST http://localhost:3141/search -H "Content-Type: application/json" -d '{"filterId":"test","deck":"Red","stake":"White"}'
```

---

## Docker Deployment

### Full API
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
COPY . /app
WORKDIR /app
ENTRYPOINT ["dotnet", "Motely.API.dll"]
```

### MCP-Only
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
COPY . /app
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=McpOnly
ENTRYPOINT ["dotnet", "Motely.API.dll"]
```

---

## Benefits of Modular Architecture

1. **Independent Testing:** Test MCP endpoints without search queue overhead
2. **Independent Deployment:** Deploy MCP server separately from main API
3. **Resource Optimization:** Disable unused features to reduce memory/CPU
4. **Security:** Reduce attack surface by disabling unused endpoints
5. **Scalability:** Scale MCP server separately from search queue

---

## Migration Guide

### From Monolithic to Modular

**Before:**
- All features always enabled
- Hard to test/deploy independently

**After:**
- Features controlled by `appsettings.json`
- Easy to create specialized deployments
- Better separation of concerns

**No code changes needed!** Just update `appsettings.json` to enable/disable features.
