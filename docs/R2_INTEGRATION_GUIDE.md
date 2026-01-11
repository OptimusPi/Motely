# Cloudflare R2 Integration Guide for BalatroSeedOracle

## Overview
This guide covers integrating Cloudflare R2 with DuckDB and DuckLake for cross-platform seed source distribution.

## R2 Setup

### 1. Create R2 Bucket
```bash
# Using Wrangler CLI
wrangler r2 bucket create balatro-seed-sources
```

### 2. Generate S3-Compatible Credentials
1. Go to Cloudflare Dashboard → R2 → Manage R2 API Tokens
2. Create API Token with:
   - **Permissions**: Object Read & Write
   - **TTL**: Optional (or no expiration)
3. Save:
   - **Access Key ID**
   - **Secret Access Key**
   - **Account ID** (found in R2 dashboard URL)

## DuckDB R2 Configuration

### Desktop (C# .NET)
```csharp
using var conn = DuckDBConnectionFactory.CreateConnection(":memory:");

// Install httpfs extension (one-time per connection)
using var cmd = conn.CreateCommand();
cmd.CommandText = "INSTALL httpfs; LOAD httpfs;";
cmd.ExecuteNonQuery();

// Create R2 secret
cmd.CommandText = @"
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

// Now you can query R2!
cmd.CommandText = "SELECT * FROM read_parquet('r2://balatro-seed-sources/path/to/file.parquet');";
```

### WebAssembly (Browser)
```typescript
import * as duckdb from '@duckdb/duckdb-wasm';

const DUCKDB_CONFIG = {
    mainModule: '/path/to/duckdb.wasm',
    mvp: { mainModule: '/path/to/duckdb-mvp.wasm' }
};

const db = await duckdb.createAsyncDatabase(DUCKDB_CONFIG);
const conn = await db.connect();

// Install httpfs
await conn.query("INSTALL httpfs; LOAD httpfs;");

// Create R2 secret (use environment variables in production!)
await conn.query(`
    CREATE SECRET (
        TYPE r2,
        KEY_ID '${R2_ACCESS_KEY_ID}',
        SECRET '${R2_SECRET_ACCESS_KEY}',
        ACCOUNT_ID '${CLOUDFLARE_ACCOUNT_ID}'
    );
`);

// Query R2
const result = await conn.query(`
    SELECT * FROM read_parquet('r2://balatro-seed-sources/path/to/file.parquet');
`);
```

## DuckLake with R2

### Upload DuckLake to R2
```powershell
# Using Wrangler CLI
wrangler r2 object put balatro-seed-sources/_Erratic_Deck__9s.ducklake --file=SeedSources/_Erratic_Deck__9s.ducklake
wrangler r2 object put balatro-seed-sources/_Erratic_Deck__9s_data/ --file=SeedSources/_Erratic_Deck__9s_data/ --recursive
```

### Read DuckLake from R2
```csharp
// In Motely
var catalogUrl = "https://your-account-id.r2.cloudflarestorage.com/balatro-seed-sources/_Erratic_Deck__9s.ducklake";
var dataPath = "r2://balatro-seed-sources/_Erratic_Deck__9s_data/";

var conn = DuckDBConnectionFactory.CreateConnectionWithDuckLake(
    catalogPath: catalogUrl,
    dataPath: dataPath,
    overrideDataPath: true  // Override catalog's local path with R2 path
);
```

## Environment Variables

### Desktop Apps (Motely.CLI, Motely.TUI)
```bash
# .env or appsettings.json
R2_ACCESS_KEY_ID=your-key-id
R2_SECRET_ACCESS_KEY=your-secret-key
CLOUDFLARE_ACCOUNT_ID=your-account-id
R2_BUCKET_NAME=balatro-seed-sources
```

### Web Apps (ErraticDeck.app, The Daily Wee)
```bash
# Cloudflare Pages/Workers environment variables
R2_ACCESS_KEY_ID=your-key-id
R2_SECRET_ACCESS_KEY=your-secret-key
CLOUDFLARE_ACCOUNT_ID=your-account-id
R2_BUCKET_NAME=balatro-seed-sources
```

## Security Best Practices

### Public Catalog Files
- **`.ducklake` catalog files**: Should be publicly readable
- **Why**: Allows Motely to discover and attach DuckLake without authentication
- **How**: Set R2 bucket policy or make specific objects public

### Private Data Files
- **Parquet data files**: Can be private (use R2 secrets)
- **Why**: Control access to actual seed data
- **How**: Use R2 secrets in DuckDB for authenticated access

### Alternative: All Public
- If seed sources are not sensitive, make entire bucket public
- Simpler setup, no authentication needed
- Use R2 public URL format: `https://account-id.r2.cloudflarestorage.com/bucket/path`

## Testing R2 Integration

### 1. Test R2 Access
```sql
-- In DuckDB
INSTALL httpfs; LOAD httpfs;
CREATE SECRET (TYPE r2, KEY_ID '...', SECRET '...', ACCOUNT_ID '...');
SELECT * FROM read_parquet('r2://balatro-seed-sources/test.parquet');
```

### 2. Test DuckLake from R2
```csharp
// In Motely
var provider = new DuckDBSeedProvider(
    "https://account.r2.cloudflarestorage.com/bucket/_Erratic_Deck__9s.ducklake"
);
// Should successfully read seeds from R2!
```

### 3. Test Concurrent Access
```bash
# Run multiple Motely instances reading from same R2 source
dotnet run --project Motely.CLI -- --seedsource https://...r2.../_Erratic_Deck__9s.ducklake &
dotnet run --project Motely.CLI -- --seedsource https://...r2.../_Erratic_Deck__9s.ducklake &
# Both should work simultaneously!
```

## Troubleshooting

### "Failed to attach DuckLake"
- **Check**: R2 bucket name and paths are correct
- **Check**: Catalog file is publicly accessible
- **Check**: R2 secret is configured correctly

### "Access Denied" when reading Parquet
- **Check**: R2 secret credentials are valid
- **Check**: Parquet files exist in R2 bucket
- **Check**: Path format: `r2://bucket-name/path/to/file.parquet`

### "Extension not found: httpfs"
- **Fix**: Run `INSTALL httpfs; LOAD httpfs;` before using R2

## References
- [DuckDB R2 Import Guide](https://duckdb.org/docs/stable/guides/network_cloud_storage/cloudflare_r2_import)
- [Cloudflare R2 Documentation](https://developers.cloudflare.com/r2/)
- [DuckLake Remote Data Path Guide](https://ducklake.select/docs/stable/duckdb/guides/using_a_remote_data_path)
