// duckdb-lake.js — DuckDB WASM bridge for Avalonia Browser interop
// Loaded lazily by the C# [JSImport] interop layer.
// This runs OUTSIDE the .NET WASM — it's pure browser JS using the @duckdb/duckdb-wasm npm CDN bundle.

let db = null;
let conn = null;

/**
 * Initialize DuckDB WASM with httpfs for remote Parquet querying.
 * Called once from C# via [JSImport].
 * @returns {Promise<boolean>} true if initialization succeeded
 */
globalThis.duckLakeInit = async function () {
    if (db !== null) return true;

    try {
        // Import DuckDB WASM from CDN (jsdelivr serves the npm package)
        const DUCKDB_CDN = 'https://cdn.jsdelivr.net/npm/@duckdb/duckdb-wasm@latest/dist';

        const duckdb = await import(`${DUCKDB_CDN}/duckdb-browser-blocking.mjs`);

        const MANUAL_BUNDLES = {
            mvp: {
                mainModule: `${DUCKDB_CDN}/duckdb-mvp.wasm`,
                mainWorker: `${DUCKDB_CDN}/duckdb-browser-mvp.worker.js`,
            },
            eh: {
                mainModule: `${DUCKDB_CDN}/duckdb-eh.wasm`,
                mainWorker: `${DUCKDB_CDN}/duckdb-browser-eh.worker.js`,
            },
        };

        // Use the official selectBundle helper to automatically choose MVP vs EH
        const bundle = await duckdb.selectBundle(MANUAL_BUNDLES);

        const worker = new Worker(bundle.mainWorker);
        const logger = new duckdb.ConsoleLogger();
        db = new duckdb.AsyncDuckDB(logger, worker);
        await db.instantiate(bundle.mainModule, bundle.mainWorker);

        conn = await db.connect();

        // Load httpfs for remote Parquet access
        await conn.query("INSTALL httpfs; LOAD httpfs;");

        console.log('[DuckLake] DuckDB WASM initialized with httpfs');
        return true;
    } catch (err) {
        console.error('[DuckLake] Init failed:', err);
        db = null;
        conn = null;
        return false;
    }
};

/**
 * Configure S3/R2 credentials for remote lake access.
 * @param {string} region - AWS region or 'auto' for R2
 * @param {string} endpoint - Custom S3 endpoint (e.g. Cloudflare R2 URL)
 * @param {string} accessKeyId - Access key (optional, empty for public buckets)
 * @param {string} secretAccessKey - Secret key (optional, empty for public buckets)
 * @returns {Promise<boolean>}
 */
globalThis.duckLakeConfigureS3 = async function (region, endpoint, accessKeyId, secretAccessKey) {
    if (!conn) return false;
    try {
        const statements = [];
        if (region) statements.push(`SET s3_region='${region}';`);
        if (endpoint) statements.push(`SET s3_endpoint='${endpoint}';`);
        if (accessKeyId) statements.push(`SET s3_access_key_id='${accessKeyId}';`);
        if (secretAccessKey) statements.push(`SET s3_secret_access_key='${secretAccessKey}';`);
        // For public R2 buckets, disable signing
        if (!accessKeyId) statements.push(`SET s3_url_style='path';`);

        for (const sql of statements) {
            await conn.query(sql);
        }
        console.log('[DuckLake] S3/R2 configured');
        return true;
    } catch (err) {
        console.error('[DuckLake] S3 config failed:', err);
        return false;
    }
};

/**
 * Execute a SQL query against DuckDB WASM and return results as JSON.
 * @param {string} sql - SQL query to execute
 * @returns {Promise<string>} JSON string of results: { columns: string[], rows: any[][] }
 */
globalThis.duckLakeQuery = async function (sql) {
    if (!conn) return JSON.stringify({ error: 'Not initialized', columns: [], rows: [] });
    try {
        const result = await conn.query(sql);
        const columns = result.schema.fields.map(f => f.name);
        const rows = result.toArray().map(row => {
            const obj = row.toJSON();
            return columns.map(c => obj[c]);
        });
        return JSON.stringify({ columns, rows });
    } catch (err) {
        console.error('[DuckLake] Query error:', err);
        return JSON.stringify({ error: err.message, columns: [], rows: [] });
    }
};

/**
 * Query a remote Parquet file directly. Convenience wrapper.
 * @param {string} parquetUrl - Full HTTP(S) URL to the .parquet file
 * @param {string} sqlWhere - Optional WHERE clause (without the "WHERE" keyword)
 * @param {number} limit - Max rows to return (default 1000)
 * @returns {Promise<string>} JSON results
 */
globalThis.duckLakeQueryParquet = async function (parquetUrl, sqlWhere, limit) {
    const whereClause = sqlWhere ? ` WHERE ${sqlWhere}` : '';
    const limitClause = limit > 0 ? ` LIMIT ${limit}` : ' LIMIT 1000';
    const sql = `SELECT * FROM read_parquet('${parquetUrl}')${whereClause} ORDER BY score DESC${limitClause}`;
    return await globalThis.duckLakeQuery(sql);
};

/**
 * Get the count of rows in a remote Parquet file.
 * @param {string} parquetUrl
 * @returns {Promise<number>}
 */
globalThis.duckLakeCountParquet = async function (parquetUrl) {
    const result = await globalThis.duckLakeQuery(`SELECT COUNT(*) as cnt FROM read_parquet('${parquetUrl}')`);
    const parsed = JSON.parse(result);
    if (parsed.rows && parsed.rows.length > 0) return parsed.rows[0][0];
    return 0;
};
