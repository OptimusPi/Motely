# Complete Prompt for BalatroSeedOracle Parent Workspace

## Context: What's Been Done in Motely Submodule

The `external/Motely/` submodule has been significantly enhanced with:

1. **DuckLake Implementation** - Complete multiplayer DuckDB support
2. **Cloudflare R2 Integration** - Code ready, needs configuration
3. **Cross-Platform Architecture** - WebAssembly, mobile, desktop support
4. **Complete Documentation** - Architecture, guides, specifications

## Your Mission: Build the Best Balatro Seed Searcher Ecosystem

### Current State
- ✅ Motely.CLI saves to DuckDB (`--output-db`)
- ✅ Motely.API has SignalR (needs testing)
- ✅ JamlGenie AI filter generator (working)
- ⚠️ MCP Server (broken/test example - needs fixing)
- ⚠️ Motely.TUI (needs DuckDB seed source selector)
- ❌ ErraticDeck.app (empty - needs building)

### Goal: Complete Ecosystem
- Multi-player seed searching (DuckLake + R2)
- Real-time updates (SignalR)
- AI filter generation (JamlGenie)
- Vector similarity search (Vectorize)
- Job queue management (Cloudflare Queues)
- Cross-platform support (WebAssembly, mobile, desktop)

## Critical Files to Read (In Order)

### 1. Start Here: Complete Overview
**File**: `external/Motely/BALATROSEEDORACLE_CLOUDFLARE_ECOSYSTEM.md`
- Complete architecture
- Cloudflare services recommendations
- Implementation priorities
- Cost estimates

### 2. Quick Start Guide
**File**: `external/Motely/HANDOFF_TO_PARENT_WORKSPACE.md`
- Quick reference
- File locations
- Implementation priorities

### 3. DuckLake Architecture
**File**: `external/Motely/DUCKLAKE_CLOUD_ARCHITECTURE.md`
- DuckLake + R2 integration
- Multiplayer access patterns
- Remote data paths

### 4. R2 Integration
**File**: `external/Motely/R2_INTEGRATION_GUIDE.md`
- Step-by-step R2 setup
- DuckDB R2 secrets
- Testing guide

### 5. ErraticDeck.app Specs
**File**: `external/Motely/ERRATICDECK_APP_SPEC.md`
- Complete specifications
- Tech stack (React TypeScript)
- API endpoints
- Knowledge transfer for Google Antigravity team

### 6. Cross-Platform Support
**File**: `external/Motely/CROSS_PLATFORM_ARCHITECTURE.md`
- WebAssembly, Windows, macOS, Linux, iOS, Android
- Platform-specific implementations
- Integration points

### 7. Data Flow
**File**: `external/Motely/DUCKDB_INPUT_OUTPUT_FLOW.md`
- Complete input/output pipeline
- Seed source conversion
- Results database schema

## Key Implementation Files

### DuckLake Implementation
```
external/Motely/Motely/Motely.DuckDB/
├── DuckLakeHelper.cs              # Core DuckLake operations
├── CloudStorageHelper.cs          # R2/S3 utilities
├── DuckDBConnectionFactory.cs     # R2 attach support
└── DuckDBSchema.cs               # Schema definitions
```

### API Components
```
external/Motely/Motely.API/
├── Services/
│   ├── SearchQueueService.cs     # Migrate to Cloudflare Queues
│   └── SearchService.cs          # Search execution
├── Hubs/SearchHub.cs             # SignalR (needs testing)
├── McpProtocol/McpServer.cs      # MCP (needs fixing)
└── MotelyApiHost.cs              # API endpoints
```

### CLI/TUI
```
external/Motely/
├── Motely.CLI/Program.cs         # CLI with --output-db
└── Motely.TUI/                   # Needs DuckDB selector
```

## Cloudflare Services to Purchase

### Essential (Already Have)
1. ✅ **R2** - Object storage
2. ✅ **D1** - SQLite database
3. ✅ **Workers AI** - AI inference

### High Priority (Buy These!)
4. **Cloudflare Queues** - $0.40 per 1M operations
   - Replace in-memory SearchQueueService
   - Persistent job queue
   - Retry logic, scaling

5. **Cloudflare Vectorize** - Free tier (5M ops/month)
   - Seed similarity search
   - Filter recommendations
   - Semantic search

### Optional
6. **Durable Objects** - Only if needed for WebSocket state
7. **AI Gateway** - Rate limiting (included with Workers AI)

**Estimated Monthly Cost**: $30-120 (scales with usage)

## Implementation Tasks

### Phase 1: Core (Week 1-2)
- [ ] Add R2 secret configuration to Motely.API/Motely.CLI
- [ ] Test SignalR real-time updates
- [ ] Add DuckDB seed source selector to Motely.TUI
- [ ] Test DuckLake from R2

### Phase 2: Queues (Week 3)
- [ ] Set up Cloudflare Queues
- [ ] Migrate SearchQueueService to use Queues
- [ ] Update Worker to consume from queue
- [ ] Test queue retry logic

### Phase 3: Vectorize (Week 4)
- [ ] Set up Cloudflare Vectorize
- [ ] Create index for seed metadata
- [ ] Implement similarity search
- [ ] Integrate with JamlGenie

### Phase 4: ErraticDeck.app (Week 5-6)
- [ ] Build React TypeScript app
- [ ] Implement DuckLake export
- [ ] Add R2 upload
- [ ] Test end-to-end

### Phase 5: MCP Server (Week 7)
- [ ] Review MCP implementation
- [ ] Fix JSON-RPC 2.0 compliance
- [ ] Test with Claude Desktop
- [ ] Add error handling

## Testing Commands

### Test DuckLake from R2
```powershell
cd external/Motely
dotnet run --project Motely.CLI -- --jaml showman-cloudnine --seedsource https://account.r2.cloudflarestorage.com/bucket/_Erratic_Deck__9s.ducklake
```

### Test SignalR
```powershell
cd external/Motely
dotnet run --project Motely.API
# Open http://localhost:3141/JAML/
# Start search, verify real-time updates
```

### Test MCP Server
```bash
# In Claude Desktop config
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "command": "dotnet",
      "args": ["run", "--project", "external/Motely/Motely.API"]
    }
  }
}
```

## Key Decisions Needed

1. **Budget**: How much for Cloudflare services? ($30-120/month estimate)
2. **ErraticDeck.app**: When needed? (Specs ready in ERRATICDECK_APP_SPEC.md)
3. **MCP Server**: Priority? (Currently broken/test example)
4. **Vectorize**: Want similarity search? (High value feature)

## Architecture Highlights

### Data Flow
```
ErraticDeck.app → DuckLake → R2 → Motely (multiple instances) → Results DuckDB → BalatroSeedOracle UI
```

### AI Flow
```
User Prompt → JamlGenie UI → Motely.API → Cloudflare Worker (AI) → JAML Filter → Search
```

### Real-Time Flow
```
Search Start → Cloudflare Queue → Worker → SignalR → JAML UI (real-time updates)
```

## Success Criteria

- ✅ Multiple Motely instances read same R2 seed source
- ✅ Real-time search updates via SignalR
- ✅ JamlGenie generates accurate filters
- ✅ ErraticDeck.app generates seed sources
- ✅ Vectorize enables similarity search
- ✅ Queues handle 100+ concurrent searches

## Questions to Answer

1. What's the budget for Cloudflare services?
2. When do you need ErraticDeck.app?
3. Is MCP Server (Claude Desktop) critical?
4. Do you want vector similarity search?

## Next Steps

1. **Read**: `BALATROSEEDORACLE_CLOUDFLARE_ECOSYSTEM.md`
2. **Review**: All documentation files
3. **Decide**: Which Cloudflare services to purchase
4. **Implement**: Start with R2 configuration
5. **Test**: SignalR, DuckLake, Queues
6. **Build**: ErraticDeck.app, fix MCP Server

---

## File Reference Summary

### Must Read
- `BALATROSEEDORACLE_CLOUDFLARE_ECOSYSTEM.md` - Complete overview
- `HANDOFF_TO_PARENT_WORKSPACE.md` - Quick start
- `ERRATICDECK_APP_SPEC.md` - ErraticDeck.app specs

### Implementation Guides
- `R2_INTEGRATION_GUIDE.md` - R2 setup
- `DUCKLAKE_CLOUD_ARCHITECTURE.md` - DuckLake details
- `CROSS_PLATFORM_ARCHITECTURE.md` - Multi-platform support

### Technical Details
- `DUCKDB_INPUT_OUTPUT_FLOW.md` - Data pipeline
- `MAPPED_APPENDER_ANALYSIS.md` - Appender comparison
- `DUCKLAKE_MOTHERDUCK_EXPLAINED.md` - DuckLake vs MotherDuck

### Code Files
- `Motely/Motely.DuckDB/DuckLakeHelper.cs` - DuckLake operations
- `Motely.API/Services/SearchQueueService.cs` - Queue service
- `Motely.API/Hubs/SearchHub.cs` - SignalR hub
- `Motely.API/McpProtocol/McpServer.cs` - MCP server

---

**Everything is documented and ready! Start with `BALATROSEEDORACLE_CLOUDFLARE_ECOSYSTEM.md`** 🚀
