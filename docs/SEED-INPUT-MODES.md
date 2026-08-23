# Seed input modes — one design, every entry point

Every Motely search answers two questions: **which seeds** (this doc) and **which filter** (JAML).
The "which seeds" side is a single engine concept, `MotelySearchIntent`, plus two DuckDB-backed
pieces in `Motely.DataLake`. Every entry point should be a thin adapter over those three things.
This page is the feature inventory: what exists, what each surface exposes, and the gaps.

## The design (engine side)

| Piece | File | Role |
|---|---|---|
| `MotelySearchIntent` / `MotelySearchInputMode` | `Motely/MotelySearchIntent.cs` | Serializable "which seeds" request: `Sequential`, `SeedList`, `Keyword`, `Random`, `Aesthetic`, `Provider`. `ApplyTo(settings)` maps it onto the engine. Crosses the WASM boundary as JSON. |
| `SeedSourceProvider` | `Motely.DataLake/SeedSourceProvider.cs` | Streams seeds out of *anything DuckDB can open*: CSV/TXT/Parquet/JSON, a JAML file's `seeds:` block, a `.duckdb`/`.db`/`.sqlite`, a directory, `http(s)://` or `s3://`. Database sources are copied into memory and DETACHed up front so the lake stays writable during the run. `FromLakeRoot(root)` = every seed ever saved — the lake catalog plus every legacy `.duckdb`/CSV/TXT still in the root — all filters, deduped; `FromLakeFilter(root, id)` = one filter's. |
| `SeedLakeSink` / `SeedLake` | `Motely.DataLake/SeedLake.cs`, `SeedLakeSink.cs` | The seed lake: one DuckLake every filter and writer share. Catalog `ducklake.sqlite` beside the data root (repo root for the default `Seeds/`; `MOTELY_DATALAKE_CATALOG` overrides), SQLite in WAL mode because the CLI, helper-api and MotelyWorker write at once; data root = `--results-path` → `MOTELY_DATALAKE_PATH` → `Seeds/`. Tables `results(filter_id, seed, score, tallies, found_at)` and `filters(filter_id, tally_labels, updated_at)`. Finds are buffered and flushed within 250 ms / 512 rows / on dispose; DuckLake has no keys, so writers dedupe per filter and readers `SELECT DISTINCT`. If DuckLake cannot attach (offline first run) the sink falls back to the legacy `<root>/<filterId>.duckdb` with one stderr line. |
| `CliSearchMode.TryApplySearchMode` | `Motely.CLI/CliSearchMode.cs` | The reference adapter: validates mutual exclusion, builds the intent or provider, applies range math. Anything that wants "CLI parity" should call this or mirror it exactly. |

## The modes (feature list to advertise)

| Mode | What it means | CLI |
|---|---|---|
| Sequential sweep | Walk the whole 35^8 space in batches. The default. | (none) · `--startBatch/--endBatch/--startPercent` · `--startSeed/--stopSeed` · `--batchCharCount` |
| Seed list | Search exactly these seeds. | `--seeds A,B,C` |
| Source | Seeds from a file/URL/dir via DuckDB. | `--source <path\|url>` |
| Keyword | Seeds containing a word, free slots padded. | `--keyword W` · `--keywords W1,W2` · `--padding <chars>` |
| Random | N uniformly random seeds. | `--random N` (`--native-random N` in native mode) |
| Aesthetic | Pretty families: palindrome, psychosis, mirror, repeater, step, leet, gross, funny, balatro, nsfw. | `--aesthetic <name\|all>` |
| Collect | Stop after N matches; default prepass runs every aesthetic then falls back to sequential. | `--collect N` |
| **Drown** | Cannonball into the seed lake: re-search **every seed ever saved, every filter**, deduped. One filter's finds are another filter's candidates. | `--drown` |
| **Replay / verify** | Re-run only this JAML's own `seeds:` block. Nothing else. | `--replay` · `--verify-seeds` |
| Score gate | Auto-tuned or fixed cutoff for scored (`should:`) filters. | `--cutoff auto\|N` |
| Lake root | Where the lake lives. | `--results-path <dir>` |
| Save-back | Found seeds written into the JAML's top-level `seeds:` block after the run. | automatic in `--jaml` mode |

Rules that hold everywhere: exactly one input mode per run; `--startSeed/--stopSeed` only with
sequential; a JAML `seeds:` block is *output*, never an implicit input (that's what `--replay` is for).

## Entry points — who exposes what today

| Entry point | Surface | Modes exposed | Uses shared adapter? | Writes to lake? |
|---|---|---|---|---|
| `Motely.CLI --jaml` | flags | **all of the above** | yes (`CliSearchMode`) | yes, **only if the filter has `should:`**; also JAML save-back |
| `Motely.CLI --native` | flags | all except `--replay`; `--native-random` instead of `--random`; `--collect` ignored | yes | **no** (can drown *from* the lake, never writes back) |
| `Motely.TUI` | settings screen + search window | sequential (+ start/stop index), random, keyword+padding, file source, palindrome, psychosis only | **partly** — hand-rolled `switch`, own range math; uses `SeedSourceProvider`/`SeedLakeSink` | only when `DefaultSink` is set; default `""` → **nothing** |
| `Motely.HelperAPI` (`POST /api/search`) | HTTP | sequential batch range + `StopAfter` only | no | no — in-memory ring of ≤500 |
| `Motely.DistributedWorker` (party / pool / hosted) | worker protocol | sequential batch range from the server lease/claim | no | no — `--local-db` is parsed and printed, save step is an empty comment |
| `Motely.Wasm` (`motely.findSeeds(jaml, intent)`) | JS | sequential, seed list, keyword, random, aesthetic — the intent object itself; **no** source/drown/replay (no DuckDB in browser) | yes (`MotelySearchIntent` directly) | n/a |
| `vscode-jaml` LM tools (`motely_search_seeds`) | VS Code | shells out to CLI with `--collect N -q` only | via CLI | side effect of CLI |
| `Motely.Lsp` | LSP | none (no search) | – | – |
| `Motely.JsonRender` | CLI | `--seeds` overrides the block for *rendering*, not search | no | – |

## Gaps (the to-do list this inventory produces)

1. **TUI ≠ CLI.** Missing drown, replay, `--aesthetic all` (only 2 of 10 families), collect, batch/percent ranges. Fix: replace the hand-rolled `switch` in `SearchWindow.cs` with `CliSearchMode.TryApplySearchMode` (or move that adapter into `Motely.DataLake`/engine so TUI can call it without referencing the CLI project). Default the sink to the lake root so stock TUI runs populate `Seeds/`.
2. **HelperAPI can only sequential.** `StartSearchRequest` should carry a `MotelySearchIntent` (WASM already proves the JSON shape works) plus `source`/`drown`/`replay` flags, and attach a `SeedLakeSink`.
3. ~~**Workers never save locally.**~~ Done: `--local-db` / `PoolWorkerOptions.LocalDbPath` now open a `SeedLakeSink` per claim/lease (pool and party mode, and helper-api's in-process worker) and every find streams into the shared lake under the pool filter id / the party JAML's id.
4. **Lake writes gated on `should:`.** A must-only filter finds seeds, prints them, saves them into the JAML — and never reaches the lake, so `--drown` can't see them. `SeedLakeSink.OnSeed` already exists; attach it on the unscored path too.
5. **`--native` never writes back.** Same sink, same fix.
6. **Dead fields.** `CliSearchMode.Input.FilterId` / `.JamlSeeds` are populated and never read (leftover from implicit `seeds:` replay). `DuckDbResultsSeedProvider` has no call sites.
7. **WASM can't source/drown/replay** — expected (no filesystem/DuckDB), but the JS host could hand `runFindSeeds` a `SeedList` intent built from a lake export, which is the browser-side equivalent of `--replay`.

## What "done" looks like

Every row in the entry-point table reads: *modes = all that make sense for the surface; adapter = shared; lake = yes.*
Then the feature list above is the marketing copy, verbatim, for every front door.
