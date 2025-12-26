# Cloudflare MCP Server Deployment Guide

## Overview

Based on [Cloudflare's MCP documentation](https://developers.cloudflare.com/llms.txt), you can deploy the Balatro Seed Oracle MCP server as a **Cloudflare Worker**, providing a hosted, always-available endpoint for Claude Desktop and other MCP clients.

## Why Deploy on Cloudflare?

### ✅ Advantages

1. **No User Installation Required**
   - Users just add a URL to Claude Desktop config
   - No need to run `dotnet run` or install dependencies
   - Works on any device (Windows, Mac, Linux, mobile)

2. **Edge Deployment**
   - Low latency worldwide
   - Automatic scaling
   - 99.9% uptime SLA

3. **Free Tier**
   - Generous free limits (100K requests/day)
   - Perfect for personal/small-scale use
   - Pay-as-you-go pricing if you exceed

4. **Already Using Cloudflare**
   - Your Workers AI integration is already set up
   - Same infrastructure, same account
   - Consistent deployment process

5. **Built-in MCP Support**
   - Cloudflare has MCP SDK/helpers
   - Less boilerplate code
   - Better integration

## Architecture Options

### Option 1: Full Worker Deployment (Recommended)

**What it means:**
- Entire MCP server runs as Cloudflare Worker
- All logic in TypeScript/JavaScript
- Connects to your existing Motely.API backend via HTTP

**Pros:**
- Fully serverless
- No backend required for MCP
- Fastest deployment

**Cons:**
- Need to port C# logic to TypeScript
- Can't use .NET libraries directly

**Implementation:**
```typescript
// worker.ts
import { createMcpHandler } from '@cloudflare/agents';

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const handler = createMcpHandler({
      tools: {
        generate_jaml_filter: async (args) => {
          // Call your Motely.API backend
          const response = await fetch('https://your-api.com/mcp/prompt', {
            method: 'POST',
            body: JSON.stringify({ prompt: args.prompt })
          });
          return await response.json();
        },
        // ... other tools
      }
    });
    return handler(request);
  }
};
```

### Option 2: Proxy Worker (Easier)

**What it means:**
- Worker acts as HTTP proxy to your existing .NET MCP server
- Minimal code changes
- Keep existing C# implementation

**Pros:**
- No code porting needed
- Keep existing .NET codebase
- Quick to deploy

**Cons:**
- Still need to run .NET server somewhere
- Adds latency (extra hop)

**Implementation:**
```typescript
// worker.ts - Simple proxy
export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    // Proxy to your existing /mcp endpoint
    const backendUrl = env.MOTELY_API_URL || 'https://your-api.com';
    const url = new URL(request.url);
    const backendRequest = new Request(
      `${backendUrl}/mcp${url.pathname}`,
      {
        method: request.method,
        headers: request.headers,
        body: request.body
      }
    );
    return fetch(backendRequest);
  }
};
```

### Option 3: Hybrid Approach (Best of Both)

**What it means:**
- Deploy Cloudflare Worker for hosted option
- Keep .NET version for self-hosted users
- Users choose based on preference

**Pros:**
- Maximum flexibility
- Works for all use cases
- Future-proof

**Cons:**
- Maintain two codebases
- More documentation needed

## Cloudflare Resources

### Documentation Links

1. **[Build a Remote MCP server](https://developers.cloudflare.com/agents/guides/remote-mcp-server/index.md)**
   - Step-by-step guide for deploying MCP servers
   - Code examples and best practices

2. **[MCP server portals](https://developers.cloudflare.com/agents/model-context-protocol/mcp-portal/index.md)**
   - Centralize multiple MCP servers
   - Customize available tools/resources

3. **[MCP Transport](https://developers.cloudflare.com/agents/model-context-protocol/transport/index.md)**
   - HTTP and SSE transport options
   - Authentication and security

4. **[Cloudflare's MCP servers](https://developers.cloudflare.com/agents/model-context-protocol/mcp-servers-for-cloudflare/index.md)**
   - Examples of Cloudflare's own MCP servers
   - Reference implementations

## Implementation Steps

### Step 1: Choose Architecture

**Recommendation:** Start with **Option 2 (Proxy Worker)** for quick deployment, then migrate to **Option 1** if needed.

### Step 2: Create Worker

```bash
# Create new Worker project
npx wrangler init balatro-seed-oracle-mcp
cd balatro-seed-oracle-mcp
```

### Step 3: Implement MCP Handler

Use Cloudflare's MCP SDK or implement JSON-RPC 2.0 directly.

### Step 4: Deploy

```bash
# Deploy to Cloudflare
npx wrangler deploy
```

### Step 5: Update Claude Desktop Config

Users can now use:
```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "url": "https://balatro-seed-oracle.workers.dev"
    }
  }
}
```

## Cost Estimate

### Free Tier (Sufficient for Most Users)
- **100,000 requests/day** - Free
- **CPU time:** 10ms per request (generous)
- **Bandwidth:** Included

### Paid (If You Exceed)
- **$5/month** for Workers Paid plan
- **$0.50 per million requests** after free tier
- **Very affordable** for personal/small projects

## Comparison: Standalone vs Cloudflare

| Feature | Standalone .NET | Cloudflare Worker |
|---------|----------------|-------------------|
| **Installation** | User must install | Just add URL |
| **Availability** | User's machine must be on | Always available |
| **Scaling** | Limited by user's machine | Automatic |
| **Maintenance** | User updates | You update |
| **Cost** | Free (user's resources) | Free tier available |
| **Latency** | Local (fast) | Edge (very fast) |
| **Setup Complexity** | Medium | Low |

## Recommendation

**Deploy both:**
1. **Cloudflare Worker** - For most users (easiest)
2. **Standalone .NET** - For power users who want self-hosted

This gives maximum flexibility and covers all use cases.

## Next Steps

1. ✅ Review Cloudflare MCP documentation
2. ⏭️ Create Worker project
3. ⏭️ Implement MCP handler (proxy or full)
4. ⏭️ Deploy to Cloudflare
5. ⏭️ Update documentation with hosted option
6. ⏭️ Test with Claude Desktop

---

**References:**
- [Cloudflare MCP Documentation](https://developers.cloudflare.com/agents/model-context-protocol/index.md)
- [Build a Remote MCP server](https://developers.cloudflare.com/agents/guides/remote-mcp-server/index.md)
- [MCP server portals](https://developers.cloudflare.com/agents/model-context-protocol/mcp-portal/index.md)

