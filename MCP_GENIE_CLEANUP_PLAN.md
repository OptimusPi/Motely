# MCP Server & Genie Cleanup Plan
## Comprehensive Analysis & Recovery Strategy

**Date:** 2024-12-26  
**Status:** CRITICAL - Multiple conflicting implementations need consolidation

---

## Executive Summary

The repository contains **multiple overlapping implementations** of MCP server functionality and Genie components, created by different AI agents over the past 3 weeks. This has resulted in:
- **Conflicting code paths** (2 MCP server classes)
- **Duplicate Genie implementations** (3+ frontend versions)
- **Inconsistent API endpoints** (missing `/mcp` protocol endpoint)
- **Unclear architecture** (REST vs MCP Protocol confusion)
- **Documentation sprawl** (20+ markdown files with conflicting info)

---

## Component Inventory

### 1. MCP Server Implementations

#### ✅ **PRIMARY: `Motely.API/McpServer.cs`**
- **Purpose:** Core JAML generation service (used by both REST and MCP Protocol)
- **Status:** ACTIVE - This is the working implementation
- **Key Methods:**
  - `GenerateJamlOnlyAsync()` - JAML generation only
  - `ProcessPromptAsync()` - JAML + search execution
  - `GetSystemPrompt()` - AI system prompt
- **Dependencies:** Cloudflare Worker AI, GenieFeedbackService
- **Used By:** 
  - REST endpoints (`/mcp/prompt`, `/mcp/generate`)
  - MCP Protocol Server (wraps this)

#### ⚠️ **SECONDARY: `Motely.API/McpProtocol/McpServer.cs`**
- **Purpose:** MCP Protocol JSON-RPC 2.0 handler
- **Status:** ACTIVE but INCOMPLETE
- **Issue:** Wraps `McpServer` but endpoint `/mcp` is NOT registered in `MotelyApiHost.cs`
- **Key Methods:**
  - `HandleRequestAsync()` - JSON-RPC request router
  - `HandleToolCall()` - Tool execution
- **Dependencies:** `McpServer` (the primary one)
- **Used By:** Should be used by MCP clients (Claude Desktop, etc.) but endpoint missing

#### 🔴 **PROBLEM:** 
- `McpProtocolServer` exists but `/mcp` endpoint is **NOT registered**
- Only `/mcp/prompt` and `/mcp/generate` REST endpoints exist
- MCP Protocol clients cannot connect

---

### 2. Genie Frontend Implementations

#### ✅ **PRIMARY: `vue-jaml-ui/src/components/JamlGeniePanel.vue`**
- **Location:** Vue 3 app, embedded panel
- **Status:** ACTIVE - Integrated into main JAML UI
- **API Calls:** `/mcp/generate` (JAML only)
- **Features:** Knowledge base, chat interface, copy/use buttons
- **Build Output:** `wwwroot/JAML/` (via Vite build)

#### ✅ **SECONDARY: `vue-jaml-ui/src/views/JamlGenie.vue`**
- **Location:** Vue 3 app, standalone route
- **Status:** ACTIVE - Full-page genie view
- **API Calls:** `/mcp/generate` (JAML only)
- **Features:** Same as panel, but full-page layout
- **Route:** `/genie` (in Vue router)

#### ⚠️ **LEGACY: `wwwroot/JamlGenie/`**
- **Location:** Static HTML/JS files
- **Status:** UNKNOWN - May be old implementation
- **Files:** `index.html`, `app.js`, `style.css`, Cloudflare Pages worker
- **Issue:** Unclear if this is still used or abandoned
- **Action Needed:** Verify if this is deployed separately or obsolete

#### 🔴 **PROBLEM:**
- Multiple Genie implementations with potential code duplication
- Unclear which one is the "source of truth"
- `wwwroot/JamlGenie/` may be orphaned code

---

### 3. API Endpoints

#### ✅ **ACTIVE Endpoints (in `MotelyApiHost.cs`):**

1. **`POST /mcp/prompt`**
   - **Purpose:** Generate JAML + start search (REST API)
   - **Used By:** Legacy JamlGenie frontend (if exists)
   - **Response:** Full search results + JAML
   - **Status:** WORKING

2. **`POST /mcp/generate`**
   - **Purpose:** Generate JAML only (no search)
   - **Used By:** Vue JamlGeniePanel, JamlGenie view
   - **Response:** JAML + reasoning + error
   - **Status:** WORKING

#### 🔴 **MISSING Endpoint:**

3. **`POST /mcp`** (MCP Protocol)
   - **Purpose:** MCP JSON-RPC 2.0 protocol endpoint
   - **Used By:** MCP clients (Claude Desktop, Cline, etc.)
   - **Handler:** `McpProtocolServer.HandleRequestAsync()`
   - **Status:** **NOT REGISTERED** - Code exists but endpoint missing

---

### 4. Cloudflare Worker

#### ✅ **ACTIVE: `Motely.API/cloudflare-worker-jamlgenie/src/index.ts`**
- **Purpose:** Cloudflare Workers AI integration
- **Status:** ACTIVE - Called by `McpServer.GenerateJamlWithAIAsync()`
- **Location:** Separate Cloudflare deployment
- **Function:** AI model inference for JAML generation
- **System Prompt:** Hardcoded in worker (security best practice)

#### ⚠️ **POTENTIAL DUPLICATE: `wwwroot/JamlGenie/worker/`**
- **Status:** UNKNOWN - May be old deployment or separate service
- **Action Needed:** Verify if this is still used

---

### 5. Shared Components

#### ✅ **Motely Core (`Motely/` namespace)**
- **Status:** STABLE - Core search engine
- **Components:**
  - `JamlConfigLoader` - JAML parsing/validation
  - `SearchManager` - Seed search execution
  - `MotelyJsonConfig` - Config data structures
- **No Issues Found**

#### ✅ **Knowledge Base**
- **Location:** `vue-jaml-ui/src/constants/balatroKnowledge.js`
- **Status:** ACTIVE - Used by Genie frontend
- **Content:** Jokers, vouchers, core mechanics
- **No Issues Found**

---

## Critical Issues

### 🔴 **ISSUE #1: Missing MCP Protocol Endpoint**
**Severity:** HIGH  
**Impact:** MCP clients (Claude Desktop, etc.) cannot connect

**Problem:**
- `McpProtocolServer` class exists and is registered in DI
- But `POST /mcp` endpoint is **NOT registered** in `MotelyApiHost.cs`
- Only REST endpoints (`/mcp/prompt`, `/mcp/generate`) exist

**Fix:**
```csharp
// Add to MotelyApiHost.cs after line 300
app.MapPost("/mcp", async (HttpRequest request, McpProtocol.McpProtocolServer mcpServer) =>
{
    try
    {
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        var jsonRpcRequest = JsonSerializer.Deserialize<McpProtocol.JsonRpcRequest>(body);
        
        if (jsonRpcRequest == null)
            return Results.BadRequest(new { error = "Invalid JSON-RPC request" });
        
        var response = await mcpServer.HandleRequestAsync(jsonRpcRequest);
        return Results.Json(response);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});
```

---

### 🔴 **ISSUE #2: Duplicate/Orphaned Genie Implementations**
**Severity:** MEDIUM  
**Impact:** Code confusion, maintenance burden

**Problem:**
- `wwwroot/JamlGenie/` - Static files, unclear if used
- `vue-jaml-ui/src/components/JamlGeniePanel.vue` - Active
- `vue-jaml-ui/src/views/JamlGenie.vue` - Active
- Potential code duplication between implementations

**Action:**
1. **Audit `wwwroot/JamlGenie/`:**
   - Check if deployed separately (Cloudflare Pages?)
   - If obsolete, mark for deletion
   - If active, document its purpose

2. **Consolidate Vue implementations:**
   - Panel and view share logic - extract to composable
   - Create `useJamlGenie.js` composable for shared logic

---

### 🔴 **ISSUE #3: Inconsistent Service Registration**
**Severity:** MEDIUM  
**Impact:** Potential runtime errors

**Problem:**
- `McpServer` registered in `MotelyApiHost.cs` (line 70)
- `McpProtocolServer` registered in `McpStdioEntryPoint.cs` (line 74)
- But `McpProtocolServer` also needs to be registered in `MotelyApiHost.cs` for HTTP endpoint

**Fix:**
```csharp
// Add to MotelyApiHost.cs service registration (after line 76)
builder.Services.AddScoped<McpProtocol.McpProtocolServer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<McpProtocol.McpProtocolServer>>();
    var jamlGenieService = sp.GetRequiredService<McpServer>();
    var searchManager = SearchManager.Instance;
    return new McpProtocol.McpProtocolServer(logger, jamlGenieService, searchManager);
});
```

---

### ⚠️ **ISSUE #4: Documentation Sprawl**
**Severity:** LOW  
**Impact:** Developer confusion

**Problem:**
- 20+ markdown files about MCP/Genie
- Conflicting information
- Unclear which docs are current

**Files to Review:**
- `mcpapi.md` - PRD for Genie widget (current)
- `Motely.API/MCP_SERVER_PURPOSE.md` - Purpose doc
- `Motely.API/README_MCP_SERVER.md` - Setup guide
- `Motely.API/CLAUDE_DESKTOP_SETUP.md` - Client setup
- `Motely.API/MCP_IMPLEMENTATION_PLAN.md` - Implementation plan
- `Motely.API/MCP_SERVER_AUDIT.md` - Audit report
- `Motely.API/MCP_SERVER_SUMMARY.md` - Summary
- `Motely.API/MCP_HOW_IT_WORKS.md` - How it works
- `Motely.API/MCP_PURPOSE_SUMMARY.md` - Purpose summary
- `Motely.API/COMPREHENSIVE_STUDY.md` - Study doc
- And more...

**Action:**
1. Create single `MCP_SERVER.md` master doc
2. Archive/delete outdated docs
3. Link from README to master doc

---

## Cleanup Plan

### Phase 1: Critical Fixes (IMMEDIATE)

#### Task 1.1: Register MCP Protocol Endpoint ✅ COMPLETE
- [x] Add `POST /mcp` endpoint to `MotelyApiHost.cs`
- [x] Register `McpProtocolServer` in DI container
- [ ] Test with MCP client (Claude Desktop) - **PENDING USER TEST**
- [ ] Update documentation - **PENDING**

**Estimated Time:** 30 minutes  
**Actual Time:** ~15 minutes  
**Status:** Code changes complete, testing pending

#### Task 1.2: Verify Service Registration ✅ COMPLETE
- [x] Ensure `McpProtocolServer` registered in `MotelyApiHost.cs`
- [x] Ensure `GenieFeedbackService` registered (if used) - **OPTIONAL, NOT REQUIRED**
- [ ] Test all endpoints work - **PENDING USER TEST**

**Estimated Time:** 15 minutes  
**Actual Time:** ~5 minutes (included in Task 1.1)  
**Status:** Service registration complete, testing pending

---

### Phase 2: Code Consolidation (HIGH PRIORITY)

#### Task 2.1: Audit `wwwroot/JamlGenie/`
- [ ] Check if deployed separately
- [ ] Check if referenced anywhere
- [ ] If obsolete, mark for deletion
- [ ] If active, document purpose

**Estimated Time:** 30 minutes

#### Task 2.2: Extract Shared Genie Logic
- [ ] Create `vue-jaml-ui/src/composables/useJamlGenie.js`
- [ ] Move shared logic from `JamlGeniePanel.vue` and `JamlGenie.vue`
- [ ] Refactor both components to use composable
- [ ] Test both implementations work

**Estimated Time:** 1-2 hours

---

### Phase 3: Documentation Cleanup (MEDIUM PRIORITY)

#### Task 3.1: Create Master Documentation
- [ ] Create `MCP_SERVER.md` with:
  - Architecture overview
  - API endpoints (REST + MCP Protocol)
  - Frontend implementations
  - Setup instructions
  - Troubleshooting
- [ ] Link from main README

**Estimated Time:** 1 hour

#### Task 3.2: Archive Outdated Docs
- [ ] Move outdated docs to `docs/archive/`
- [ ] Update any cross-references
- [ ] Delete truly obsolete files

**Estimated Time:** 30 minutes

---

### Phase 4: Testing & Validation (HIGH PRIORITY)

#### Task 4.1: End-to-End Testing
- [ ] Test `/mcp/generate` endpoint (Vue Genie)
- [ ] Test `/mcp/prompt` endpoint (if used)
- [ ] Test `/mcp` endpoint (MCP Protocol)
- [ ] Test Cloudflare Worker integration
- [ ] Test error handling

**Estimated Time:** 1 hour

#### Task 4.2: Client Integration Testing
- [ ] Test Claude Desktop MCP connection
- [ ] Test Vue Genie panel in JAML UI
- [ ] Test Vue Genie standalone route
- [ ] Verify knowledge base integration

**Estimated Time:** 1 hour

---

## Architecture Decision: REST vs MCP Protocol

### Current State (CONFUSED):
- REST endpoints exist (`/mcp/prompt`, `/mcp/generate`)
- MCP Protocol code exists but endpoint missing
- Unclear which is "primary"

### Recommended Architecture:

```
┌─────────────────────────────────────────────────────────┐
│                    Frontend Clients                      │
├─────────────────────────────────────────────────────────┤
│  Vue Genie Panel/View  │  Legacy JamlGenie (if exists) │
└────────────┬────────────┴──────────────┬─────────────────┘
             │                           │
             │ POST /mcp/generate        │ POST /mcp/prompt
             │ (JAML only)               │ (JAML + search)
             │                           │
┌────────────▼───────────────────────────▼─────────────────┐
│              Motely.API (ASP.NET Core)                    │
│  ┌──────────────────────────────────────────────────┐   │
│  │  REST Endpoints (MotelyApiHost.cs)                │   │
│  │  - POST /mcp/generate                             │   │
│  │  - POST /mcp/prompt                               │   │
│  │  - POST /mcp (NEW - MCP Protocol)                │   │
│  └──────────────────────────────────────────────────┘   │
│                          │                                │
│  ┌───────────────────────▼────────────────────────────┐  │
│  │  McpServer (Core Service)                          │  │
│  │  - GenerateJamlOnlyAsync()                         │  │
│  │  - ProcessPromptAsync()                            │  │
│  │  - GetSystemPrompt()                                │  │
│  └───────────────────────┬────────────────────────────┘  │
│                          │                                │
│  ┌───────────────────────▼────────────────────────────┐  │
│  │  McpProtocolServer (MCP Protocol Handler)           │  │
│  │  - HandleRequestAsync()                            │  │
│  │  - HandleToolCall()                                │  │
│  │  - Wraps McpServer                                 │  │
│  └────────────────────────────────────────────────────┘  │
└───────────────────────────┬──────────────────────────────┘
                            │
                            │ HTTP POST
                            │
┌───────────────────────────▼──────────────────────────────┐
│         Cloudflare Worker (Workers AI)                    │
│  - AI model inference                                     │
│  - System prompt hardcoded                                 │
│  - Returns JAML                                           │
└───────────────────────────────────────────────────────────┘
```

### Decision:
- **Keep both REST and MCP Protocol** - They serve different use cases:
  - REST: Frontend web apps (Vue Genie)
  - MCP Protocol: AI assistants (Claude Desktop, Cline)
- **Single core service** (`McpServer`) used by both
- **Clear separation** of concerns

---

## File Structure After Cleanup

```
Motely.API/
├── McpServer.cs                    ✅ KEEP (Core service)
├── McpProtocol/
│   ├── McpServer.cs                ✅ KEEP (Protocol handler)
│   ├── McpStdioServer.cs            ✅ KEEP (stdio transport)
│   └── JsonRpcModels.cs             ✅ KEEP (Protocol models)
├── GenieFeedbackService.cs         ✅ KEEP (Learning system)
├── MotelyApiHost.cs                 ⚠️ FIX (Add /mcp endpoint)
└── cloudflare-worker-jamlgenie/    ✅ KEEP (AI worker)

vue-jaml-ui/
├── src/
│   ├── components/
│   │   └── JamlGeniePanel.vue      ✅ KEEP (Panel version)
│   ├── views/
│   │   └── JamlGenie.vue           ✅ KEEP (Standalone version)
│   ├── composables/
│   │   └── useJamlGenie.js         🆕 CREATE (Shared logic)
│   └── constants/
│       └── balatroKnowledge.js     ✅ KEEP (Knowledge base)

wwwroot/
├── JAML/                            ✅ KEEP (Vue build output)
└── JamlGenie/                       ⚠️ AUDIT (May be obsolete)

docs/
├── MCP_SERVER.md                    🆕 CREATE (Master doc)
└── archive/                         🆕 CREATE (Old docs)
```

---

## Testing Checklist

### Backend API Tests
- [ ] `POST /mcp/generate` returns valid JAML
- [ ] `POST /mcp/prompt` generates JAML and starts search
- [ ] `POST /mcp` handles JSON-RPC 2.0 requests
- [ ] Error handling works correctly
- [ ] Cloudflare Worker integration works

### Frontend Tests
- [ ] Vue Genie Panel loads and works
- [ ] Vue Genie standalone route works
- [ ] Knowledge base queries work
- [ ] Copy JAML button works
- [ ] Use in Editor button works
- [ ] API error handling works

### Integration Tests
- [ ] Claude Desktop can connect via MCP Protocol
- [ ] Vue Genie can generate JAML
- [ ] Generated JAML loads into editor
- [ ] Search execution works

---

## Success Criteria

✅ **Phase 1 Complete When:**
- `/mcp` endpoint registered and working
- All service registrations correct
- No runtime errors

✅ **Phase 2 Complete When:**
- Shared Genie logic extracted
- No code duplication
- All implementations work

✅ **Phase 3 Complete When:**
- Single master documentation file
- Outdated docs archived
- Clear architecture documented

✅ **Phase 4 Complete When:**
- All tests pass
- MCP clients can connect
- Frontend Genie works
- No regressions

---

## Risk Assessment

### Low Risk
- Documentation cleanup
- Code extraction/refactoring

### Medium Risk
- Adding `/mcp` endpoint (new code path)
- Service registration changes

### High Risk
- Deleting `wwwroot/JamlGenie/` (if it's deployed separately)
- Changing existing API contracts

**Mitigation:**
- Test thoroughly before deleting anything
- Keep backups of removed code
- Version control all changes

---

## Next Steps

1. **IMMEDIATE:** Fix missing `/mcp` endpoint (Phase 1, Task 1.1)
2. **IMMEDIATE:** Verify service registrations (Phase 1, Task 1.2)
3. **HIGH PRIORITY:** Audit `wwwroot/JamlGenie/` (Phase 2, Task 2.1)
4. **HIGH PRIORITY:** Extract shared Genie logic (Phase 2, Task 2.2)
5. **MEDIUM PRIORITY:** Documentation cleanup (Phase 3)
6. **HIGH PRIORITY:** Full testing (Phase 4)

---

## Questions to Resolve

1. **Is `wwwroot/JamlGenie/` still deployed/used?**
   - If yes, document its purpose
   - If no, mark for deletion

2. **Is `/mcp/prompt` endpoint still used?**
   - Vue Genie uses `/mcp/generate`
   - Check if anything uses `/mcp/prompt`

3. **Should we support both REST and MCP Protocol?**
   - Recommendation: YES (different use cases)
   - But document clearly

4. **Is `GenieFeedbackService` being used?**
   - Check if failures are being logged
   - Verify feedback collection works

---

**END OF PLAN**
