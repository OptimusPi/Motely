# BalatroSeedOracle Cross-Platform Architecture

## Platform Targets
- ✅ **WebAssembly** (Browser, Cloudflare Workers)
- ✅ **Windows 11** (Desktop)
- ✅ **macOS** (Desktop)
- ✅ **Linux** (Desktop, Server)
- ✅ **iOS** (Native App)
- ✅ **Android** (Native App)

## Project Structure

```
BalatroSeedOracle/
├── external/
│   └── Motely/                    # Git submodule (fork of tacodiva/Motely)
│       ├── Motely/                # Core engine (C#)
│       ├── Motely.CLI/            # CLI searches → DuckDB output
│       ├── Motely.TUI/            # Graphical TUI (needs DuckDB seed source selector)
│       ├── Motely.API/            # C# .NET 10 Minimal API
│       └── Motely.Tests/
│
├── TheDailyWee/                   # WeeJoker.App (Cloudflare Pages + D1)
└── ErraticDeck.app/              # Empty - needs to be built
```

## Component Responsibilities

### Motely.CLI
- **Current**: CLI seed searches, saves to DuckDB with `--output-db`
- **Platform**: Windows/macOS/Linux (native)
- **DuckDB**: ✅ Already integrated

### Motely.TUI
- **Current**: Graphical TUI for seed searching
- **Needs**: DuckDB seed source selector (replaces `--wordlist`)
- **Platform**: Windows/macOS/Linux (Terminal.GUI v2)
- **DuckDB**: ⚠️ Needs integration

### Motely.API
- **Current**: C# .NET 10 Minimal API
- **Platform**: Server (Windows/macOS/Linux)
- **DuckDB**: ✅ Already integrated
- **Serves**: Desktop apps, websites, mobile apps

### The Daily Wee (WeeJoker.App)
- **Current**: Cloudflare Pages + D1 database
- **Platform**: Web (Cloudflare)
- **Integration**: Uses seeds from Motely searches
- **Database**: D1 (SQLite-compatible)

### ErraticDeck.app
- **Current**: Empty website
- **Needs**: Full implementation
- **Platform**: Web (Cloudflare Pages)
- **Integration**: Generates seed sources → DuckLake → R2

## Cross-Platform DuckDB Strategy

### WebAssembly (Browser, Cloudflare Workers)
- **DuckDB-Wasm**: Use `@duckdb/duckdb-wasm` package
- **Limitations**: 
  - No file system access
  - Use HTTP/HTTPS for data loading
  - DuckLake over R2/S3 works perfectly!
- **Resources**:
  - [DuckDB Wasm Documentation](https://duckdb.org/docs/stable/api/wasm)
  - [Using DuckDB WASM + Cloudflare R2](https://github.com/davidgasquez/awesome-duckdb#blog-posts)

### Native Desktop (Windows/macOS/Linux)
- **DuckDB.NET**: Full-featured .NET binding
- **DuckLake**: Full support for multiplayer access
- **File System**: Direct access to `.db` and `.ducklake` files

### Mobile (iOS/Android)
- **DuckDB.NET**: Works on both platforms
- **DuckLake**: Full support
- **Storage**: App sandbox directories
- **Cloud**: Read from R2 via DuckLake remote paths

## Key Resources from Awesome DuckDB

### For WebAssembly
- **DuckDB-Wasm**: Core WebAssembly build
- **Cloudflare R2 Integration**: Store seed sources in R2, query via DuckDB-Wasm
  - [DuckDB R2 Import Guide](https://duckdb.org/docs/stable/guides/network_cloud_storage/cloudflare_r2_import)
  - Use S3-compatible API with R2 secrets
- **HTTP/HTTPS Support**: Load data from remote URLs
- **DuckLake-Wasm**: Create DuckLake catalogs in browser

### For Mobile
- **DuckDB.NET**: Cross-platform .NET binding
- **DuckLake**: Enables cloud-synced seed sources
- **App Sandbox**: Store databases in app-specific directories
- **R2 Access**: Read seed sources from R2 via DuckLake remote paths

### For Cross-Platform Development
- **DuckDB Extensions**: 
  - **VSS** (Vector Similarity Search) - Could be useful for seed similarity
  - **Arrow** - Efficient data interchange ([DuckDB Arrow](https://github.com/duckdb/arrow))
  - **DuckLake** - Multiplayer access ([DuckDB DuckLake](https://github.com/duckdb/ducklake))
  - **httpfs** - Required for R2/S3 access

### Cloudflare-Specific
- **R2 S3 Compatibility**: DuckDB uses S3 API, R2 is S3-compatible
- **R2 Secrets**: Configure via `CREATE SECRET (TYPE r2, ...)`
- **Public Catalog Files**: `.ducklake` files should be publicly readable
- **Authenticated Data**: Parquet files can use R2 credentials

## Integration Points

### 1. Motely.TUI → DuckDB Seed Source Selector
**Current State**: Uses `--wordlist` parameter
**Needed**: UI dropdown to select from:
- Local `.db` files
- Local `.ducklake` files
- Remote R2 DuckLake URLs

**Implementation**:
```csharp
// In Motely.TUI
var seedSources = DuckDBHelper.ListSeedSources("SeedSources/");
// Show in UI dropdown
```

### 2. The Daily Wee → Motely Search Results
**Current**: Uses D1 database for high scores
**Integration**: Query Motely.API for seed searches
**Data Flow**: Motely.API → D1 (via API calls)

### 3. ErraticDeck.app → DuckLake → R2
**Needed**: 
- Generate seed sources from Erratic Deck analysis
- Export to DuckLake format
- Upload to R2
- Motely reads from R2

## Knowledge Transfer Strategy

See `ERRATICDECK_APP_SPEC.md` for detailed specifications.
