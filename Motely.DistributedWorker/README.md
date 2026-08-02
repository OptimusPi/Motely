# Motely Distributed Worker

Native AOT executable that grinds seed blocks for a coordinator. Two modes,
two protocols:

```
MotelyWorker --party <partyId> [--server https://www.seedfinder.app]   # community Search Party
MotelyWorker --pool <helper-url>                                       # self-hosted Motely.HelperAPI
```

Options: `--threads N` (default: all cores), `--local-db <dir>` DuckLake root
(`-` disables), and pool-mode-only `--worker-id` / `--filter`.

## Party mode — the seedfinder.app protocol

The server side of this contract lives in the seedfinder.app repo
(`lib/party/coordinator.ts`, `app/api/party/*`); its mirrored copy of this
document is `lib/party/PROTOCOL.md`. Wire shapes here are `PartyDtos.cs` and
match the server's zod schemas exactly.

### Endpoints

| Call | Shape | Notes |
|---|---|---|
| `GET /api/party/next?partyId=` | → `{lease}` \| `{done, reason}` | Claims one lease. Reopens an expired lease first, else carves from the party's block cursor. |
| `POST /api/party/report` | `{partyId, workerToken, startBlock, seeds[], heartbeatOnly?}` → `{ok}` \| `{confirmed, rejected, recorded}` | `heartbeatOnly: true` extends the lease TTL. Otherwise completes the lease. |
| `GET /api/party/status?partyId=` | → `{state, blocksDone, blocksTotal, percentComplete, activeWorkers, finds}` | Read-only progress. |

### Lease lifecycle

- A lease covers blocks `[startBlock, startBlock + blockCount)` — **end
  exclusive**, like every range in this system.
- Server-side TTL is 60 seconds. This worker heartbeats every 20s while
  grinding; a dead worker's lease expires and is re-leased to someone else.
  Missed heartbeats cost nothing but redundant work.
- **Always report, even with zero seeds** — the report is what completes the
  lease. An unreported lease waits out its TTL before anyone can retry it.
- Party terminal states: `complete` (all blocks done), `exhausted` (block
  budget or deadline reached), `cancelled`.

### Trust model

Workers are untrusted hardware. The report carries **seed strings only**
(max 1000/report, each 1–8 chars) — the server re-runs every candidate
against the party's JAML itself and records at most 500 confirmed finds per
party. Local scores never cross the wire. This works because the engine is
deterministic: the same (JAML, block range, batchChars) always yields the
same matches, on any machine.

### Block semantics

A party block IS an engine batch index at the lease's `batchChars` — the
lease maps 1:1 onto `WithStartBatchIndex(startBlock)` /
`WithEndBatchIndex(startBlock + blockCount)` at
`WithBatchCharacterCount(batchChars)`.

> **Open question (needs one authoritative answer):** seeds-per-batch is
> documented as `35^batchChars` here (`ProcessBlockRunner`, and the party
> coordinator's "~1.5M seeds/block at batchChars 4") but as
> `35^(batchChars−1)` in seedfinder's hunt workflow, where the wasm build
> measured 35 seeds for one batch at `batchChars=2`. Both cannot be right.
> The engine should export its own constants (alphabet size, seeds-per-batch)
> so neither side hardcodes the math again.

### Known gap

There is no party-discovery endpoint — a worker must be handed its `partyId`
(from `create_search_party` output or the app). "Give me any active party"
exists only in pool mode against a self-hosted HelperAPI.

## Pool mode — self-hosted Motely.HelperAPI

The original mode: `POST {pool}/api/search/helper` with
`action=request` / `action=submit` envelopes (`WorkerDtos.cs`). This protocol
is served by `Motely.HelperAPI` (standalone or in-process from Motely.TUI) —
it is **not** served by seedfinder.app, which is why `--pool
https://www.seedfinder.app` never worked and party mode exists.
