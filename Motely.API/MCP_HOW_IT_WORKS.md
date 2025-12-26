# How the MCP Server Works - Clear Explanation

## The Flow: Human Prompt → Config → Seeds

### Step-by-Step Process

**1. Human gives prompt to Claude Desktop:**
```
"Find me a seed with Blueprint and Brainstorm"
```

**2. Claude calls MCP tool `generate_jaml_filter`:**
- Input: `{ prompt: "Find me a seed with Blueprint and Brainstorm" }`
- Output: `{ jaml: "...", searchId: "...", searchUrl: "..." }`
- **Returns:** JAML config (NOT seeds yet)

**3. Claude can then call `search_seeds`:**
- Input: `{ jaml: "<the JAML from step 2>" }`
- Output: `{ searchId: "...", results: [...], status: "running" }`
- **Returns:** Search ID and initial results (if any found immediately)

**4. Claude can check progress with `get_search_status`:**
- Input: `{ searchId: "..." }`
- Output: `{ status: "running"|"completed", results: [...], progress: 50 }`
- **Returns:** Current status and any new results

---

## What Each Tool Returns

### `generate_jaml_filter`
**Input:** Human prompt (natural language)  
**Output:** JAML config (filter configuration)  
**Does NOT return seeds** - just the config!

**Example:**
```json
{
  "jaml": "must:\n  - type: Joker\n    value: Blueprint\n    antes: [1]",
  "searchId": null,
  "searchUrl": null,
  "reasoning": "AI-generated JAML filter for: Blueprint and Brainstorm"
}
```

### `search_seeds`
**Input:** JAML config  
**Output:** Search ID + initial results (if any)  
**Returns seeds** (if found immediately) or starts search

**Example:**
```json
{
  "searchId": "Blueprint_Red_White",
  "searchUrl": "/JAML/?search=Blueprint_Red_White",
  "results": [
    { "Seed": "ALEEB", "Score": 100, "Tallies": [...] }
  ],
  "status": "running",
  "message": "Search started. Found 1 initial result."
}
```

### `get_search_status`
**Input:** Search ID  
**Output:** Current status + all results so far  
**Returns seeds** (all found so far)

**Example:**
```json
{
  "searchId": "Blueprint_Red_White",
  "status": "completed",
  "results": [
    { "Seed": "ALEEB", "Score": 100 },
    { "Seed": "12345", "Score": 95 }
  ],
  "progressPercent": 100
}
```

---

## Typical Usage Patterns

### Pattern 1: Generate Config Only
```
Human: "Generate a JAML filter for Blueprint"
Claude: Calls generate_jaml_filter
Result: Returns JAML config (no seeds)
```

### Pattern 2: Generate + Search
```
Human: "Find me a seed with Blueprint"
Claude: 
  1. Calls generate_jaml_filter → gets JAML
  2. Calls search_seeds with JAML → gets search ID + initial results
  3. (Optional) Calls get_search_status to check progress
Result: Returns seeds (if found)
```

### Pattern 3: Search Existing Config
```
Human: "Search for seeds using this JAML: ..."
Claude: Calls search_seeds directly
Result: Returns seeds (if found)
```

---

## Key Points

1. **`generate_jaml_filter`** → Returns **config only** (no seeds)
2. **`search_seeds`** → Returns **seeds** (if found) + search ID
3. **`get_search_status`** → Returns **seeds** (all found so far)

**The MCP server provides:**
- ✅ Tools to generate configs
- ✅ Tools to search for seeds
- ✅ Tools to check search status
- ❌ Does NOT automatically search after generating config
- ❌ Does NOT return seeds from `generate_jaml_filter`

**Claude decides:**
- Whether to just generate config
- Whether to search after generating
- When to check search status

---

## Example: Full Flow

**Human asks Claude:**
> "Find me a Balatro seed with Perkeo and Negative tags"

**Claude's actions:**
1. Calls `generate_jaml_filter({ prompt: "Perkeo and Negative tags" })`
   - Gets: `{ jaml: "...", searchId: null }` (just config, no seeds)

2. Calls `search_seeds({ jaml: "<the JAML>" })`
   - Gets: `{ searchId: "Perkeo_Red_White", results: [...], status: "running" }` (seeds!)

3. (Later) Calls `get_search_status({ searchId: "Perkeo_Red_White" })`
   - Gets: `{ status: "completed", results: [seed1, seed2, ...] }` (more seeds!)

**Claude returns to human:**
> "I found 5 seeds matching your criteria: ALEEB, 12345, ..."

---

## Summary

**MCP Server accepts:**
- ✅ Human prompts (via `generate_jaml_filter`)
- ✅ JAML configs (via `search_seeds`)

**MCP Server returns:**
- ✅ JAML configs (from `generate_jaml_filter`)
- ✅ Seeds (from `search_seeds` or `get_search_status`)
- ✅ Search status (from `get_search_status`)

**It's flexible:**
- Can return just config (if Claude only calls `generate_jaml_filter`)
- Can return seeds (if Claude calls `search_seeds`)
- Can return both (if Claude calls both tools)

**Claude decides what to do based on the human's request!**

