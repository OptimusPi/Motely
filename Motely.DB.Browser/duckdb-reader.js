// duckdb-wasm.js — DuckDB WASM bridge for Motely Browser interop
// Loaded by the C# [JSImport] interop layer.
// Runs browser-side JS using @duckdb/duckdb-wasm from CDN.

// Pin to a specific version — do NOT use @latest in production.
const DUCKDB_VERSION = '1.29.0';
const DUCKDB_CDN = `https://cdn.jsdelivr.net/npm/@duckdb/duckdb-wasm@${DUCKDB_VERSION}/dist`;

/** @type {any} */
let db = null;
/** @type {any} */
let conn = null;
/** @type {Promise<boolean> | null} */
let _initPromise = null; // Singleton init guard — prevents concurrent init races

/**
 * Escape a SQL string value (for use inside single-quoted literals only).
 * Do NOT use for SQL expressions like WHERE or ORDER BY clauses.
 * @param {string} value
 * @returns {string}
 */
function _escapeSql(value) {
    return String(value).replace(/'/g, "''");
}

async function _doInit() {
    try {
        // Import the async EH (exception-handling) module — NOT the blocking variant
        const duckdb = await import(`${DUCKDB_CDN}/duckdb-browser-eh.js`);

        const BUNDLES = {
            mvp: {
                mainModule: `${DUCKDB_CDN}/duckdb-mvp.wasm`,
                mainWorker: `${DUCKDB_CDN}/duckdb-browser-mvp.worker.js`,
            },
            eh: {
                mainModule: `${DUCKDB_CDN}/duckdb-eh.wasm`,
                mainWorker: `${DUCKDB_CDN}/duckdb-browser-eh.worker.js`,
            },
        };

        const bundle = await duckdb.selectBundle(BUNDLES);

        // Use Blob Worker URL to satisfy CORS restrictions on cross-origin worker scripts
        const workerUrl = URL.createObjectURL(
            new Blob([`importScripts("${bundle.mainWorker}");`], { type: 'text/javascript' })
        );
        const worker = new Worker(workerUrl);
        const logger = new duckdb.ConsoleLogger();
        db = new duckdb.AsyncDuckDB(logger, worker);

        // Second arg is pthreadWorker (for pthread support), NOT mainWorker again
        await db.instantiate(bundle.mainModule, bundle.pthreadWorker);
        URL.revokeObjectURL(workerUrl);

        conn = await db.connect();

        // httpfs autoloads in DuckDB WASM when querying https:// URLs — no explicit LOAD needed
        // Ref: https://duckdb.org/docs/api/wasm/extensions

        console.log('[DuckDB] WASM initialized');
        return true;
    } catch (err) {
        console.error('[DuckDB] Init failed:', err);
        db = null;
        conn = null;
        _initPromise = null; // allow retry on next call
        return false;
    }
}

async function _ensureInit() {
    if (conn !== null) return true;
    if (_initPromise === null) _initPromise = _doInit();
    return _initPromise;
}

/**
 * Initialize DuckDB WASM. Idempotent — safe to call multiple times.
 * @returns {Promise<boolean>}
 */
globalThis.duckDbInit = async function () {
    return _ensureInit();
};

/**
 * Configure S3/R2 credentials.
 * @param {string} region
 * @param {string} endpoint
 * @param {string} accessKeyId
 * @param {string} secretAccessKey
 * @returns {Promise<boolean>}
 */
globalThis.duckDbConfigureS3 = async function (region, endpoint, accessKeyId, secretAccessKey) {
    if (!(await _ensureInit())) return false;
    try {
        if (region)          await conn.query(`SET s3_region='${_escapeSql(region)}';`);
        if (endpoint)        await conn.query(`SET s3_endpoint='${_escapeSql(endpoint)}';`);
        if (accessKeyId)     await conn.query(`SET s3_access_key_id='${_escapeSql(accessKeyId)}';`);
        if (secretAccessKey) await conn.query(`SET s3_secret_access_key='${_escapeSql(secretAccessKey)}';`);
        if (!accessKeyId)    await conn.query(`SET s3_url_style='path';`);
        return true;
    } catch (err) {
        console.error('[DuckDB] S3 config failed:', err);
        return false;
    }
};

/**
 * Execute SQL and return results as JSON: { columns: string[], rows: any[][] }
 * @param {string} sql
 * @returns {Promise<string>}
 */
globalThis.duckDbQuery = async function (sql) {
    if (!(await _ensureInit())) return JSON.stringify({ error: 'Not initialized', columns: [], rows: [] });
    try {
        const result = await conn.query(sql);
        const columns = result.schema.fields.map(f => f.name);
        const rows = result.toArray().map(row => {
            const obj = row.toJSON();
            return columns.map(c => obj[c]);
        });
        return JSON.stringify({ columns, rows });
    } catch (err) {
        console.error('[DuckDB] Query error:', err);
        return JSON.stringify({ error: err.message, columns: [], rows: [] });
    }
};

/**
 * Query a remote Parquet file with optional WHERE filter, ORDER BY, and row limit.
 * @param {string} parquetUrl - Full HTTP(S) URL to the .parquet file
 * @param {string} sqlWhere - Optional WHERE expression (no "WHERE" keyword)
 * @param {string} orderBy - Optional ORDER BY expression (no "ORDER BY" keyword), defaults to 'score DESC'
 * @param {number} limit - Max rows (default 1000)
 * @returns {Promise<string>}
 */
globalThis.duckDbQueryParquet = async function (parquetUrl, sqlWhere, orderBy, limit) {
    const safeUrl = _escapeSql(parquetUrl); // URL is a value → escape it
    const whereClause = sqlWhere ? ` WHERE ${sqlWhere}` : '';
    const orderClause = orderBy ? ` ORDER BY ${orderBy}` : ' ORDER BY score DESC';
    const safeLimit = (limit > 0) ? Math.floor(limit) : 1000;
    const sql = `SELECT * FROM read_parquet('${safeUrl}')${whereClause}${orderClause} LIMIT ${safeLimit}`;
    return globalThis.duckDbQuery(sql);
};

/**
 * Count rows in a remote Parquet file.
 * @param {string} parquetUrl
 * @returns {Promise<number>}
 */
globalThis.duckDbCountParquet = async function (parquetUrl) {
    const safeUrl = _escapeSql(parquetUrl);
    const result = await globalThis.duckDbQuery(`SELECT COUNT(*) AS cnt FROM read_parquet('${safeUrl}')`);
    const parsed = JSON.parse(result);
    if (parsed.rows && parsed.rows.length > 0) return parsed.rows[0][0];
    return 0;
};

/**
 * Release DuckDB WASM resources. Call on page unload.
 */
globalThis.duckDbCleanup = async function () {
    try {
        if (conn) { await conn.close(); conn = null; }
        if (db)   { await db.terminate(); db = null; }
        _initPromise = null;
    } catch { /* ignore cleanup errors */ }
};
