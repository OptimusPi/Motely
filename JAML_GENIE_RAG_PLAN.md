# 🧞 JAML Genie RAG Implementation Plan

## 🎯 Goal
Make JAML Genie **actually good** by using RAG (Retrieval Augmented Generation) with Cloudflare's free/cheap tier services.

**You already have:**
- ✅ R2 (purchased)
- ✅ D1 (in use)
- ✅ Workers AI (working - `jamlgenie.optimuspi.workers.dev`)
- ✅ Knowledge files (`Knowledge/*.md`, `Knowledge/*.json`)

**You need to add:**
- 🎯 **Vectorize** - Vector database for semantic search
- 🎯 **Embeddings Worker** - Generate embeddings for RAG

---

## 📊 Architecture

```
User: "I want a seed with Blueprint early and good economy"
                    │
                    ▼
┌──────────────────────────────────────────────────────────┐
│  Cloudflare Worker: jamlgenie-rag                        │
│                                                          │
│  1. EMBED the user query                                 │
│     └─► Workers AI: @cf/baai/bge-base-en-v1.5           │
│                                                          │
│  2. SEARCH Vectorize for relevant context                │
│     └─► Query: "Blueprint economy" → top 5 matches      │
│     └─► Returns: similar JAML examples + game knowledge │
│                                                          │
│  3. BUILD enhanced prompt with retrieved context         │
│     ┌─────────────────────────────────────────────────┐ │
│     │ SYSTEM PROMPT:                                  │ │
│     │ - JAML syntax reference (from jaml-syntax.md)   │ │
│     │ - Game mechanics (from game-mechanics.md)       │ │
│     │                                                 │ │
│     │ CONTEXT (from Vectorize):                       │ │
│     │ - Similar JAML example #1                       │ │
│     │ - Similar JAML example #2                       │ │
│     │ - Relevant strategy pattern                     │ │
│     │                                                 │ │
│     │ USER: "Blueprint early and good economy"        │ │
│     └─────────────────────────────────────────────────┘ │
│                                                          │
│  4. GENERATE with Workers AI                             │
│     └─► @cf/meta/llama-3.1-8b-instruct-fp8             │
│     └─► Returns valid JAML                              │
│                                                          │
└──────────────────────────────────────────────────────────┘
                    │
                    ▼
          Valid JAML Filter
```

---

## 🗂️ What Gets Embedded in Vectorize

### Index 1: `jaml-examples` (CRITICAL)
Embed every JAML file in `JamlFilters/` as examples:

| id | text (for embedding) | metadata |
|----|----------------------|----------|
| `01WeeMonday` | "Erratic deck Wee Joker with Eternal sticker, wants 10+ Twos for high mult scaling" | `{ jaml: "...", tags: ["erratic", "wee", "scaling"] }` |
| `meow_money` | "Lucky Money event filter, finds seeds with early lucky card money procs" | `{ jaml: "...", tags: ["event", "lucky", "economy"] }` |

### Index 2: `game-knowledge` 
Embed game mechanics and strategy patterns:

| id | text | metadata |
|----|------|----------|
| `blueprint-synergy` | "Blueprint copies the joker to its right. Best synergies: Brainstorm, Showman, Baron. Often paired for infinite copy chains." | `{ type: "synergy", jokers: ["Blueprint", "Brainstorm"] }` |
| `economy-early` | "Early economy strategies: Reserved Parking, Golden Joker, Credit Card. LuckyMoney events proc at 1/15 chance." | `{ type: "strategy", category: "economy" }` |

### Index 3: `prompt-examples` (Few-shot learning)
Embed successful prompt→JAML pairs from `prompt-examples.jsonl`:

| id | text | metadata |
|----|------|----------|
| `example-1` | "User asked: 'blueprint brainstorm copy chain'. Generated JAML with Blueprint ante 1, Brainstorm ante 1-2, Showman as should." | `{ prompt: "...", jaml: "..." }` |

---

## 🛠️ Implementation Steps

### Step 1: Create Vectorize Index (5 min)

```bash
# In your terminal with Wrangler
npx wrangler vectorize create jaml-knowledge --dimensions=768 --metric=cosine
```

### Step 2: Create Embeddings Worker (30 min)

**File: `cloudflare-worker-embeddings/src/index.ts`**

```typescript
export interface Env {
  AI: Ai;
  VECTORIZE: VectorizeIndex;
  JAML_BUCKET: R2Bucket;  // Your R2 bucket
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    
    // Endpoint: Generate embedding for text
    if (url.pathname === '/embed' && request.method === 'POST') {
      const { text } = await request.json() as { text: string };
      
      const embedding = await env.AI.run('@cf/baai/bge-base-en-v1.5', {
        text: [text]
      });
      
      return Response.json({ embedding: embedding.data[0] });
    }
    
    // Endpoint: Query similar documents
    if (url.pathname === '/query' && request.method === 'POST') {
      const { query, topK = 5 } = await request.json() as { query: string; topK?: number };
      
      // Embed the query
      const queryEmbedding = await env.AI.run('@cf/baai/bge-base-en-v1.5', {
        text: [query]
      });
      
      // Search Vectorize
      const results = await env.VECTORIZE.query(queryEmbedding.data[0], {
        topK,
        returnMetadata: true
      });
      
      return Response.json({ results: results.matches });
    }
    
    // Endpoint: Index a document
    if (url.pathname === '/index' && request.method === 'POST') {
      const { id, text, metadata } = await request.json() as {
        id: string;
        text: string;
        metadata: Record<string, string>;
      };
      
      const embedding = await env.AI.run('@cf/baai/bge-base-en-v1.5', {
        text: [text]
      });
      
      await env.VECTORIZE.insert([{
        id,
        values: embedding.data[0],
        metadata
      }]);
      
      return Response.json({ success: true, id });
    }
    
    return new Response('Not found', { status: 404 });
  }
};
```

**wrangler.toml:**
```toml
name = "jaml-embeddings"
main = "src/index.ts"
compatibility_date = "2024-01-01"

[ai]
binding = "AI"

[[vectorize]]
binding = "VECTORIZE"
index_name = "jaml-knowledge"

[[r2_buckets]]
binding = "JAML_BUCKET"
bucket_name = "your-r2-bucket"
```

### Step 3: Seed the Knowledge Base (1 hour)

Create a script to embed all your knowledge:

**File: `scripts/seed-vectorize.ts`**

```typescript
import fs from 'fs';
import path from 'path';

const EMBEDDINGS_WORKER = 'https://jaml-embeddings.optimuspi.workers.dev';

async function indexDocument(id: string, text: string, metadata: Record<string, string>) {
  const response = await fetch(`${EMBEDDINGS_WORKER}/index`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ id, text, metadata })
  });
  return response.json();
}

async function main() {
  // 1. Index JAML examples
  const jamlDir = './JamlFilters';
  for (const file of fs.readdirSync(jamlDir)) {
    if (!file.endsWith('.jaml')) continue;
    
    const jaml = fs.readFileSync(path.join(jamlDir, file), 'utf-8');
    const name = file.replace('.jaml', '');
    
    // Create a natural language description of the JAML
    const description = describeJaml(jaml); // You write this function
    
    await indexDocument(`jaml-${name}`, description, {
      type: 'jaml-example',
      filename: file,
      jaml: jaml
    });
    
    console.log(`Indexed: ${file}`);
  }
  
  // 2. Index game knowledge chunks
  const gameKnowledge = fs.readFileSync('./Motely.API/Knowledge/game-mechanics.md', 'utf-8');
  const chunks = chunkMarkdown(gameKnowledge, 500); // Split into ~500 char chunks
  
  for (let i = 0; i < chunks.length; i++) {
    await indexDocument(`knowledge-${i}`, chunks[i], {
      type: 'game-knowledge',
      source: 'game-mechanics.md'
    });
  }
  
  // 3. Index strategy patterns
  const strategies = fs.readFileSync('./Motely.API/Knowledge/strategy-patterns.md', 'utf-8');
  const strategyChunks = chunkMarkdown(strategies, 500);
  
  for (let i = 0; i < strategyChunks.length; i++) {
    await indexDocument(`strategy-${i}`, strategyChunks[i], {
      type: 'strategy',
      source: 'strategy-patterns.md'
    });
  }
  
  // 4. Index prompt examples (few-shot)
  const examples = fs.readFileSync('./Motely.API/Knowledge/prompt-examples.jsonl', 'utf-8')
    .split('\n')
    .filter(Boolean)
    .map(line => JSON.parse(line));
  
  for (let i = 0; i < examples.length; i++) {
    const ex = examples[i];
    await indexDocument(`example-${i}`, `User: ${ex.prompt}. Generated: ${ex.description}`, {
      type: 'prompt-example',
      prompt: ex.prompt,
      jaml: ex.jaml
    });
  }
  
  console.log('Done!');
}

main();
```

### Step 4: Update JamlGenie Worker with RAG (30 min)

**Update `cloudflare-worker-jamlgenie/src/index.ts`:**

```typescript
export interface Env {
  AI: Ai;
  EMBEDDINGS_WORKER: string; // URL to embeddings worker
}

async function retrieveContext(query: string, env: Env): Promise<string> {
  const response = await fetch(`${env.EMBEDDINGS_WORKER}/query`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ query, topK: 5 })
  });
  
  const { results } = await response.json() as { results: Array<{ metadata: any; score: number }> };
  
  // Build context from retrieved documents
  let context = '';
  
  for (const match of results) {
    if (match.metadata.type === 'jaml-example') {
      context += `\n\n### Similar JAML Example:\n\`\`\`yaml\n${match.metadata.jaml}\n\`\`\``;
    } else if (match.metadata.type === 'game-knowledge') {
      context += `\n\n### Game Knowledge:\n${match.metadata.text || ''}`;
    } else if (match.metadata.type === 'prompt-example') {
      context += `\n\n### Previous successful generation:\nUser asked: "${match.metadata.prompt}"\nResult was a JAML that ${match.metadata.description}`;
    }
  }
  
  return context;
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const { prompt } = await request.json() as { prompt: string };
    
    // 🔥 RAG: Retrieve relevant context
    const retrievedContext = await retrieveContext(prompt, env);
    
    // Build enhanced prompt
    const systemPrompt = `You are JAML Genie, an expert at creating Balatro seed search filters.

## JAML Syntax (key rules):
- Use \`must:\` for required items, \`should:\` for scored optional items
- Types: joker, voucher, legendaryJoker, tarot, planet, spectral, boss, tag, event
- Modifiers: antes (1-8), edition (Foil/Holo/Polychrome/Negative), score, sources

## Retrieved Context (similar examples and knowledge):
${retrievedContext}

## Instructions:
1. Generate ONLY valid JAML (no explanation)
2. Use the retrieved examples as reference for syntax
3. Match the user's intent closely
4. Prioritize early antes (1-2) with higher scores`;

    const response = await env.AI.run('@cf/meta/llama-3.1-8b-instruct-fp8', {
      messages: [
        { role: 'system', content: systemPrompt },
        { role: 'user', content: prompt }
      ],
      max_tokens: 1024
    });
    
    return Response.json({
      success: true,
      jaml: response.response,
      context_used: results.length
    });
  }
};
```

---

## 💰 Cost Breakdown (FREE TIER)

| Service | Free Tier | Your Usage | Cost |
|---------|-----------|------------|------|
| **Workers AI** | 10,000 neurons/day | ~1000 requests | **FREE** |
| **Vectorize** | 5M vector operations/month | ~10K queries | **FREE** |
| **R2** | 10GB storage | Knowledge files | **FREE** |
| **D1** | 5M reads/day | Metadata | **FREE** |
| **Workers** | 100K requests/day | API calls | **FREE** |

**Total: $0/month** for reasonable usage.

---

## 📋 Checklist

### Phase 1: Infrastructure (Day 1)
- [ ] Create Vectorize index: `npx wrangler vectorize create jaml-knowledge --dimensions=768 --metric=cosine`
- [ ] Deploy embeddings worker
- [ ] Test embedding generation: `curl -X POST https://jaml-embeddings.../embed -d '{"text":"Blueprint early"}'`

### Phase 2: Knowledge Ingestion (Day 1-2)
- [ ] Write `describeJaml()` function to convert JAML to natural language
- [ ] Run seeding script to embed all 50+ JAML files
- [ ] Embed game-mechanics.md chunks
- [ ] Embed strategy-patterns.md chunks
- [ ] Embed prompt-examples.jsonl

### Phase 3: RAG Integration (Day 2)
- [ ] Update JamlGenie worker with RAG retrieval
- [ ] Test: "blueprint brainstorm" should retrieve copy-build examples
- [ ] Test: "lucky money" should retrieve event filter examples
- [ ] Test: "erratic wee joker" should retrieve erratic deck examples

### Phase 4: Quality Tuning (Day 3+)
- [ ] Adjust `topK` (how many examples to retrieve)
- [ ] Tune embedding descriptions for better matching
- [ ] Add user feedback loop (store good/bad generations)
- [ ] Consider upgrading model if quality still lacking

---

## 🚀 Quick Start Commands

```bash
# 1. Create the Vectorize index
npx wrangler vectorize create jaml-knowledge --dimensions=768 --metric=cosine

# 2. Create embeddings worker project
npm create cloudflare@latest jaml-embeddings

# 3. Deploy embeddings worker
cd jaml-embeddings
npx wrangler deploy

# 4. Seed the knowledge base (run your script)
npx ts-node scripts/seed-vectorize.ts

# 5. Update and deploy JamlGenie worker
cd cloudflare-worker-jamlgenie
npx wrangler deploy
```

---

## 🎯 Why This Will Work

1. **RAG solves the "hallucination" problem** - The LLM sees real JAML examples, not just instructions
2. **Semantic search finds relevant context** - "economy build" matches "Golden Joker, Credit Card, Reserved Parking"
3. **Few-shot learning** - Real prompt→JAML pairs teach the model what works
4. **Free tier is sufficient** - You're not running a million queries/day

**The key insight:** Your current Genie has ZERO examples in context. With RAG, every request gets 5+ relevant examples. That's the difference between "guess what JAML looks like" and "here are 5 similar filters, now make one like these."

---

## 📚 References

- [Cloudflare RAG Tutorial](https://developers.cloudflare.com/workers-ai/tutorials/build-a-retrieval-augmented-generation-ai/)
- [Vectorize Docs](https://developers.cloudflare.com/vectorize/)
- [Workers AI Embeddings](https://developers.cloudflare.com/workers-ai/models/text-embeddings/)
- [BGE Embedding Model](https://developers.cloudflare.com/workers-ai/models/bge-base-en-v1.5/)

---

*Last Updated: 2025-01-09*
