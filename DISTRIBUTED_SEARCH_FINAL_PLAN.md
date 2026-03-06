# Distributed Seed Search — Final Architecture Plan

## The Problem

Balatro has 35^8 = **2,251,875,390,625** possible seeds. Exhaustive sequential search is
the ONLY search type that needs distribution. Palindrome search is trivially small
(a single thread handles it in minutes). The distributed search is a
**Bitcoin-mining-pool-style batch coordination system**.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│  v0-balatro-seed-hosting  (Vercel)  =  THE COORDINATOR          │
│                                                                 │
│  Neon PostgreSQL:                                               │
│    jaml_filters    — UUID id, name, author, jaml_content,       │
│                      embedding, votes, usage stats              │
│    search_sessions — persistent record of every distributed     │
│                      search (linked to jaml_filters.id)         │
│    seed_history    — copied/top-100 seeds                       │
│                                                                 │
│  Upstash Redis:                                                 │
│    session:{id}:cursor  — atomic INCRBY batch cursor            │
│    session:{id}:done    — completed batch counter               │
│    session:{id}:results — sorted set (seed → score)             │
│    session:{id}:meta    — live session metadata (hash)          │
│                                                                 │
│  Cloudflare R2:                                                 │
│    Completed search results exported as .parquet                │
│    Served via R2 public bucket or presigned URLs                │
│                                                                 │
│  API Routes (Next.js):                                          │
│    POST /api/search/sessions              create session        │
│    GET  /api/search/sessions              list sessions         │
│    GET  /api/search/sessions/[id]         status + top results  │
│    POST /api/search/sessions/[id]/claim   workers claim batches │
│    POST /api/search/sessions/[id]/results workers submit hits   │
│    GET  /api/search/sessions/[id]/export  download parquet (R2) │
└─────────────────────────────────────────────────────────────────┘
           ▲              ▲              ▲              ▲
           │              │              │              │
    ┌──────┘       ┌──────┘       ┌──────┘       ┌─────┘
    │              │              │              │
┌───┴────┐   ┌────┴───┐   ┌─────┴────┐   ┌─────┴────┐
│Worker 1│   │Worker 2│   │Worker N  │   │ Browser  │
│VPS     │   │Proxmox │   │Anywhere  │   │ WASM     │
│AVX-512 │   │VM      │   │AVX-512   │   │ (slow)   │
│Native  │   │Native  │   │Native    │   │ optional │
└────────┘   └────────┘   └──────────┘   └──────────┘
    Motely.DistributedWorker (AOT Linux native)
    --url https://seedfinder.app --session <id> --token <tok>
```

---

## Fixed Design Decisions

| Decision | Value | Rationale |
|----------|-------|-----------|
| **Block size** | 35^4 = 1,500,625 seeds/batch | Fixed. Not configurable. This is the Motely engine's batch unit. |
| **Total batches** | 35^(8-4) = 35^4 = 1,500,625 | Fixed. Covers the full 35^8 seed space. |
| **batchCharCount** | Always `4` | Hardcoded everywhere. No reason to change it. |
| **Search type** | Sequential exhaustive ONLY | The entire point of distributed search is covering ALL seeds. |
| **No palindrome** | Removed from distributed | Palindrome has ~44K seeds. A single thread finds them in seconds. |
| **Filter identity** | `filterId` (UUID from `jaml_filters`) | One standard way to identify a filter. Period. |
| **Coordinator** | v0-balatro-seed-hosting (Vercel) | Owns the database. Authoritative entry point. |
| **Workers** | Motely.DistributedWorker (C# AOT native) | Connects to coordinator, claims chunks, runs Motely engine. |
| **Chunk size** | Worker-chosen (default 100 batches) | Each claim = 100 × 1.5M = 150M seeds. Worker chooses based on speed. |

---

## What Exists Today (Already Built)

### v0-balatro-seed-hosting
- `lib/search/redis.ts` — Upstash Redis singleton
- `lib/search/coordination.ts` — createSession, claimBatches (atomic INCRBY), submitResults, getSessionStatus, validateToken
- `lib/search/types.ts` — SearchSession, ClaimResult, SeedResult, SubmitResultsBody, SessionStatus, CreateSessionBody, ClaimBody
- `app/api/search/sessions/route.ts` — POST create, GET list
- `app/api/search/sessions/[id]/route.ts` — GET status
- `app/api/search/sessions/[id]/claim/route.ts` — POST claim (Bearer auth)
- `app/api/search/sessions/[id]/results/route.ts` — POST submit (Bearer auth)
- `lib/rag.ts` — saveJamlFilter, getFilterById, searchSimilarFilters, getAllFilters
- `lib/db.ts` — Neon PostgreSQL client

### Motely.DistributedWorker
- `CoordinatorClient.cs` — HTTP client with Bearer auth, GetSession, Claim, SubmitResults
- `Program.cs` — claim→search→submit loop with progress, retry, Ctrl+C
- `WorkerDtos.cs` — AOT-safe JSON serialization

### Neon DB Tables
- `jaml_filters` — UUID id, name, author, description, jaml_content, embedding(768), thumbs_up/down, usage_count
- `seed_history` — seed_id, score, jaml_filter_id FK, user_session
- `chat_feedback` — feedback with embeddings for RAG
- `jaml_filter_votes` — per-user voting

---

## What Needs to Change

### 1. v0 Coordination Layer — Clean Up

**`lib/search/types.ts`** changes:
- Remove `batchCharCount` from `CreateSessionBody` — hardcode to 4
- Remove `palindrome` from `CreateSessionBody` and `SearchSession`
- Add `filterId: string` (UUID) to `CreateSessionBody` and `SearchSession`
- `batchCharCount` in `SearchSession` becomes a readonly constant, not a field

**`lib/search/coordination.ts`** changes:
- `createSession()`:
  - Require `filterId` — look up JAML content from `jaml_filters` table in Neon
  - Hardcode `batchCharCount = 4`, `totalBatches = 35^4 = 1_500_625`
  - Remove palindrome logic
  - **Persist session to Neon** `search_sessions` table (see migration below)
- `submitResults()`:
  - After accepting results, check if search is complete (done == totalBatches)
  - If complete: export results to R2 as parquet, update Neon search_sessions status

**`app/api/search/sessions/route.ts`** changes:
- POST body requires `filterId` (UUID), optional `name`
- Fetch JAML from Neon `jaml_filters` by filterId, reject if not found

### 2. Neon Migration — `search_sessions` Table

```sql
-- 006_search_sessions.sql
CREATE TABLE IF NOT EXISTS search_sessions (
  id            TEXT PRIMARY KEY,              -- same as Redis session ID
  filter_id     UUID NOT NULL REFERENCES jaml_filters(id),
  name          TEXT NOT NULL DEFAULT 'Distributed Search',
  deck          TEXT NOT NULL DEFAULT 'Red',
  stake         TEXT NOT NULL DEFAULT 'White',
  total_batches BIGINT NOT NULL DEFAULT 1500625, -- 35^4
  status        TEXT NOT NULL DEFAULT 'active'
                  CHECK (status IN ('active', 'completed', 'cancelled')),
  created_at    TIMESTAMPTZ DEFAULT NOW(),
  completed_at  TIMESTAMPTZ,
  -- Final stats (populated on completion)
  total_seeds_searched BIGINT DEFAULT 0,
  matching_seeds       INT DEFAULT 0,
  result_parquet_key   TEXT,  -- R2 object key for exported results
  token_hash           TEXT   -- bcrypt or SHA256 hash of bearer token (don't store raw)
);

CREATE INDEX idx_search_sessions_filter ON search_sessions(filter_id);
CREATE INDEX idx_search_sessions_status ON search_sessions(status) WHERE status = 'active';
```

### 3. Motely.API/Program.cs — Remove Distributed Coordinator

The entire `// ── Distributed Search Coordinator` section (lines 233-337) is **redundant**.
The coordinator lives in v0 with Upstash Redis. Motely.API should keep ONLY:

- **Filter CRUD** (local filesystem for standalone use, fine as-is)
- **Local search** endpoints (start/status/stop for single-machine use)
- Optionally: a `/health` or `/capabilities` endpoint

Remove: `CreateSessionRequest`, `SessionSummary`, `SearchSession` class,
all `/api/search/sessions/*` endpoints, `ClaimRequestBody`, `SubmitResultsBody` records.

### 4. Motely.DistributedWorker — Minor Cleanup

- Remove `session.Palindrome` check and `WithPalindromeSearch()` call
- `batchCharCount` is always 4 — can hardcode in settings, but reading from session is fine
  since the coordinator will always return 4

### 5. R2 Export (on search completion)

When `submitResults()` detects all batches are done:

1. Read all results from Redis sorted set `session:{id}:results`
2. Format as Parquet (using a lightweight library or CSV→Parquet pipeline)
3. Upload to R2 bucket: `results/{sessionId}.parquet`
4. Update Neon `search_sessions` with `status = 'completed'`, `result_parquet_key`, final stats
5. Clean up Redis keys (or let TTL expire)

Implementation options for Parquet on Vercel:
- **Option A**: Write CSV to R2, convert to Parquet via a Cloudflare Worker with DuckDB-WASM
- **Option B**: Write results as JSON to R2, let consumers convert
- **Option C**: Use `parquet-wasm` npm package directly in the Next.js API route

### 6. FilterId Flow

```
User picks/creates filter on seedfinder.app
  → jaml_filters row with UUID id
  → User clicks "Start Distributed Search"
  → POST /api/search/sessions { filterId: "abc-123", name: "My Search" }
  → Coordinator:
      1. Look up jaml_filters.id = "abc-123" → get jaml_content, deck, stake
      2. Create Redis session with that JAML
      3. Insert row in Neon search_sessions (filter_id = "abc-123")
      4. Return session ID + bearer token (shown ONCE)
  → User distributes session ID + token to workers
  → Workers: MotelyWorker --url https://seedfinder.app --session <id> --token <tok>
```

---

## Why NOT Rollup

The reference repo (maraf/dotnet-wasm-react) uses Rollup because it bundles
a **React application** that embeds .NET WASM. Their Rollup config handles:
- JSX transpilation (Babel)
- .wasm/.dat file copying
- React dependency deduplication
- Single-file ESM bundle output

Our npm packages (`motely-wasm`, `motely-node`) are **thin loader wrappers**, not apps:
- No JSX, no React, no Babel needed
- `_framework/` directory with .wasm files is shipped alongside (not bundled into JS)
- `dotnet.js` is loaded via dynamic `import()` at runtime
- The packages are consumed as dependencies by other apps (Blueprint, v0, etc.)

Rollup would add build complexity for zero functional benefit. The current approach
(raw ESM `index.ts` / `index.js` + `index.d.ts`) works correctly and consumers
import it fine. If we wanted to add Rollup later for polish (source maps, CJS compat),
we can, but it is NOT blocking.

---

## Execution Order

1. **Create Neon migration** `search_sessions` table
2. **Fix `lib/search/types.ts`** — remove batchCharCount/palindrome, add filterId
3. **Fix `lib/search/coordination.ts`** — hardcode batchCharCount=4, look up filter by filterId, persist to Neon
4. **Fix `app/api/search/sessions/route.ts`** — require filterId in POST
5. **Gut `Motely.API/Program.cs`** — remove distributed coordinator section
6. **Fix `Motely.DistributedWorker/Program.cs`** — remove palindrome handling
7. **Add R2 export** — on search completion, export results to R2
8. **Test end-to-end** — create session via v0, run worker against it

---

## Infrastructure Summary

| Service | Purpose | Cost |
|---------|---------|------|
| **Vercel** (v0) | Coordinator, web UI, AI chat, RAG | Free tier / Pro |
| **Neon** | PostgreSQL — filters, sessions, history, embeddings | Free tier (0.5GB) |
| **Upstash Redis** | Atomic batch coordination, live state | Free tier (10K commands/day) |
| **Cloudflare R2** | Parquet result storage | Free tier (10GB) |
| **VPS workers** | Motely.DistributedWorker (AVX-512 native) | $5-15/mo each |

All coordination flows through **one URL**: `https://seedfinder.app` (or whatever v0 deploys to).
Workers don't need to know about Redis, Neon, or R2. They just talk HTTP to the coordinator.
