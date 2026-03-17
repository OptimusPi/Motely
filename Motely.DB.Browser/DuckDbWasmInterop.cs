using System.Runtime.InteropServices.JavaScript;

namespace Motely.DB;

/// <summary>
/// [JSImport] bridge to the DuckDB WASM engine running in the browser.
/// These map 1:1 to globalThis.duckDb* functions defined in duckdb-wasm.js.
/// </summary>
internal static partial class DuckDbWasmInterop
{
    /// <summary>Initialize DuckDB WASM with httpfs extension.</summary>
    [JSImport("globalThis.duckDbInit")]
    internal static partial Task<bool> InitAsync();

    /// <summary>Configure S3/R2 credentials for remote Parquet access.</summary>
    [JSImport("globalThis.duckDbConfigureS3")]
    internal static partial Task<bool> ConfigureS3Async(
        string region,
        string endpoint,
        string accessKeyId,
        string secretAccessKey);

    /// <summary>Execute arbitrary SQL and get JSON results.</summary>
    [JSImport("globalThis.duckDbQuery")]
    internal static partial Task<string> QueryAsync(string sql);

    /// <summary>Query a remote Parquet file with optional WHERE, ORDER BY, and LIMIT.</summary>
    [JSImport("globalThis.duckDbQueryParquet")]
    internal static partial Task<string> QueryParquetAsync(
        string parquetUrl,
        string sqlWhere,
        string orderBy,
        int limit);

    /// <summary>Count rows in a remote Parquet file.</summary>
    [JSImport("globalThis.duckDbCountParquet")]
    internal static partial Task<int> CountParquetAsync(string parquetUrl);

    /// <summary>Release DuckDB WASM resources. Call on page unload.</summary>
    [JSImport("globalThis.duckDbCleanup")]
    internal static partial Task CleanupAsync();
}
