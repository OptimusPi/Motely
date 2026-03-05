# JAML Natural Language Generation with RAG
## Complete Implementation Guide for Seed Finder App

**Goal:** Enable natural language → JAML filter generation with learning capabilities  
**Example:** "Give me a face card joker in ante 2" → Valid JAML filter  
**Tech Stack:** Vercel, V0, AI SDK, Vector Database, LLM API

---

## Architecture Overview

```mermaid
graph TB
    User[User Input: "face card ante 2"]
    VectorDB[(Vector DB<br/>Balatro Knowledge)]
    LLM[LLM API<br/>Claude/GPT-4]
    Generator[JAML Generator]
    Validator[JAML Validator]
    UI[Preview & Edit]
    Feedback[(Feedback Store<br/>Corrections)]
    
    User --> VectorDB
    VectorDB --> |Relevant Context| LLM
    User --> LLM
    LLM --> Generator
    Generator --> Validator
    Validator --> UI
    UI --> |User Corrections| Feedback
    Feedback --> VectorDB
```

---

## Phase 1: Knowledge Base Preparation

### 1.1 Create Training Data Structure

Create a new file: `knowledge-base/balatro-knowledge.json`

```json
{
  "entities": {
    "jokers": [
      {
        "id": "Blueprint",
        "name": "Blueprint",
        "description": "Copies ability of Joker to the right",
        "rarity": "rare",
        "category": "support",
        "aliases": ["blue print", "blueprint joker"],
        "jaml_example": "joker: Blueprint"
      },
      {
        "id": "OopsAll6s",
        "name": "Oops! All 6s",
        "description": "Doubles all probabilities",
        "rarity": "uncommon",
        "category": "probability",
        "aliases": ["oops", "all 6s", "oops all sixes"],
        "jaml_example": "joker: OopsAll6s"
      }
    ],
    "tarots": [
      {
        "id": "TheFool",
        "name": "The Fool",
        "effect": "Creates last used Tarot card",
        "aliases": ["fool", "the fool tarot"],
        "jaml_example": "tarot: TheFool"
      }
    ],
    "planets": [
      {
        "id": "Pluto",
        "name": "Pluto",
        "levels": "High Card",
        "aliases": ["pluto planet"],
        "jaml_example": "planet: Pluto"
      }
    ],
    "vouchers": [
      {
        "id": "Telescope",
        "name": "Telescope",
        "effect": "Most played hand appears in shop",
        "jaml_example": "voucher: Telescope"
      }
    ],
    "decks": [
      {
        "id": "Anaglyph",
        "name": "Anaglyph Deck",
        "effect": "After defeating Boss Blind, gain a Double Tag",
        "aliases": ["anaglyph", "anaglyph deck"],
        "jaml_example": "deck: Anaglyph"
      }
    ],
    "tags": [
      {
        "id": "NegativeTag",
        "name": "Negative Tag",
        "effect": "Next Joker in shop is free and becomes Negative",
        "context": "smallBlindTag, bigBlindTag, or bossBlindTag",
        "aliases": ["negative", "negative tag"],
        "jaml_example": "smallBlindTag: NegativeTag"
      }
    ]
  },
  "concepts": {
    "antes": {
      "definition": "Rounds in Balatro, like poker tournaments",
      "range": [1, 8],
      "jaml_usage": "antes: [1,2,3]",
      "examples": [
        "ante 1 = early game",
        "ante 4 = mid game",
        "ante 8 = endgame"
      ]
    },
    "sources": {
      "definition": "Where items appear",
      "types": {
        "shopItems": "Shop positions [0-6]",
        "boosterPacks": "Pack positions [0-4]",
        "buffoonPack": "Joker packs",
        "arcana": "Tarot packs",
        "celestial": "Planet packs"
      },
      "jaml_examples": [
        "sources:\n  shopItems: [0,1,2]",
        "sources:\n  boosterPacks: [0]"
      ]
    },
    "editions": {
      "types": ["Foil", "Holographic", "Polychrome", "Negative"],
      "jaml_usage": "edition: Polychrome",
      "aliases": {
        "Holographic": ["holo", "holographic"],
        "Polychrome": ["poly", "polychrome"],
        "Negative": ["neg", "negative"]
      }
    },
    "seals": {
      "types": ["Gold", "Red", "Blue", "Purple"],
      "jaml_usage": "seal: Gold"
    },
    "enhancements": {
      "types": ["Bonus", "Mult", "Wild", "Glass", "Steel", "Stone", "Gold", "Lucky"],
      "jaml_usage": "enhancement: Glass"
    }
  },
  "jaml_patterns": [
    {
      "intent": "find joker in specific ante",
      "natural_language": ["get {joker} in ante {number}", "find {joker} ante {number}"],
      "jaml_template": "- joker: {joker}\n  antes: [{number}]"
    },
    {
      "intent": "joker in shop slot",
      "natural_language": ["get {joker} in shop slot {slot}", "{joker} in position {slot}"],
      "jaml_template": "- joker: {joker}\n  sources:\n    shopItems: [{slot}]"
    },
    {
      "intent": "joker with edition",
      "natural_language": ["{edition} {joker}", "{joker} with {edition} edition"],
      "jaml_template": "- joker: {joker}\n  edition: {edition}"
    },
    {
      "intent": "specific tag on blind",
      "natural_language": ["{tag} on small blind ante {ante}", "{tag} tag ante {ante}"],
      "jaml_template": "- smallBlindTag: {tag}\n  antes: [{ante}]"
    },
    {
      "intent": "multiple jokers combo",
      "natural_language": ["get {joker1}, {joker2}, and {joker3}", "combo of {jokers}"],
      "jaml_template": "- jokers: [{joker1}, {joker2}, {joker3}]"
    }
  ]
}
```

### 1.2 Convert Existing Markdown Knowledge

If you have a Balatro rules markdown file, structure it into the JSON format above. Focus on:
- Entity names and aliases
- JAML syntax examples
- Common patterns and synonyms

---

## Phase 2: Vector Database Setup (Vercel KV + Upstash)

### 2.1 Install Dependencies

```bash
npm install @upstash/vector ai openai
```

### 2.2 Setup Upstash Vector on Vercel

1. Go to https://console.upstash.com/
2. Create a new Vector Database (1536 dimensions for OpenAI embeddings)
3. Add credentials to Vercel environment variables:
   - `UPSTASH_VECTOR_REST_URL`
   - `UPSTASH_VECTOR_REST_TOKEN`

### 2.3 Embed Knowledge Base

Create `scripts/embed-knowledge.ts`:

```typescript
import { Index } from "@upstash/vector";
import { OpenAI } from "openai";
import knowledgeBase from "../knowledge-base/balatro-knowledge.json";

const vector = new Index({
  url: process.env.UPSTASH_VECTOR_REST_URL!,
  token: process.env.UPSTASH_VECTOR_REST_TOKEN!,
});

const openai = new OpenAI({
  apiKey: process.env.OPENAI_API_KEY!,
});

async function embedAndStore() {
  const documents: Array<{ id: string; text: string; metadata: any }> = [];

  // Embed each joker
  for (const joker of knowledgeBase.entities.jokers) {
    const text = `${joker.name} (${joker.id}): ${joker.description}. Aliases: ${joker.aliases.join(", ")}. JAML: ${joker.jaml_example}`;
    documents.push({
      id: `joker-${joker.id}`,
      text,
      metadata: { type: "joker", ...joker },
    });
  }

  // Embed JAML patterns
  for (const pattern of knowledgeBase.jaml_patterns) {
    const text = `Pattern: ${pattern.intent}. Examples: ${pattern.natural_language.join(", ")}. Template: ${pattern.jaml_template}`;
    documents.push({
      id: `pattern-${pattern.intent}`,
      text,
      metadata: { type: "pattern", ...pattern },
    });
  }

  // Embed concepts
  for (const [key, concept] of Object.entries(knowledgeBase.concepts)) {
    const text = `${key}: ${JSON.stringify(concept)}`;
    documents.push({
      id: `concept-${key}`,
      text,
      metadata: { type: "concept", name: key, ...concept },
    });
  }

  // Generate embeddings and upsert
  for (const doc of documents) {
    const embedding = await openai.embeddings.create({
      model: "text-embedding-3-small",
      input: doc.text,
    });

    await vector.upsert({
      id: doc.id,
      vector: embedding.data[0].embedding,
      metadata: { text: doc.text, ...doc.metadata },
    });
  }

  console.log(`Embedded ${documents.length} documents`);
}

embedAndStore();
```

Run: `npx tsx scripts/embed-knowledge.ts`

---

## Phase 3: RAG Query Pipeline

### 3.1 Create RAG API Route

Create `app/api/jaml/generate/route.ts`:

```typescript
import { Index } from "@upstash/vector";
import { OpenAI } from "openai";
import { streamText } from "ai";

const vector = new Index({
  url: process.env.UPSTASH_VECTOR_REST_URL!,
  token: process.env.UPSTASH_VECTOR_REST_TOKEN!,
});

const openai = new OpenAI({
  apiKey: process.env.OPENAI_API_KEY!,
});

export async function POST(req: Request) {
  const { prompt, conversationHistory } = await req.json();

  // Step 1: Generate embedding for user query
  const queryEmbedding = await openai.embeddings.create({
    model: "text-embedding-3-small",
    input: prompt,
  });

  // Step 2: Retrieve relevant context from vector DB
  const results = await vector.query({
    vector: queryEmbedding.data[0].embedding,
    topK: 10,
    includeMetadata: true,
  });

  // Step 3: Build context from retrieved documents
  const context = results
    .map((r) => r.metadata?.text)
    .filter(Boolean)
    .join("\n\n");

  // Step 4: Build system prompt with RAG context
  const systemPrompt = `You are a JAML (Joker Ante Markup Language) filter generator for Balatro seed searching.

JAML is a YAML-based format for defining seed search criteria. Your job is to convert natural language requests into valid JAML.

## Knowledge Base Context:
${context}

## JAML Structure:
\`\`\`yaml
name: FilterName
deck: DeckName  # optional
stake: StakeName  # optional (Red, Gold, White, etc.)
must:  # Required criteria (AND logic)
  - joker: JokerName
    antes: [1,2,3]  # optional
    edition: EditionName  # optional (Foil, Holographic, Polychrome, Negative)
    sources:  # optional
      shopItems: [0,1,2]
      boosterPacks: [0]
  
  - smallBlindTag: TagName
    antes: [4]

should:  # Optional criteria with scoring (OR logic)
  - joker: AnotherJoker
    score: 100
\`\`\`

## Rules:
1. Use PascalCase for all names (Blueprint, OopsAll6s, NegativeTag)
2. Antes are 1-indexed arrays
3. Shop slots are 0-6, pack slots are 0-4
4. Tags use context: smallBlindTag, bigBlindTag, bossBlindTag
5. Multiple jokers in one criteria: \`jokers: [Joker1, Joker2]\`
6. Editions: Foil, Holographic, Polychrome, Negative
7. Seals: Gold, Red, Blue, Purple
8. Enhancements: Bonus, Mult, Wild, Glass, Steel, Stone, Gold, Lucky

## Examples:
User: "Get Blueprint in ante 2"
JAML:
\`\`\`yaml
name: BlueprintAnte2
must:
  - joker: Blueprint
    antes: [2]
\`\`\`

User: "Negative tag on small blind ante 4, and Oops All 6s in shop"
JAML:
\`\`\`yaml
name: NegativeOops
must:
  - smallBlindTag: NegativeTag
    antes: [4]
  - joker: OopsAll6s
    sources:
      shopItems: [0,1,2,3,4,5,6]
\`\`\`

Generate only valid JAML. If unclear, ask for clarification.`;

  // Step 5: Generate JAML with AI SDK
  const result = await streamText({
    model: openai.chat("gpt-4-turbo"),
    system: systemPrompt,
    messages: [
      ...(conversationHistory || []),
      { role: "user", content: prompt },
    ],
  });

  return result.toDataStreamResponse();
}
```

---

## Phase 4: Frontend Integration (V0 + Vercel AI SDK)

### 4.1 Install AI SDK

```bash
npm install ai @ai-sdk/openai
```

### 4.2 Create Chat Component

Create `components/jaml-chat.tsx`:

```typescript
"use client";

import { useChat } from "ai/react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";

export function JamlChat() {
  const { messages, input, handleInputChange, handleSubmit, isLoading } =
    useChat({
      api: "/api/jaml/generate",
    });

  const [generatedJaml, setGeneratedJaml] = useState<string>("");
  const [isEditing, setIsEditing] = useState(false);

  // Extract JAML from assistant messages
  const extractJaml = (content: string) => {
    const match = content.match(/```yaml\n([\s\S]*?)\n```/);
    return match ? match[1] : content;
  };

  const lastAssistantMessage = [...messages]
    .reverse()
    .find((m) => m.role === "assistant");

  const currentJaml = lastAssistantMessage
    ? extractJaml(lastAssistantMessage.content)
    : "";

  return (
    <div className="flex flex-col h-full gap-4">
      {/* Chat History */}
      <div className="flex-1 overflow-y-auto space-y-4 p-4">
        {messages.map((m) => (
          <div
            key={m.id}
            className={`flex ${m.role === "user" ? "justify-end" : "justify-start"}`}
          >
            <div
              className={`max-w-[80%] rounded-lg p-3 ${
                m.role === "user"
                  ? "bg-blue-600 text-white"
                  : "bg-gray-200 dark:bg-gray-800"
              }`}
            >
              {m.role === "assistant" && m.content.includes("```yaml") ? (
                <pre className="text-xs overflow-x-auto">
                  {extractJaml(m.content)}
                </pre>
              ) : (
                <p>{m.content}</p>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* JAML Preview & Edit */}
      {currentJaml && (
        <div className="border rounded-lg p-4">
          <div className="flex justify-between items-center mb-2">
            <h3 className="font-bold">Generated JAML Filter</h3>
            <div className="space-x-2">
              <Button onClick={() => setIsEditing(!isEditing)}>
                {isEditing ? "Preview" : "Edit"}
              </Button>
              <Button onClick={() => navigator.clipboard.writeText(currentJaml)}>
                Copy
              </Button>
              <Button
                variant="default"
                onClick={() => handleSaveCorrection(currentJaml)}
              >
                Save & Learn
              </Button>
            </div>
          </div>
          {isEditing ? (
            <Textarea
              value={generatedJaml || currentJaml}
              onChange={(e) => setGeneratedJaml(e.target.value)}
              className="font-mono text-xs"
              rows={20}
            />
          ) : (
            <pre className="bg-gray-100 dark:bg-gray-900 p-4 rounded text-xs overflow-x-auto">
              {generatedJaml || currentJaml}
            </pre>
          )}
        </div>
      )}

      {/* Input */}
      <form onSubmit={handleSubmit} className="flex gap-2">
        <input
          value={input}
          onChange={handleInputChange}
          placeholder="Describe your filter... (e.g., 'Get Oops All 6s in ante 4 shop')"
          className="flex-1 p-2 border rounded"
          disabled={isLoading}
        />
        <Button type="submit" disabled={isLoading}>
          {isLoading ? "..." : "Generate"}
        </Button>
      </form>
    </div>
  );
}

async function handleSaveCorrection(jaml: string) {
  // Save user corrections for future training
  await fetch("/api/jaml/feedback", {
    method: "POST",
    body: JSON.stringify({ jaml }),
  });
}
```

---

## Phase 5: Feedback Loop & Learning

### 5.1 Store User Corrections

Create `app/api/jaml/feedback/route.ts`:

```typescript
import { kv } from "@vercel/kv";
import { Index } from "@upstash/vector";
import { OpenAI } from "openai";

const vector = new Index({
  url: process.env.UPSTASH_VECTOR_REST_URL!,
  token: process.env.UPSTASH_VECTOR_REST_TOKEN!,
});

const openai = new OpenAI();

export async function POST(req: Request) {
  const { jaml, originalPrompt, correctedJaml } = await req.json();

  // Store feedback
  const feedbackId = `feedback-${Date.now()}`;
  await kv.set(feedbackId, {
    originalPrompt,
    generatedJaml: jaml,
    correctedJaml,
    timestamp: new Date().toISOString(),
  });

  // Re-embed corrected example
  const text = `User prompt: ${originalPrompt}\nCorrect JAML:\n${correctedJaml}`;
  const embedding = await openai.embeddings.create({
    model: "text-embedding-3-small",
    input: text,
  });

  await vector.upsert({
    id: feedbackId,
    vector: embedding.data[0].embedding,
    metadata: { type: "user_correction", text, originalPrompt, correctedJaml },
  });

  return Response.json({ success: true });
}
```

### 5.2 Periodic Re-training

Create a cron job to aggregate feedback and fine-tune:

```typescript
// app/api/jaml/retrain/route.ts
export async function GET() {
  // Scheduled via Vercel Cron
  const feedback = await kv.keys("feedback-*");
  // Process feedback, create fine-tuning dataset
  // Upload to OpenAI for fine-tuning
  return Response.json({ processed: feedback.length });
}
```

Add to `vercel.json`:

```json
{
  "crons": [
    {
      "path": "/api/jaml/retrain",
      "schedule": "0 0 * * 0"
    }
  ]
}
```

---

## Phase 6: Advanced Features

### 6.1 JAML Validation

Create `lib/validate-jaml.ts`:

```typescript
import Ajv from "ajv";
import jamlSchema from "../jaml.schema.json";

const ajv = new Ajv();
const validate = ajv.compile(jamlSchema);

export function validateJaml(yamlString: string): {
  valid: boolean;
  errors?: string[];
} {
  try {
    const parsed = yaml.parse(yamlString);
    const valid = validate(parsed);
    return {
      valid,
      errors: validate.errors?.map((e) => e.message) || [],
    };
  } catch (err) {
    return { valid: false, errors: [err.message] };
  }
}
```

### 6.2 Few-Shot Prompt Engineering

Enhance the system prompt with dynamic few-shot examples:

```typescript
// In generate/route.ts, add few-shot examples from similar queries
const similarQueries = results
  .filter((r) => r.metadata?.type === "user_correction")
  .slice(0, 3);

const fewShotExamples = similarQueries
  .map(
    (r) => `
User: "${r.metadata.originalPrompt}"
JAML:
\`\`\`yaml
${r.metadata.correctedJaml}
\`\`\`
`
  )
  .join("\n\n");

const systemPrompt = `${baseSystemPrompt}

## Similar Examples from Other Users:
${fewShotExamples}

Generate JAML for the following request:`;
```

### 6.3 Multi-Turn Clarification

Allow the AI to ask clarifying questions:

```typescript
// In system prompt, add:
`If the request is ambiguous, ask clarifying questions before generating JAML.

Examples:
- "Which ante do you want the joker in?"
- "Do you want it in a specific shop slot or any slot?"
- "Should this be a MUST or SHOULD criteria?"`;
```

---

## Phase 7: Alternative: Use Claude with MCP (Model Context Protocol)

If you want even better results, use Claude with the Motely MCP server:

### 7.1 Setup MCP Server

Your repo already has `motely-mcp-server/`. Configure it in Claude Desktop:

```json
{
  "mcpServers": {
    "motely": {
      "command": "node",
      "args": ["x:/BalatroSeedOracle/external/Motely/motely-mcp-server/dist/index.js"],
      "env": {
        "MOTELY_JAML_PATH": "x:/BalatroSeedOracle/external/Motely/JamlFilters"
      }
    }
  }
}
```

### 7.2 Use Claude API with MCP

```typescript
import Anthropic from "@anthropic-ai/sdk";

const anthropic = new Anthropic({
  apiKey: process.env.ANTHROPIC_API_KEY,
});

export async function POST(req: Request) {
  const { prompt } = await req.json();

  const message = await anthropic.messages.create({
    model: "claude-3-5-sonnet-20241022",
    max_tokens: 4096,
    system: [
      {
        type: "text",
        text: "You are a JAML filter generator for Balatro. Generate valid JAML from natural language.",
      },
      {
        type: "tool_use",
        tool_use_id: "motely_mcp",
        name: "get_jaml_examples",
      },
    ],
    messages: [
      {
        role: "user",
        content: prompt,
      },
    ],
  });

  return Response.json({ jaml: message.content });
}
```

---

## Cost Optimization

### Use Smaller Models for Embeddings

```typescript
// text-embedding-3-small: $0.02 / 1M tokens
// text-embedding-ada-002: $0.10 / 1M tokens
// Recommended: text-embedding-3-small
```

### Use GPT-4o-mini for Generation

```typescript
model: openai.chat("gpt-4o-mini"); // $0.15 / 1M input tokens
```

### Cache System Prompts (Claude)

```typescript
const message = await anthropic.messages.create({
  model: "claude-3-5-sonnet-20241022",
  system: [
    {
      type: "text",
      text: systemPrompt,
      cache_control: { type: "ephemeral" }, // Cache for 5 min
    },
  ],
  // ... rest
});
```

---

## Testing Your Implementation

### Test Query Examples

```
1. "Get Blueprint and Brainstorm in ante 2"
2. "Negative tag on small blind ante 4"
3. "Polychrome Joker in shop slot 0 ante 1"
4. "Get Showman from a pack in ante 1"
5. "Three jokers: Oops All 6s, Blueprint, and Invisible Joker"
```

### Expected JAML Outputs

```yaml
# Query 1
name: BlueprintBrainstormAnte2
must:
  - jokers: [Blueprint, Brainstorm]
    antes: [2]

# Query 2
name: NegativeTagAnte4
must:
  - smallBlindTag: NegativeTag
    antes: [4]

# Query 3
name: PolychromeJokerShop0
must:
  - joker: (any joker)
    edition: Polychrome
    antes: [1]
    sources:
      shopItems: [0]

# Query 4
name: ShowmanPack
must:
  - joker: Showman
    antes: [1]
    sources:
      boosterPacks: [0]

# Query 5
name: OopsBlueInvis
must:
  - jokers: [OopsAll6s, Blueprint, InvisibleJoker]
```

---

## Deployment Checklist

- [ ] Upstash Vector DB created and credentials added to Vercel
- [ ] Knowledge base embedded (run `embed-knowledge.ts`)
- [ ] API routes deployed to Vercel
- [ ] Environment variables set:
  - `UPSTASH_VECTOR_REST_URL`
  - `UPSTASH_VECTOR_REST_TOKEN`
  - `OPENAI_API_KEY` or `ANTHROPIC_API_KEY`
- [ ] Frontend component integrated into your V0 app
- [ ] JAML validation implemented
- [ ] Feedback loop configured
- [ ] Test with 10+ real queries

---

## Next Steps

1. **Start with Phase 1-3**: Get basic RAG working
2. **Test with real users**: Gather 100+ corrections
3. **Fine-tune model**: Use feedback to create fine-tuning dataset
4. **Add multi-modal**: Support screenshots ("Find the joker in this image")
5. **Add search history**: "Generate a filter like my last search but with Anaglyph deck"

---

## Resources

- **Vercel AI SDK**: https://sdk.vercel.ai/docs
- **Upstash Vector**: https://upstash.com/docs/vector/overall/getstarted
- **JAML Schema**: `x:/BalatroSeedOracle/external/Motely/jaml.schema.json`
- **Motely MCP Server**: `x:/BalatroSeedOracle/external/Motely/motely-mcp-server/`
- **OpenAI Embeddings**: https://platform.openai.com/docs/guides/embeddings
- **Claude Prompt Caching**: https://docs.anthropic.com/en/docs/build-with-claude/prompt-caching

---

**Estimated Implementation Time:**  
- Phase 1-3 (Basic RAG): 4-6 hours  
- Phase 4-5 (Frontend + Feedback): 3-4 hours  
- Phase 6 (Advanced Features): 2-3 hours  
- **Total**: 1-2 days for a working prototype

**Estimated Monthly Costs** (1000 queries/month):
- Upstash Vector (Free tier): $0
- OpenAI Embeddings: ~$0.20
- OpenAI GPT-4o-mini: ~$5
- Vercel KV (Feedback storage): $0 (free tier)
- **Total**: ~$5-10/month

Good luck! This should get you a production-ready JAML chatbot with learning capabilities. 🚀
