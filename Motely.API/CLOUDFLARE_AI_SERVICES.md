# Cloudflare AI Services Integration Guide

Based on [Cloudflare's AI documentation](https://developers.cloudflare.com/developer-platform/llms-full.txt), here are relevant AI services that could enhance the Balatro Seed Oracle MCP server:

## Currently Using

### ✅ Workers AI
- **What:** JAML generation via Cloudflare Workers AI
- **Model:** `@cf/meta/llama-3.1-8b-instruct-fp8`
- **Location:** `Motely.API/McpServer.cs`
- **Usage:** Natural language → JAML filter generation

## Potential Integrations

### 1. AI Gateway ⭐ **RECOMMENDED**

**What it does:**
- Observability and control over AI applications
- Analytics, logging, caching, rate limiting
- Request retries and model fallback

**Why integrate:**
- **Analytics:** Track JAML generation requests, costs, usage patterns
- **Caching:** Cache common JAML queries (e.g., "Blueprint and Brainstorm")
- **Rate Limiting:** Prevent abuse of the MCP server
- **Cost Tracking:** Monitor Workers AI usage and costs

**How to integrate:**
```typescript
// In Cloudflare Worker
const aiGatewayUrl = `https://gateway.ai.cloudflare.com/v1/${accountId}/${gatewayId}/workers-ai`;

const response = await fetch(aiGatewayUrl, {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${env.CLOUDFLARE_API_TOKEN}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    model: '@cf/meta/llama-3.1-8b-instruct-fp8',
    messages: [...]
  })
});
```

**Benefits:**
- See which prompts are most common
- Cache frequently requested JAML configs
- Monitor costs per user/request
- Automatic retries on failures

### 2. AI Search (formerly AutoRAG) 🔍

**What it does:**
- Managed search service for RAG applications
- Automatically indexes and updates content
- Natural language search over your data

**Potential use case:**
- Index Balatro game mechanics documentation
- Index JAML filter examples
- Enable natural language search over game knowledge
- Provide context to JAML generation AI

**Example:**
```typescript
// Search game mechanics before generating JAML
const searchResults = await env.AI_SEARCH.query({
  query: "What jokers are available in Ante 1?",
  filters: { category: "game-mechanics" }
});

// Use search results to improve JAML generation
const enhancedPrompt = `${userPrompt}\n\nContext: ${searchResults.join('\n')}`;
```

**Benefits:**
- Better JAML generation with game knowledge
- Searchable documentation for users
- Automatic updates when docs change

### 3. Vectorize 🧠

**What it does:**
- Vector database for embeddings
- Semantic search
- Context and memory for LLMs

**Potential use case:**
- Store embeddings of successful JAML filters
- Semantic search for similar filters
- Find filters by intent, not exact match

**Example:**
```typescript
// Store successful JAML filter embeddings
await env.VECTORIZE.insert([
  {
    id: 'filter-123',
    values: await generateEmbedding(jamlFilter),
    metadata: { prompt: userPrompt, seedCount: 5 }
  }
]);

// Find similar filters
const similar = await env.VECTORIZE.query({
  vector: await generateEmbedding(userPrompt),
    topK: 5
});
```

**Benefits:**
- Suggest similar filters to users
- Learn from successful searches
- Improve JAML generation with examples

### 4. Agents SDK 🤖

**What it does:**
- Build AI-powered agents
- Autonomous task performance
- Real-time communication
- State persistence

**Potential use case:**
- Create an autonomous Balatro seed finder agent
- Agent that can chain multiple searches
- Learn from user preferences
- Schedule recurring searches

**Example:**
```typescript
import { Agent } from "agents";

class BalatroSeedAgent extends Agent<Env> {
  async onRequest(request: Request) {
    // Agent can autonomously:
    // 1. Generate JAML from user request
    // 2. Start search
    // 3. Monitor progress
    // 4. Notify when seeds found
    // 5. Learn from successful patterns
  }
}
```

**Benefits:**
- Autonomous seed finding
- Multi-step workflows
- Persistent state (user preferences)
- Scheduled searches

## Integration Priority

### High Priority ⭐
1. **AI Gateway** - Immediate value for observability and caching
2. **AI Search** - Improve JAML generation with game knowledge

### Medium Priority
3. **Vectorize** - Enhance filter discovery and suggestions
4. **Agents SDK** - Advanced autonomous capabilities

## Implementation Notes

### AI Gateway Setup

1. **Create Gateway:**
   ```bash
   # Via Dashboard or API
   curl -X POST "https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/gateways" \
     -H "Authorization: Bearer {api_token}" \
     -d '{"name": "balatro-mcp-gateway"}'
   ```

2. **Update Worker:**
   - Route Workers AI calls through AI Gateway
   - Enable caching for common prompts
   - Set up rate limiting

3. **Monitor:**
   - View analytics in dashboard
   - Track costs and usage
   - Optimize based on data

### AI Search Setup

1. **Index Game Mechanics:**
   - Upload Balatro wiki/docs
   - Index JAML examples
   - Set up automatic updates

2. **Integrate with MCP:**
   - Add `search_game_knowledge` tool
   - Use search results in JAML generation
   - Provide context to AI model

## Cost Considerations

- **AI Gateway:** Free tier available, pay-per-use after
- **AI Search:** Free tier available, pay-per-use after
- **Vectorize:** Free tier: 5M vector operations/month
- **Agents:** Included with Workers, pay for compute

## References

- [AI Gateway Documentation](https://developers.cloudflare.com/ai-gateway/)
- [AI Search Documentation](https://developers.cloudflare.com/ai-search/)
- [Vectorize Documentation](https://developers.cloudflare.com/vectorize/)
- [Agents SDK Documentation](https://developers.cloudflare.com/agents/)
- [Full AI Services Index](https://developers.cloudflare.com/developer-platform/llms-full.txt)

## Next Steps

1. **Start with AI Gateway** - Quick win for observability
2. **Add AI Search** - Improve JAML generation quality
3. **Consider Vectorize** - If filter discovery becomes important
4. **Explore Agents** - For advanced autonomous features

---

**Note:** All these services integrate seamlessly with Cloudflare Workers, making them perfect additions to your MCP server Worker implementation.

