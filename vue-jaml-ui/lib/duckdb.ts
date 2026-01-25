import * as duckdb from "duckdb"

let db: duckdb.Database | null = null

export function getDatabase(): duckdb.Database {
  if (!db) {
    const dbPath = process.env.DUCKDB_PATH || ":memory:"
    db = new duckdb.Database(dbPath)
  }
  return db
}

export function query<T = Record<string, unknown>>(
  sql: string,
  params: unknown[] = []
): Promise<T[]> {
  return new Promise((resolve, reject) => {
    const database = getDatabase()
    database.all(sql, ...params, (err, rows) => {
      if (err) reject(err)
      else resolve(rows as T[])
    })
  })
}

export function querySingle<T = Record<string, unknown>>(
  sql: string,
  params: unknown[] = []
): Promise<T | null> {
  return new Promise((resolve, reject) => {
    const database = getDatabase()
    database.get(sql, ...params, (err, row) => {
      if (err) reject(err)
      else resolve((row as T) || null)
    })
  })
}
