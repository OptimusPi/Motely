# DuckLake + Cloud Storage Architecture for BalatroSeedOracle

## Overview

This document outlines the complete architecture for using DuckLake with cloud storage (Cloudflare R2) to enable:
- **Multiplayer DuckDB**: Multiple processes reading/writing the same seed sources
- **Cloud Distribution**: Seed sources accessible from anywhere via R2
- **Local Development**: DuckDB UI extension for debugging and exploration

## Architecture Components

### 1. DuckLake Format
- **Catalog Database**: SQLite/DuckDB file (`.ducklake`) containing metadata
- **Data Storage**: Parquet files in local directory or cloud storage (R2/S3)
- **Concurrent Access**: Multiple DuckDB instances can attach to the same DuckLake

### 2. Storage Options

#### Local Development
```
SeedSources/
├── _Erratic_Deck__9s.ducklake          # Catalog file
└── _Erratic_Deck__9s_data/             # Parquet files
    └── main/
        └── seeds/
            └── ducklake-*.parquet
```

#### Cloud Distribution (R2)
```
R2 Bucket: balatro-seed-sources
├── _Erratic_Deck__9s.ducklake          # Catalog file (HTTPS accessible)
└── _Erratic_Deck__9s_data/             # Parquet files
    └── main/
        └── seeds/
            └── ducklake-*.parquet
```

## Implementation Status

### ✅ Completed
- `DuckLakeHelper.cs` - Core DuckLake operations
- `CloudStorageHelper.cs` - R2/S3 path utilities
- `DuckDBConnectionFactory` - DuckLake attach support
- Remote data path support (R2, S3, HTTPS)

### 🚧 In Progress
- `DuckDBSeedProvider` - Auto-detect DuckLake vs legacy
- `DuckDBHelper` - Create DuckLake by default
- Migration utilities

### 📋 Planned
- Migration script for existing seed sources
- R2 upload automation
- DuckDB UI integration for dev

## Usage Patterns

### Local DuckLake (Multiplayer)
```csharp
// Multiple CLI instances can now read the same seed source!
var provider = new DuckDBSeedProvider("SeedSources/_Erratic_Deck__9s");
// No file locking - concurrent access enabled!
```

### Remote DuckLake (R2)
```csharp
// Seed source stored in Cloudflare R2
var catalogUrl = "https://your-account.r2.cloudflarestorage.com/bucket/_Erratic_Deck__9s.ducklake";
var dataPath = "s3://bucket/_Erratic_Deck__9s_data/";

var provider = new DuckDBSeedProvider(catalogUrl, dataPath);
// Reads directly from R2 - no local storage needed!
```

### Development with DuckDB UI
```bash
# Start DuckDB with UI for local exploration
duckdb -ui SeedSources/_Erratic_Deck__9s.ducklake

# Or from SQL
CALL start_ui();
# Opens http://localhost:4213 in browser
```

## Cloudflare R2 Integration

### Setup
1. **R2 Bucket**: `balatro-seed-sources`
2. **Public Access**: Catalog files (`.ducklake`) should be publicly readable
3. **Data Files**: Parquet files can be public or use R2 credentials

### Upload Workflow
```powershell
# 1. Create DuckLake locally
dotnet run --project Motely.CLI -- --convert-seedsource _Erratic_Deck__9s

# 2. Upload to R2
r2 upload balatro-seed-sources SeedSources/_Erratic_Deck__9s.ducklake
r2 upload balatro-seed-sources SeedSources/_Erratic_Deck__9s_data/ --recursive

# 3. Use remote URL
dotnet run --project Motely.CLI -- --seedsource https://account.r2.cloudflarestorage.com/bucket/_Erratic_Deck__9s.ducklake
```

### R2 Configuration
```csharp
// DuckDB automatically handles R2 via S3-compatible API
// No special configuration needed - just use s3:// paths
var r2Path = CloudStorageHelper.BuildR2S3Path("balatro-seed-sources", "path/to/data");
// Returns: s3://balatro-seed-sources/path/to/data
```

## Benefits for BalatroSeedOracle

### 1. **Multiplayer Access**
- Multiple CLI instances searching the same seed source
- API server serving seed sources to multiple clients
- No more "database is locked" errors

### 2. **Cloud Distribution**
- Seed sources accessible from anywhere
- No need to download large `.db` files
- Automatic updates when new seeds are added

### 3. **Development Experience**
- DuckDB UI for exploring seed sources locally
- Visual query builder
- Performance profiling

### 4. **Scalability**
- Partition large seed sources by length/prefix
- Efficient Parquet storage
- Fast queries with filter pushdown

## Migration Path

### Phase 1: Local DuckLake (Current)
- Convert existing `.db` files to DuckLake format
- Enable multiplayer access locally
- Keep backward compatibility with legacy `.db`

### Phase 2: Cloud Upload (Next)
- Upload popular seed sources to R2
- Update CLI/API to support remote URLs
- Add automatic fallback (local → R2)

### Phase 3: Full Cloud (Future)
- All seed sources in R2
- CDN caching for catalog files
- Automatic sync from local to cloud

## Code Examples

### Creating DuckLake from CSV
```csharp
DuckLakeHelper.CreateDuckLakeFromSeedFile(
    sourcePath: "SeedSources/erratic_9s.csv",
    catalogPath: "SeedSources/_Erratic_Deck__9s.ducklake",
    dataPath: "SeedSources/_Erratic_Deck__9s_data"
);
```

### Attaching Remote DuckLake
```csharp
var conn = DuckDBConnectionFactory.CreateConnectionWithDuckLake(
    catalogPath: "https://account.r2.cloudflarestorage.com/bucket/_Erratic_Deck__9s.ducklake",
    dataPath: "s3://bucket/_Erratic_Deck__9s_data/",
    overrideDataPath: true  // Override catalog's data path
);
```

### Using DuckDB UI for Development
```csharp
// In development, start UI server
using var conn = DuckDBConnectionFactory.CreateConnection(":memory:");
using var cmd = conn.CreateCommand();
cmd.CommandText = "CALL start_ui_server();";
cmd.ExecuteNonQuery();
// Navigate to http://localhost:4213
```

## References
- [DuckLake Specification](https://ducklake.select/docs/stable/specification/introduction)
- [DuckLake Remote Data Path Guide](https://ducklake.select/docs/stable/duckdb/guides/using_a_remote_data_path)
- [DuckDB UI Extension](https://duckdb.org/docs/stable/core_extensions/ui)
- [Cloudflare R2 Documentation](https://developers.cloudflare.com/r2/)
- [DuckDB Appender Docs](https://duckdb.org/docs/stable/data/appender)
