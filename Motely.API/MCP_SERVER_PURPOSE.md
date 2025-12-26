# MCP Server Purpose - Design Philosophy

## The Core Question

**What is the MCP server's job?**
- ✅ **Generate JAML configs** from natural language
- ✅ **Search for seeds** using JAML configs
- ✅ **Analyze seeds** to verify they match requirements

**The MCP server provides TOOLS, not a single workflow.**

---

## Current Design (Two Use Cases)

### Use Case 1: REST API (`/mcp/prompt`) - For JamlGenie Frontend
**Purpose:** Generate JAML + Automatically start search  
**Why:** Users on the website want immediate results  
**Method:** `ProcessPromptAsync()` - does both

### Use Case 2: MCP Protocol (`/mcp`) - For AI Assistants
**Purpose:** Provide separate tools that AI can chain together  
**Why:** AI needs flexibility to decide workflow  
**Tools:**
- `generate_jaml_filter` → **ONLY generates config** (no search)
- `search_seeds` → **Searches using config** (separate call)
- `get_search_status` → **Checks progress** (separate call)

---

## The Problem (FIXED)

Previously, `generate_jaml_filter` called `ProcessPromptAsync()`, which:
1. ✅ Generates JAML
2. ❌ **Also automatically searches** (not what MCP tool should do)

**This violated separation of concerns!**

---

## The Solution (IMPLEMENTED)

**Split the logic:**

1. **`GenerateJamlOnlyAsync()`** - New method ✅
   - Refines prompt
   - Generates JAML via AI
   - Validates JAML
   - **Does NOT search**
   - Returns: `{ jaml: string, reasoning: string, error?: string }`

2. **`ProcessPromptAsync()`** - Updated for REST API ✅
   - Calls `GenerateJamlOnlyAsync()`
   - Then starts search
   - Returns: `{ jaml: string, searchId: string, results: array }`

3. **`HandleGenerateJamlFilter()`** - Updated MCP tool ✅
   - Calls `GenerateJamlOnlyAsync()` (not `ProcessPromptAsync()`)
   - Returns: `{ jaml: string, reasoning: string }` (no searchId, no results)

---

## Benefits

✅ **Clear separation:** Config generation ≠ Seed searching  
✅ **AI flexibility:** AI decides when to search  
✅ **REST API convenience:** Still does both for website users  
✅ **MCP protocol compliance:** Tools do one thing each  

---

## Example Flows

### Flow 1: AI Assistant (MCP Protocol) - Config Only
```
User: "Generate a filter for Blueprint"
AI: Calls generate_jaml_filter
→ Returns: { jaml: "...", reasoning: "..." }
→ NO search started
```

### Flow 2: AI Assistant (MCP Protocol) - Full Search
```
User: "Find me a seed with Blueprint"
AI: 
  1. Calls generate_jaml_filter → gets JAML
  2. Calls search_seeds with JAML → gets seeds
  3. Returns seeds to user
```

### Flow 3: Website User (REST API)
```
User types: "Blueprint"
Frontend: Calls /mcp/prompt
→ ProcessPromptAsync() does both:
  - Generates JAML
  - Starts search
  - Returns: { jaml: "...", searchId: "...", results: [...] }
```

---

## Summary

**MCP Server's Job:**
- Provide **tools** for generating configs
- Provide **tools** for searching seeds
- Provide **tools** for analyzing seeds
- **NOT** to automatically chain them together (AI decides that)

**REST API's Job:**
- Provide **convenience** for website users
- Automatically chain generation + search
- Return immediate results

**Both serve different audiences with different needs!**

---

## Answer to Your Question

**"What is the MCP server's job? Should it find seeds or just get config?"**

**Answer:** The MCP server provides **both** as **separate tools**:
- `generate_jaml_filter` → **Just config** (no search)
- `search_seeds` → **Finds seeds** (using config)

**The AI decides** whether to:
- Just get config (if user asks "generate a filter")
- Get config AND search (if user asks "find me a seed")

This gives maximum flexibility while keeping tools focused on single responsibilities.
