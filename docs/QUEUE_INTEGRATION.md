# MotelyJAML — Vercel Queue Integration

## How it works

JAMMY (the Next.js app) owns two Vercel Queue topics:

| Topic | Direction | Message |
|-------|-----------|---------|
| `search-blocks` | JAMMY → helpers | `{ filterId, blockIndex }` — one block of 35^5 seeds to search |
| `search-results` | helpers → JAMMY | `{ filterId, seeds: [{seed, score}] }` — seeds found in a block |

**Helpers are both consumers AND producers.** They consume `search-blocks` and produce `search-results`. Every helper hears every other helper's results via the fan-out consumer.

---

## Helper Protocol

### 1. How does a helper say "I am ready"?

It doesn't need to. Queue consumers are always ready. When a `search-block` message arrives, the consumer wakes up, processes it, and goes back to sleep. There is no "register" step.

- **Vercel function**: The `search-block` consumer route wakes automatically when a message is delivered.
- **Motely.DistributedWorker**: Polls the queue (poll mode) or is triggered via webhook (push mode). Just run it — it's always listening.

### 2. How does a helper post back results?

When a helper finishes a block:

```
POST /api/queue/submit-results    (via Vercel Queue topic: search-results)
Body: {
  "filterId": "perkeo2",           // UUID from the search session
  "blockIndex": 7,                 // which block was searched
  "seedsFound": 8,                 // count (informational)
  "seeds": [
    { "seed": "PERKEO2X", "score": 3 },
    { "seed": "XPERKEO2", "score": 2 }
  ]
}
```

In practice, helpers send this by publishing to the `search-results` topic via `@vercel/queue`:

```typescript
await send('search-results', { filterId, seeds: results })
```

JAMMY's `search-result` consumer stores the seeds in Neon. All other helpers subscribed to `search-results` also receive and store them — **everyone gets everyone's seeds for free**.

### 3. Can a helper REQUEST help for a filter?

Yes. Any Jammy Seed Finder client (desktop app, Motely.API, or browser) can call:

```
POST https://seedfinder.app/api/search/start
Body: { "filterId": "<uuid>", "jamlContent": "..." }
```

This starts the durable workflow which publishes 42,875 `search-block` messages. Any connected helper will pick them up.

---

## Motely.DistributedWorker Integration

The distributed worker (`Motely.DistributedWorker`) connects to JAMMY's queue endpoints.

**Configuration** (`appsettings.json` or environment vars):

```json
{
  "PoolWorker": {
    "Url": "https://seedfinder.app",
    "WorkerId": "my-machine-name",
    "Threads": 8
  }
}
```

**What the worker does:**
1. Polls `/api/search/status` to find active sessions
2. For each session: calls `POST /api/search/start` if needed (with JAML content)
3. Subscribes to `search-blocks` topic (poll mode if self-hosted, push mode on Vercel)
4. Runs motely-node for each block
5. Posts results to `search-results` topic

**In Vercel environment**: Workers are the `search-block` consumer functions — Vercel handles scaling automatically.

**Self-hosted (Proxmox/Linux)**: See `PROXMOX_INSTALL.md` for setup.

---

## Re-scoring Block Results on JAMMY

When JAMMY receives a `search-results` message with `seeds: [...]`, it re-runs the JAML filter with `--seeds` mode to get accurate scores:

```
motely --jaml filter.jaml --seeds "PERKEO2X,XPERKEO2,..."
```

This is essentially free — scoring 1M seeds takes under a few milliseconds. Even if a helper submits 1 million seeds, the re-score is near-instant.

---

## Observability

**Vercel Dashboard** → Project → **Observability** tab → **Queues** tab:

| Metric | What it shows |
|--------|---------------|
| Messages/s | Publishing throughput (blocks being queued) |
| Queued | Total messages sent to topic |
| Received | Total delivered to consumers |
| Deleted | Successfully acknowledged (processed) |
| Max message age | Consumer lag — high = workers are behind |

Click any topic for per-consumer-group throughput charts.

**Vercel Workflow** dashboard is in the same Observability panel — shows run status, step completions, and sleep durations for the 30-day distributed search sessions.

---

## Why seeds are capped at 1000 (today)

Seeds are stored as JSONB in `jaml_filters.seeds`. The 1000 cap is conservative to keep Postgres rows small. Future: store unlimited seeds as `.seeds.parquet` in Vercel Blob, referenced from the DB row.

Scores ARE stored per seed: `[{ "seed": "PERKEO2X", "score": 3 }, ...]`. Score = number of `should` clauses matched.
