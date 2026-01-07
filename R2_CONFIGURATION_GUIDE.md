# R2 Configuration Guide for Motely

## Quick Setup

### 1. Get R2 Credentials

1. Go to [Cloudflare Dashboard](https://dash.cloudflare.com/)
2. Navigate to **R2** → **Manage R2 API Tokens**
3. Create a new API token with:
   - **Permissions**: Object Read & Write
   - **TTL**: Optional (or leave blank for no expiration)
4. Copy:
   - **Access Key ID**
   - **Secret Access Key**
   - **Account ID** (found in R2 dashboard URL or account settings)

### 2. Configure appsettings.json

Edit `Motely.API/appsettings.json`:

```json
{
  "Cloudflare": {
    "R2": {
      "AccountId": "your-account-id-here",
      "AccessKeyId": "your-access-key-id-here",
      "SecretAccessKey": "your-secret-access-key-here",
      "Endpoint": "https://your-account-id.r2.cloudflarestorage.com",
      "Bucket": "balatro-seed-sources",
      "Enabled": true
    }
  }
}
```

### 3. Use Environment Variables (Recommended for Production)

Instead of storing secrets in `appsettings.json`, use environment variables:

```bash
# Windows PowerShell
$env:Cloudflare__R2__AccountId = "your-account-id"
$env:Cloudflare__R2__AccessKeyId = "your-access-key-id"
$env:Cloudflare__R2__SecretAccessKey = "your-secret-access-key"
$env:Cloudflare__R2__Enabled = "true"

# Linux/macOS
export Cloudflare__R2__AccountId="your-account-id"
export Cloudflare__R2__AccessKeyId="your-access-key-id"
export Cloudflare__R2__SecretAccessKey="your-secret-access-key"
export Cloudflare__R2__Enabled="true"
```

### 4. Configure R2 in Code

When creating DuckDB connections that need R2 access:

```csharp
using Motely.DuckDB;

// Load R2 config from appsettings
var r2Config = configuration.GetSection("Cloudflare:R2");
if (r2Config.GetValue<bool>("Enabled"))
{
    var connection = DuckDBConnectionFactory.CreateConnection(":memory:");
    
    // Configure R2 secret
    CloudStorageHelper.ConfigureR2Secret(
        connection,
        r2Config["AccessKeyId"]!,
        r2Config["SecretAccessKey"]!,
        r2Config["Endpoint"]!
    );
    
    // Now you can use R2 paths in queries!
    // Example: ATTACH 'ducklake:s3://bucket/catalog.ducklake' ...
}
```

## Usage Examples

### Reading DuckLake from R2

```csharp
// Configure R2 first
CloudStorageHelper.ConfigureR2Secret(connection, accessKeyId, secretKey, endpoint);

// Attach DuckLake from R2
var catalogPath = "s3://balatro-seed-sources/_Erratic_Deck__9s.ducklake";
var dataPath = "s3://balatro-seed-sources/_Erratic_Deck__9s_data/";

DuckLakeHelper.AttachDuckLake(connection, catalogPath, dataPath);
```

### Using in Motely.CLI

```bash
# R2 will be auto-configured if appsettings.json has R2 config
dotnet run --project Motely.CLI -- --jaml showman-cloudnine --seedsource s3://balatro-seed-sources/_Erratic_Deck__9s.ducklake
```

## Security Best Practices

1. **Never commit secrets** to git
   - Add `appsettings.json` to `.gitignore`
   - Use `appsettings.example.json` as template

2. **Use environment variables** in production
   - More secure than config files
   - Easy to rotate without code changes

3. **Limit R2 token permissions**
   - Only grant necessary permissions
   - Use separate tokens for read vs write

4. **Rotate tokens regularly**
   - Update tokens every 90 days
   - Revoke old tokens immediately

## Troubleshooting

### Error: "httpfs extension not found"
**Solution**: `CloudStorageHelper.InstallHttpfsExtension()` is called automatically by `ConfigureR2Secret()`

### Error: "Access Denied"
**Solution**: Check:
- Access Key ID is correct
- Secret Access Key is correct
- Token has proper permissions
- Bucket name is correct

### Error: "Endpoint not found"
**Solution**: Verify endpoint format:
- Should be: `https://{accountId}.r2.cloudflarestorage.com`
- Account ID must match your Cloudflare account

## Testing R2 Configuration

```csharp
// Test R2 connection
var connection = DuckDBConnectionFactory.CreateConnection(":memory:");
CloudStorageHelper.ConfigureR2Secret(connection, accessKeyId, secretKey, endpoint);

// Try listing files in bucket
using var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT * FROM glob('s3://balatro-seed-sources/*.ducklake') LIMIT 10;";
using var reader = cmd.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine(reader.GetString(0));
}
```

## Next Steps

1. ✅ Configure R2 in `appsettings.json`
2. ✅ Test R2 connection
3. ✅ Upload seed source to R2
4. ✅ Test reading from R2 in Motely.CLI
5. ✅ Test DuckLake from R2

See `R2_INTEGRATION_GUIDE.md` for complete R2 integration details.
