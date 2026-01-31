# DuckLake (Motely.DB)

## DuckDB = engine, the rest = storage

**DuckDB** is the query engine (like SQLite, but for analytics). It runs inside your process — you open a *connection* to a database (a file or `:memory:`). There is no separate “DuckDB server.” When docs say “run `INSTALL ducklake` in your DuckDB environment,” they mean: in any **session** that talks to DuckDB — e.g. DuckDB CLI, or in code `conn.CreateCommand(); cmd.CommandText = "INSTALL ducklake;"; cmd.ExecuteNonQuery();`. The extension is then available for that (and later) connections.

**Storage options:**

| Format | Speed | When to use |
|--------|--------|-------------|
| **CSV** | Slow for repeated queries; parsed every time, no indexes. | Exchange, one-off loads. Load into DuckDB then query. |
| **Single .duckdb file** | **Fastest** — native format, one file, minimal overhead. | One writer (or writer closes and others read). Best when only one process writes. |
| **DuckLake** (.ducklake + Parquet) | Fast, small extra cost vs single file. | Multiple processes (API + UI + readers) read/write same data without “database is locked.” |

So: **if only one thing writes at a time, stick with .duckdb** — it’s the fastest. Use **DuckLake** when you need API + UI + other readers all hitting the same seed source or result set without blocking each other. Use **CSV** for interchange or initial load, then `COPY`/import into a .duckdb or DuckLake table and use that for queries.

---

## Where will the data live?

| Data | Today | With DuckLake |
|------|--------|----------------|
| **Seeds (input)** | One path from config/CLI → single `.duckdb` file (e.g. `SeedSourcesDir` or user path). | Same path idea, but path = **catalog** (e.g. `seeds.ducklake`). Physically: that file + a **data directory** of Parquet (e.g. `seeds_data/` next to catalog or a configured `DATA_PATH`). |
| **Results (output)** | One `.duckdb` per search: `SearchResultsDir` + `{searchId}.db` (orchestrator: `searchResultsDir` + `{filterId}.db`). | Same dir, but catalog = e.g. `{searchId}.ducklake` + data dir; or one shared DuckLake catalog with one table per search. |

So: same **logical** place (configurable dirs, same path semantics). DuckLake = **catalog file** (`.ducklake`) + **Parquet directory**; the catalog stores metadata and points at the data path.

---

## DuckLake: no primary keys, upsert via MERGE INTO

**DuckLake does not support PRIMARY KEY.** Upserting is only supported via **`MERGE INTO`** (match on a key column, then UPDATE or INSERT).

**Our data:** Results are just **scores of seeds that fit a filter**. It’s reproducible (re-run the filter to get results again), and duplicates are acceptable. For upsert semantics (same seed, update score), **`MERGE INTO` is the right pattern** when writing to DuckLake.

Today `.duckdb` results use `seed VARCHAR PRIMARY KEY` and `INSERT OR REPLACE`. For DuckLake we’d create the results table *without* PRIMARY KEY and use `MERGE INTO results ... USING (source) ON seed = source.seed WHEN MATCHED THEN UPDATE SET ... WHEN NOT MATCHED THEN INSERT ...` (or equivalent) instead of INSERT OR REPLACE / appender.

---

## What code will change?

### Already done (read seeds from DuckLake)

- **`DuckDBSeedProvider`** — If `DbPath` is a `.ducklake` path, it attaches and reads `seed_source.main.seeds`. No further change for the read path.
- **Callers** — `JsonSearchExecutor`, `SeedSourceProvider`, CLI: they pass `DbPath`; if that path is a `.ducklake`, reading seeds already works.

### Not implemented

1. **Creating / migrating to DuckLake**
   - **New code:** Something that creates a DuckLake and populates it. Options:
     - **CLI or script:** Run the 3-line SQL (ATTACH DuckLake, ATTACH DuckDB, `COPY FROM DATABASE ... TO ...`) — e.g. DuckDB CLI or a small Motely CLI command.
     - **Motely.DB helper:** e.g. `DuckLakeCreator.CreateFromDuckDB(duckDbPath, duckLakeCatalogPath, dataPath)` that opens both and runs the COPY.
   - **Optional:** `JsonSearchExecutor` / `SeedSourceProvider`: today CSV/TXT → convert to `.duckdb` and return path. Could add “convert to DuckLake” (create catalog + data dir, COPY from the temp .duckdb or insert from CSV). Not required for “user already has a .ducklake.”

2. **Writing results to DuckLake**
   - **`MotelySearchDatabase`** — Today: takes `dbPath`, uses `CreateConnection(dbPath)` (single .duckdb), table has `seed VARCHAR PRIMARY KEY`, INSERT/Appender or `INSERT OR REPLACE`. Change: when path is `.ducklake`, use in-memory conn + `CreateConnectionWithDuckLake`, CREATE TABLE *without* PRIMARY KEY, and **upsert via `MERGE INTO`** (see above). Schema compatibility checks must work against that catalog.
   - **`MotelySearchOrchestrator`** — Today: `dbPath = Path.Combine(searchResultsDir, $"{filterId}.db")`, then `OrchestrateDatabase(dbPath, ...)`. For DuckLake: same dir but e.g. `{filterId}.ducklake` (and a data dir), or one shared DuckLake path + table name; pass that into `MotelySearchDatabase` (or a factory that branches on extension).
   - **`Motely.API` `SearchManager`** — Today: `dbPath = Path.Combine(MotelyPaths.SearchResultsDir, $"{searchId}.db")` everywhere (create DB, get results, resume cursor, column names, export, delete). Change: use same path pattern but `.ducklake` + data dir; every place that does `CreateConnection(dbPath)` or opens `MotelySearchDatabase(dbPath, ...)` would go through a path that supports DuckLake (e.g. `MotelySearchDatabase` handles both so callers keep passing a single path).

So: **minimal change** = only creation/migration (CLI or helper). **Larger change** = results in DuckLake (extend `MotelySearchDatabase` + orchestrator + `SearchManager` to open/write DuckLake when path is `.ducklake`).

---

## Status quo

- **Code:** `DuckDBSeedProvider` accepts a path; if `DuckLakeHelper.IsDuckLake(path)` it attaches via `CreateConnectionWithDuckLake(catalogPath, null, "seed_source")` and runs `SELECT seed FROM seed_source.main.seeds`. Otherwise it opens a normal `.duckdb` file and reads `seeds`.
- **Not verified:** No `.ducklake` file in repo. No test that attach + read works. Schema `seed_source.main.seeds` is assumed (DuckLake default schema may differ).
- **Not implemented:** Creating a DuckLake from CSV/TXT or from an existing `.duckdb` seed source. Writing results to DuckLake. Remote (R2/S3) attach untested.
- **Who uses it:** Only `DuckDBSeedProvider`; callers (e.g. `JsonSearchExecutor`) pass `source.DbPath` — if that path is `.ducklake`, DuckLake is used. No other code creates or attaches DuckLake.

## What’s actually in the codebase

| Piece | Role |
|-------|------|
| `DuckDBConnectionFactory.CreateConnectionWithDuckLake(catalogPath, dataPath?, schemaName)` | In-memory conn + `ATTACH 'ducklake:...' AS name (DATA_PATH '...')`. Existing catalog: pass `dataPath: null`. |
| `DuckLakeHelper.IsDuckLake(path)` | True if path ends in `.ducklake` or `path + ".ducklake"` exists. |
| `DuckLakeHelper.GetDuckLakeCatalogPath(path)` | Ensures path ends with `.ducklake`. |
| `DuckLakeHelper.GetDuckLakeDataPath(path)` | Dir for Parquet (e.g. `foo_data/`). Only relevant when **creating** a DuckLake, not when attaching. |
| `DuckLakeHelper.AttachDuckLake(conn, catalogPath, dataPath?, schemaName)` | Runs ATTACH on an open connection. |

Extension: DuckDB autoloads `ducklake` on first ATTACH; no `INSTALL` in code.

## Clarifying questions

1. **Schema:** Do we know that an attached DuckLake exposes tables as `<schemaName>.main.<table>`? If not, we need one real `.ducklake` and a quick test (attach + `SELECT * FROM ... LIMIT 1`).
2. **Creation:** Who creates DuckLakes today — hand with DuckDB CLI, or do we want a Motely helper (e.g. “convert this CSV / this .duckdb to DuckLake”)?
3. **Results:** Are search results staying in single-file `.duckdb` per run, or do we want results in DuckLake too (so API + UI can read/write same result set)?
4. **Remote:** Is R2/S3 attach in scope soon? If yes, we should test with one URL and document httpfs/secrets; if no, drop remote from this doc until we need it.
5. **Cloudflare / ErraticSeeds.app:** You **can** store a few GB of scored seeds in a good format on Cloudflare. **Workers don’t run DuckDB**, so you can’t run DuckLake inside a Worker. **R2** is the right place for data. Best option for the **browser** app: upload **Parquet** to R2 (one or more files, optionally partitioned by score). ErraticSeeds.app (Pages) already uses DuckDB-WASM — it can `read_parquet('https://...')` on R2 public/signed URLs and query in the client. For a few GB, partition (e.g. by score bucket) so the app loads only what it needs. DuckLake with R2 as DATA_PATH is fine for **server-side** (e.g. Motely.API or CLI) that runs DuckDB; the catalog lives elsewhere (DuckDB/SQLite/Postgres). See `docs/R2_INTEGRATION_GUIDE.md`, `docs/ERRATICDECK_APP_SPEC.md`.
6. **Access control:** DuckLake doesn’t implement roles itself; you enforce them via the **catalog** (e.g. PostgreSQL `GRANT` for superuser/writer/reader) and **storage** (e.g. S3 bucket policies per schema/table path). For local catalog + local `DATA_PATH`, not needed. See [DuckLake – Access Control](https://ducklake.select/docs/stable/duckdb/guides/access_control).

## If you just want to try it

1. Create a DuckLake with DuckDB CLI (1.4+): `INSTALL ducklake; LOAD ducklake;` then `ATTACH 'ducklake:path/to/catalog.ducklake' AS dl (DATA_PATH 'path/to/data_dir/'); USE dl; CREATE TABLE main.seeds (seed VARCHAR PRIMARY KEY);` and load seeds.
2. Point Motely at the catalog path (e.g. `path/to/catalog.ducklake`) where you’d pass a `.duckdb` path. Same CLI/API args, different extension.
3. If attach or `seed_source.main.seeds` fails, the schema or path is wrong — fix in `DuckDBSeedProvider` / helper and document the real shape here.

## Migration: DuckDB → DuckLake

Official guide: [DuckDB to DuckLake](https://ducklake.select/docs/stable/duckdb/migrations/duckdb_to_ducklake).

**When everything is supported** (our schemas use VARCHAR, INTEGER, no VARINT/ENUM/BIT/generated columns):

```sql
ATTACH 'ducklake:my_ducklake.ducklake' AS my_ducklake;
ATTACH 'db.duckdb' AS my_duckdb;

COPY FROM DATABASE my_duckdb TO my_ducklake;
```

That copies all tables from the attached `.duckdb` into the DuckLake. Catalog type (DuckDB, SQLite, PostgreSQL) doesn’t change this recipe. If you hit unsupported types (VARINT, ENUM, non-literal defaults, generated columns), the doc has a Python migration script and type-mapping notes.

## References

- [DuckLake](https://ducklake.select/) — integrated data lake and catalog format (Parquet + SQL catalog); multi-client with PostgreSQL/MySQL/SQLite, single-client with DuckDB catalog; snapshots, time travel, ACID. MIT.
- [DuckLake Manifesto](https://ducklake.select/manifesto/) — “SQL as a lakehouse format”: metadata in a SQL database (not JSON/Avro file mazes), data in Parquet on blob storage; simplicity, scalability, speed; one catalog transaction per change.
- [DuckDB – DuckLake extension](https://duckdb.org/docs/stable/core_extensions/ducklake)
- [DuckLake – Connecting](https://ducklake.select/docs/stable/duckdb/usage/connecting)
- [DuckLake – DuckDB to DuckLake migration](https://ducklake.select/docs/stable/duckdb/migrations/duckdb_to_ducklake)
- [DuckLake – Access Control](https://ducklake.select/docs/stable/duckdb/guides/access_control) (catalog + storage permissions; relevant for PostgreSQL/S3 multi-tenant)
- In-repo: `docs/DUCKLAKE_EXPLAINED.md`, `docs/R2_INTEGRATION_GUIDE.md` (if you add remote)
