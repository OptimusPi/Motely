# Handoff Document: Motely → BalatroSeedOracle

## Quick Start for Parent Workspace

This document provides everything needed to continue development in the **BalatroSeedOracle** parent workspace.

## Current Status Summary

### ✅ Completed in Motely Submodule
1. **DuckLake Implementation** - Full multiplayer DuckDB support
2. **R2 Integration** - Cloud storage support (code ready, needs config)
3. **DuckDB Architecture** - Centralized schema, operations, helpers
4. **Documentation** - Complete architecture docs

### ⚠️ Needs Implementation
1. **R2 Secret Configuration** - Add to Motely.API/Motely.CLI
2. **Motely.TUI DuckDB Selector** - Replace `--wordlist` with UI dropdown
3. **SignalR Testing** - Test real-time updates (code exists, untested)
4. **MCP Server Fix** - Review and fix JSON-RPC 2.0 compliance
5. **Cloudflare Queues** - Migrate SearchQueueService
6. **Cloudflare Vectorize** - Add similarity search
7. **ErraticDeck.app** - Build from scratch (specs ready)

## Key Files to Review

### Architecture Documentation
```
external/Motely/
├── BALATROSEEDORACLE_CLOUDFLARE_ECOSYSTEM.md  ← START HERE
├── DUCKLAKE_CLOUD_ARCHITECTURE.md
├── R2_INTEGRATION_GUIDE.md
├── ERRATICDECK_APP_SPEC.md
├── CROSS_PLATFORM_ARCHITECTURE.md
├── DUCKDB_INPUT_OUTPUT_FLOW.md
└── MAPPED_APPENDER_ANALYSIS.md
```

### Implementation Files
```
external/Motely/
├── Motely/Motely.DuckDB/
│   ├── DuckLakeHelper.cs          # DuckLake operations
│   ├── CloudStorageHelper.cs       # R2 utilities
│   └── DuckDBConnectionFactory.cs  # R2 attach support
│
├── Motely.API/
│   ├── Services/SearchQueueService.cs  # Migrate to Cloudflare Queues
│   ├── Hubs/SearchHub.cs              # SignalR (needs testing)
│   └── McpProtocol/McpServer.cs      # MCP (needs fixing)
│
└── Motely.TUI/
    └── (needs DuckDB seed source selector)
```

## Recommended Cloudflare Services

### Essential (Buy These!)
1. **R2** - ✅ Already have (seed source storage)
2. **D1** - ✅ Already have (The Daily Wee)
3. **Workers AI** - ✅ Already using (JamlGenie)

### High Priority (Add These!)
4. **Cloudflare Queues** - Search job processing ($0.40/1M ops)
5. **Cloudflare Vectorize** - Similarity search (Free tier: 5M ops/month)

### Optional
6. **Durable Objects** - Only if needed for WebSocket state
7. **AI Gateway** - Rate limiting for Workers AI

## Implementation Priority

1. **Week 1**: R2 configuration, SignalR testing, Motely.TUI selector
2. **Week 2**: Cloudflare Queues setup and migration
3. **Week 3**: Cloudflare Vectorize setup
4. **Week 4-5**: ErraticDeck.app development
5. **Week 6**: MCP Server fix and testing

## Quick Commands

### Test DuckLake from R2
```powershell
# In Motely submodule
dotnet run --project Motely.CLI -- --jaml showman-cloudnine --seedsource https://account.r2.cloudflarestorage.com/bucket/_Erratic_Deck__9s.ducklake
```

### Convert Seed Source to DuckLake
```csharp
// Use DuckLakeHelper.ConvertLegacyToDuckLake()
// Or DuckLakeHelper.CreateDuckLakeFromSeedFile()
```

### Test SignalR
```bash
# Start Motely.API
dotnet run --project Motely.API
# Open JAML UI, start search, verify real-time updates
```

## Questions to Answer

1. **Budget**: How much for Cloudflare services? (Estimate: $30-120/month)
2. **Timeline**: When do you need ErraticDeck.app?
3. **MCP Server**: Is Claude Desktop integration critical?
4. **Vectorize**: Do you want similarity search features?

## Next Action Items

1. **Read**: `BALATROSEEDORACLE_CLOUDFLARE_ECOSYSTEM.md` (complete overview)
2. **Review**: `ERRATICDECK_APP_SPEC.md` (if building ErraticDeck.app)
3. **Implement**: Start with R2 configuration (see `R2_INTEGRATION_GUIDE.md`)
4. **Test**: SignalR real-time updates
5. **Decide**: Which Cloudflare services to purchase

---

**All documentation is in `external/Motely/` - ready for parent workspace!** 🎯
