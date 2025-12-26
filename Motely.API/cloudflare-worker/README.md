# Balatro Seed Oracle MCP Server - Cloudflare Worker

A Cloudflare Worker implementation of the Balatro Seed Oracle MCP server, following [Cloudflare's Workers guidelines](https://developers.cloudflare.com/workers/prompt.txt).

## Overview

This Worker acts as a proxy to your Motely.API backend, implementing the MCP (Model Context Protocol) 2024-11-05 specification. It provides a hosted, always-available endpoint for Claude Desktop and other MCP clients.

## Features

- ✅ **MCP Protocol 2024-11-05** - Full compliance with MCP specification
- ✅ **JSON-RPC 2.0** - Standard request/response format
- ✅ **Edge Deployment** - Low latency worldwide
- ✅ **CORS Support** - Works with web-based MCP clients
- ✅ **Error Handling** - Proper JSON-RPC error responses
- ✅ **TypeScript** - Fully typed implementation

## Prerequisites

1. **Cloudflare Account** - Sign up at [cloudflare.com](https://cloudflare.com)
2. **Wrangler CLI** - Install: `npm install -g wrangler`
3. **Motely.API Backend** - Your .NET API must be running and accessible

## Setup

### 1. Install Dependencies

```bash
cd cloudflare-worker
npm install
```

### 2. Configure Backend URL

Edit `wrangler.jsonc` and set your `MOTELY_API_URL`:

```jsonc
{
  "vars": {
    "MOTELY_API_URL": "https://your-api.com"
    // Or for local development:
    // "MOTELY_API_URL": "http://localhost:3141"
  }
}
```

### 3. Deploy to Cloudflare

```bash
# Login to Cloudflare
wrangler login

# Deploy the Worker
wrangler deploy
```

### 4. Get Your Worker URL

After deployment, you'll get a URL like:
```
https://balatro-seed-oracle-mcp.your-subdomain.workers.dev
```

## Usage with Claude Desktop

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "url": "https://balatro-seed-oracle-mcp.your-subdomain.workers.dev"
    }
  }
}
```

## Environment Variables

### Required

- `MOTELY_API_URL` - URL of your Motely.API backend (e.g., `https://api.example.com`)

### Optional

- `API_KEY` - API key for backend authentication (if your backend requires it)

## Local Development

### 1. Start Your Backend

```bash
# In your Motely.API directory
dotnet run
```

### 2. Run Worker Locally

```bash
cd cloudflare-worker
wrangler dev
```

The Worker will proxy requests to `http://localhost:3141` (or your configured URL).

## Testing

### Test MCP Initialize

```bash
curl -X POST https://your-worker.workers.dev \
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

### Test Tools List

```bash
curl -X POST https://your-worker.workers.dev \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list"
  }'
```

### Test Tool Call

```bash
curl -X POST https://your-worker.workers.dev \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
      "name": "generate_jaml_filter",
      "arguments": {
        "prompt": "Blueprint and Brainstorm"
      }
    }
  }'
```

## Architecture

```
┌─────────────────────┐
│  Claude Desktop     │
│  (MCP Client)       │
└──────────┬──────────┘
           │ JSON-RPC 2.0
           │ HTTPS
           ▼
┌─────────────────────┐
│  Cloudflare Worker  │
│  (MCP Proxy)        │
└──────────┬──────────┘
           │ HTTP/HTTPS
           │ JSON-RPC 2.0
           ▼
┌─────────────────────┐
│  Motely.API         │
│  (Backend)          │
└─────────────────────┘
```

## MCP Tools

The Worker exposes these tools (proxied to backend):

1. **`generate_jaml_filter`** - Generate JAML from natural language
2. **`search_seeds`** - Search for seeds matching JAML
3. **`get_search_status`** - Check search progress
4. **`analyze_seed`** - Analyze a specific seed

## Error Handling

The Worker follows JSON-RPC 2.0 error codes:

- `-32700` - Parse error
- `-32600` - Invalid Request
- `-32601` - Method not found
- `-32602` - Invalid params
- `-32603` - Internal error

## Security

- **CORS** - Configured to allow all origins (adjust for production)
- **API Key** - Optional authentication via `API_KEY` env var
- **HTTPS Only** - Cloudflare Workers enforce HTTPS

## Cost

- **Free Tier**: 100,000 requests/day
- **Paid**: $5/month + $0.50 per million requests after free tier

## Troubleshooting

### Backend Not Reachable

Ensure your `MOTELY_API_URL` is:
- Accessible from the internet (Cloudflare Workers can't reach localhost)
- Using HTTPS (or HTTP for development)
- Not blocked by firewall

### CORS Errors

The Worker includes CORS headers. If you see CORS errors:
1. Check browser console for specific error
2. Verify `Access-Control-Allow-Origin` header is present
3. Ensure preflight OPTIONS requests are handled

### Tool Calls Failing

1. Check Worker logs: `wrangler tail`
2. Verify backend `/mcp` endpoint is working
3. Check backend logs for errors

## References

- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [Cloudflare Workers Docs](https://developers.cloudflare.com/workers/)
- [Cloudflare Workers Prompt](https://developers.cloudflare.com/workers/prompt.txt)
- [Wrangler CLI](https://developers.cloudflare.com/workers/wrangler/)

