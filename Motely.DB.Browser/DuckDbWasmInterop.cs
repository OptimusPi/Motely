using System.Runtime.InteropServices.JavaScript;

namespace Motely.DB;

/// <summary>
/// [JSImport] bridge to the DuckDB WASM engine running in the browser.
/// These map 1:1 to globalThis.duckLake* functions defined in duckdb-lake.js.
/// </summary>
internal static partial class DuckDbWasmInterop
{
    /// <summary>Initialize DuckDB WASM with httpfs extension.</summary>
    [JSImport("globalThis.duckLakeInit")]
    internal static partial Task<bool> InitAsync();

    /// <summary>Configure S3/R2 credentials for remote lake access.</summary>
    [JSImport("globalThis.duckLakeConfigureS3")]
    internal static partial Task<bool> ConfigureS3Async(
        string region,
        string endpoint,
        string accessKeyId,
        string secretAccessKey);

    /// <summary>Execute arbitrary SQL and get JSON results.</summary>
    [JSImport("globalThis.duckLakeQuery")]
    internal static partial Task<string> QueryAsync(string sql);

    /// <summary>Query a remote Parquet file with optional WHERE and LIMIT.</summary>
    [JSImport("globalThis.duckLakeQueryParquet")]
    internal static partial Task<string> QueryParquetAsync(
        string parquetUrl,
        string sqlWhere,
        int limit);

    /// <summary>Count rows in a remote Parquet file.</summary>
    [JSImport("globalThis.duckLakeCountParquet")]
    internal static partial Task<int> CountParquetAsync(string parquetUrl);
}
