import * as duckdb from '@duckdb/duckdb-wasm';
const state = {
    ready: false,
    tableName: 'motely_results',
    maxQueueSize: 500,
    queue: [],
    flushing: false,
    conn: null,
    db: null,
    insertStmt: null,
};
export async function initDuckDbWasmResults(options = {}) {
    if (state.ready && state.conn) {
        return createHandle(state.conn, state.tableName);
    }
    state.tableName = options.tableName || state.tableName;
    state.maxQueueSize = options.maxQueueSize || state.maxQueueSize;
    const bundles = duckdb.getJsDelivrBundles();
    const bundle = await duckdb.selectBundle(bundles);
    if (!bundle.mainWorker) {
        throw new Error('DuckDB-WASM bundle missing mainWorker.');
    }
    const worker = new Worker(bundle.mainWorker);
    const logger = new duckdb.ConsoleLogger();
    const db = new duckdb.AsyncDuckDB(logger, worker);
    await db.instantiate(bundle.mainModule, bundle.pthreadWorker);
    const conn = await db.connect();
    await conn.query(`CREATE TABLE IF NOT EXISTS ${state.tableName} (seed VARCHAR, score INTEGER, tallies VARCHAR)`);
    state.insertStmt = await conn.prepare(`INSERT INTO ${state.tableName} VALUES (?, ?, ?)`);
    state.db = db;
    state.conn = conn;
    state.ready = true;
    state.previousOnResult = globalThis.MotelyWasmOnResult;
    globalThis.MotelyWasmOnResult = (seed, score, talliesStr) => {
        if (state.previousOnResult) {
            try {
                state.previousOnResult(seed, score, talliesStr);
            }
            catch {
                // ignore user callback errors
            }
        }
        state.queue.push({ seed, score, tallies: talliesStr || '' });
        if (state.queue.length >= state.maxQueueSize) {
            void flushQueue();
        }
        else if (!state.flushing) {
            queueMicrotask(() => void flushQueue());
        }
    };
    return createHandle(conn, state.tableName);
}
async function flushQueue() {
    if (!state.conn || state.flushing || state.queue.length === 0) {
        return;
    }
    state.flushing = true;
    try {
        const stmt = state.insertStmt;
        if (!stmt) {
            throw new Error('DuckDB-WASM insert statement not initialized.');
        }
        while (state.queue.length > 0) {
            const row = state.queue.shift();
            await stmt.query(row.seed, row.score, row.tallies ?? '');
        }
    }
    finally {
        state.flushing = false;
    }
}
function createHandle(conn, tableName) {
    return {
        async getTopResults(limit = 1000) {
            const result = await conn.query(`SELECT seed, score, tallies FROM ${tableName} ORDER BY score DESC LIMIT ${limit}`);
            const rows = result.toArray();
            return rows;
        },
        async getCount() {
            const result = await conn.query(`SELECT COUNT(*) as count FROM ${tableName}`);
            const rows = result.toArray();
            return rows.length > 0 ? rows[0].count : 0;
        },
        async close() {
            await flushQueue();
            if (state.insertStmt) {
                await state.insertStmt.close();
            }
            if (state.conn) {
                await state.conn.close();
            }
            state.conn = null;
            state.db = null;
            state.insertStmt = null;
            state.ready = false;
        },
    };
}
