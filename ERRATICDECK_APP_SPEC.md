# ErraticDeck.app - Complete Specification

## Overview
**ErraticDeck.app** is a web application for analyzing Erratic Deck seeds in Balatro. It generates seed sources that are consumed by Motely for seed searching.

## Architecture

### Tech Stack
- **Frontend**: React TypeScript (to match WeeJoker.app consistency)
- **Backend**: Cloudflare Pages (static) + Cloudflare Workers (API)
- **Database**: Cloudflare D1 (SQLite-compatible) for user data
- **Storage**: Cloudflare R2 for seed source distribution
- **Data Format**: DuckLake (for multiplayer access)

### Data Flow
```
ErraticDeck.app (User Input)
    ↓ Analyzes Erratic Deck seeds
    ↓ Generates seed source data
    ↓ Exports to DuckLake format
    ↓
Cloudflare R2 (balatro-seed-sources bucket)
    ├── _Erratic_Deck__9s.ducklake (catalog)
    └── _Erratic_Deck__9s_data/ (Parquet files)
    ↓
Motely (Multiple instances)
    ↓ Reads from R2 via DuckLake
    ↓ No file locking, concurrent access!
```

## Core Features

### 1. Erratic Deck Analysis
- **Input**: User provides deck configuration or seed
- **Analysis**: Calculate Erratic Deck card distribution
- **Output**: List of valid seeds matching criteria

### 2. Seed Source Generation
- **Format**: DuckLake (`.ducklake` catalog + Parquet data files)
- **Schema**: 
  ```sql
  CREATE TABLE seeds (
      id BIGINT,
      seed VARCHAR(8)
  );
  ```
- **Validation**: Only valid Balatro seeds (1-8 chars, 1-9/A-Z, no 0)

### 3. R2 Upload Integration
- **Bucket**: `balatro-seed-sources`
- **Path Structure**: `{seed-source-name}.ducklake` + `{seed-source-name}_data/`
- **Public Access**: Catalog files should be publicly readable
- **Authentication**: Use R2 S3-compatible API

## Implementation Details

### DuckDB R2 Integration
Based on [DuckDB R2 Import Guide](https://duckdb.org/docs/stable/guides/network_cloud_storage/cloudflare_r2_import):

```sql
-- Install httpfs extension (one-time)
INSTALL httpfs;

-- Create R2 secret
CREATE SECRET (
    TYPE r2,
    KEY_ID 'your-r2-access-key-id',
    SECRET 'your-r2-secret-access-key',
    ACCOUNT_ID 'your-cloudflare-account-id'
);

-- Query R2 data
SELECT * FROM read_parquet('r2://balatro-seed-sources/_Erratic_Deck__9s_data/main/seeds/*.parquet');
```

### DuckLake Export from ErraticDeck
```typescript
// Pseudo-code for ErraticDeck.app export
async function exportToDuckLake(seeds: string[], sourceName: string) {
    // 1. Create DuckLake catalog locally
    const catalogPath = `${sourceName}.ducklake`;
    const dataPath = `${sourceName}_data`;
    
    // 2. Use DuckDB-Wasm to create DuckLake
    const conn = await duckdb.connect();
    await conn.query(`
        INSTALL ducklake;
        LOAD ducklake;
        ATTACH 'ducklake:${catalogPath}' AS export (DATA_PATH '${dataPath}/');
        CREATE TABLE export.seeds AS
        SELECT ROW_NUMBER() OVER (ORDER BY LENGTH(seed), seed) - 1 AS id, seed
        FROM (VALUES ${seeds.map(s => `('${s}')`).join(',')}) AS t(seed);
    `);
    
    // 3. Upload to R2
    await uploadToR2(catalogPath, `r2://balatro-seed-sources/${catalogPath}`);
    await uploadToR2(dataPath, `r2://balatro-seed-sources/${dataPath}/`, true);
}
```

## API Endpoints

### Generate Seed Source
```
POST /api/generate-seed-source
Body: {
    name: "Erratic_Deck__9s",
    criteria: { rank: "9", minCount: 10 },
    format: "ducklake"
}
Response: {
    catalogUrl: "https://account.r2.cloudflarestorage.com/bucket/Erratic_Deck__9s.ducklake",
    dataUrl: "r2://balatro-seed-sources/Erratic_Deck__9s_data/",
    seedCount: 1234567
}
```

### List Seed Sources
```
GET /api/seed-sources
Response: [
    {
        name: "Erratic_Deck__9s",
        catalogUrl: "https://...",
        seedCount: 1234567,
        createdAt: "2025-01-06T..."
    }
]
```

## Frontend UI

### Main Page
- **Deck Input**: Text area or file upload for deck configuration
- **Analysis Options**: 
  - Rank filter (2, 3, 4, ..., A, K, Q, J)
  - Minimum count
  - Suit filter (optional)
- **Generate Button**: Creates seed source and uploads to R2

### Seed Source Browser
- **List View**: All available seed sources in R2
- **Details**: Seed count, creation date, download link
- **Use in Motely**: Copy R2 URL for use in Motely searches

## Integration with Motely

### Motely Reads from R2
```csharp
// In Motely, use R2 DuckLake URL
var provider = new DuckDBSeedProvider(
    catalogPath: "https://account.r2.cloudflarestorage.com/bucket/_Erratic_Deck__9s.ducklake",
    dataPath: "r2://balatro-seed-sources/_Erratic_Deck__9s_data/"
);
```

### R2 Secret Configuration
```csharp
// In Motely.API or Motely.CLI
using var conn = DuckDBConnectionFactory.CreateConnection(":memory:");
using var cmd = conn.CreateCommand();
cmd.CommandText = @"
    INSTALL httpfs;
    LOAD httpfs;
    CREATE SECRET (
        TYPE r2,
        KEY_ID ?,
        SECRET ?,
        ACCOUNT_ID ?
    );
";
cmd.Parameters.Add(new DuckDBParameter(r2AccessKeyId));
cmd.Parameters.Add(new DuckDBParameter(r2SecretAccessKey));
cmd.Parameters.Add(new DuckDBParameter(cloudflareAccountId));
cmd.ExecuteNonQuery();
```

## Knowledge Transfer for Google Antigravity

### Key Concepts
1. **DuckLake**: Lightweight lakehouse format for multiplayer DuckDB
2. **R2 Integration**: Use S3-compatible API with DuckDB
3. **Seed Source Format**: DuckLake with `seeds` table (id, seed)
4. **Motely Integration**: Motely reads from R2 via DuckLake URLs

### Required Reading
- [DuckLake Specification](https://ducklake.select/docs/stable/specification/introduction)
- [DuckDB R2 Import Guide](https://duckdb.org/docs/stable/guides/network_cloud_storage/cloudflare_r2_import)
- [DuckDB-Wasm Documentation](https://duckdb.org/docs/stable/api/wasm) (for browser-based export)

### Code Examples
See `DUCKLAKE_CLOUD_ARCHITECTURE.md` for complete integration examples.

### Testing Strategy
1. **Local Testing**: Create DuckLake locally, verify structure
2. **R2 Upload**: Upload to R2, verify public access
3. **Motely Integration**: Test Motely reading from R2 URL
4. **Concurrent Access**: Test multiple Motely instances reading same R2 source

## Deployment

### Cloudflare Pages
- **Build**: React TypeScript app
- **Environment Variables**:
  - `R2_ACCESS_KEY_ID`
  - `R2_SECRET_ACCESS_KEY`
  - `CLOUDFLARE_ACCOUNT_ID`
  - `R2_BUCKET_NAME=balatro-seed-sources`

### Cloudflare Workers (API)
- **Routes**: `/api/*`
- **R2 Access**: Use R2 bindings for uploads
- **DuckDB-Wasm**: For DuckLake generation in browser

## Success Criteria
- ✅ Generate seed sources from Erratic Deck analysis
- ✅ Export to DuckLake format
- ✅ Upload to R2 bucket
- ✅ Motely can read from R2 URLs
- ✅ Multiple Motely instances can read concurrently
- ✅ Public catalog files, authenticated data files (optional)
