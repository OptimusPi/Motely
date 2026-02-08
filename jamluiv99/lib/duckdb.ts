/**
 * Optional DuckDB access for Node.js (e.g. self-hosted API).
 * Not available on Vercel serverless (native addon unsupported).
 * Use try/catch or isDuckDBAvailable() before calling query/getDatabase.
 */
function loadDuckDb(): typeof import("duckdb") | null {
  try {
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    return require("duckdb") as typeof import("duckdb")
  } catch {
    return null
  }
}

const _duckdb = loadDuckDb()

export function isDuckDBAvailable(): boolean {
  return _duckdb != null
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
let db: any = null

export function getDatabase(): { all: (...args: unknown[]) => void; get: (...args: unknown[]) => void } {
  if (!_duckdb) throw new Error("DuckDB is not available (e.g. Vercel serverless). Use self-hosted or WASM client.")
  if (!db) {
    const dbPath = process.env.DUCKDB_PATH || ":memory:"
    db = new _duckdb.Database(dbPath)
  }
  return db
}

export function query<T = Record<string, unknown>>(
  sql: string,
  params: unknown[] = []
): Promise<T[]> {
  return new Promise((resolve, reject) => {
    try {
      const database = getDatabase()
      database.all(sql, ...params, (err: Error | null, rows: T[]) => {
        if (err) reject(err)
        else resolve(rows ?? [])
      })
    } catch (e) {
      reject(e)
    }
  })
}

export function querySingle<T = Record<string, unknown>>(
  sql: string,
  params: unknown[] = []
): Promise<T | null> {
  return new Promise((resolve, reject) => {
    try {
      const database = getDatabase()
      database.get(sql, ...params, (err: Error | null, row: T | undefined) => {
        if (err) reject(err)
        else resolve((row as T) ?? null)
      })
    } catch (e) {
      reject(e)
    }
  })
}
