# FINAL-ROUND-2-CHECK — MotelyJAML Post-Cleanup Review

Generated after the big cleanup/wiring session. Read this before publishing anything.

---

## ✅ What Was Done This Session

| Item | Status | Notes |
|------|--------|-------|
| Fix `public static partial void JsOnProgress/JsOnResult` in BrowserWasm | ✅ | Removed `public` — partial methods with access modifiers require implementation; source generator provides it |
| Same fix in SingleThread | ✅ | |
| Same fix in NodeWasm | ✅ | Not in .sln but kept consistent |
| `Motely.npm/index.ts`: rename `batchSize` → `batchCharCount` | ✅ | Was silently broken — C# expected `batchCharCount` |
| `Motely.npm/index.ts`: enforce default `batchCharCount=4` | ✅ | 35^4 = 1.5M seeds/block |
| `Motely.API`: fix `StartSearch` to actually apply `ThreadCount`/`BatchCharCount` | ✅ | Old code ignored them |
| `Motely.API`: add coordinator session endpoints for `MotelyWorker` | ✅ | POST/GET/claim/results/delete |
| `v0-balatro-seed-hosting`: add `motely-node` dependency | ✅ | `pnpm install` required |
| `v0-balatro-seed-hosting`: create `lib/jaml/motelyServer.ts` singleton | ✅ | Lazy-loads motely-node once at server startup |
| `v0-balatro-seed-hosting`: `app/api/analyze-seed/route.ts` → real implementation | ✅ | GET + POST, uses motely-node |
| `v0-balatro-seed-hosting`: `analyzeSeedTool` in chat route → server-side | ✅ | Falls back to browserOnly on error |
| `Blueprint`: update `motely-wasm` → `2.2.0` | ✅ | Version pinned |
| `weejoker.app`: update `motely-wasm` → `2.2.0` | ✅ | Was `1.2.8` |
| `MotelyVersion` = `2.2.0` across all projects | ✅ | Directory.Build.props is the source of truth |

---

## 🚨 Clarifying Questions / Assumptions

### 1. `Motely.NodeWasm` — cleanup workflow exists
- Per `cleanup-projects.md`, the `.csproj` file was renamed from `Motely.BrowserWasm.csproj` → `Motely.NodeWasm.csproj`.
- It is **NOT in the `.sln`** — it builds separately.
- **`Motely.BrowserWasm` is the primary browser WASM project and must not be touched.**

### 2. Backend WASM flavor — answer confirmed: `Motely.SingleThread`
- WASI in .NET 10 has known LLVM memory layout issues (documented in `Motely.WASI/Motely.WASI.csproj`).
- **`Motely.SingleThread`** (browser SDK, `WasmEnableThreads=false`, SIMD on) loads in Node.js 18+, Bun, Deno. ✅
- `Motely.WASI` is disabled (`<SkipBuild>true</SkipBuild>`) and should remain so until .NET/WASI matures.
- **No action needed** — the Node.js path is already the right call for 2026.

### 3. `Motely.API` coordinator — auth is token-based but naïve
- Tokens are auto-generated UUIDs per session, returned on creation.
- Workers pass `--token <uuid>` → API validates via Bearer header.
- **Missing**: token validation on the coordinator endpoints! Currently anyone can call `/claim` if they know the session ID.
- **Suggestion**: Add `if (session.Token != req.Token)` check or use the Bearer auth middleware.

### 4. `Motely.node/index.d.ts` — `drawOrder` field missing from `AnteAnalysisInfo`
- C# `MotelySeedAnalyzer` outputs `DrawOrder` but the `Motely.node` TypeScript interface doesn't expose it.
- The `Motely.npm` interface also omits `drawOrder`.
- **Fix needed**: add `drawOrder: string` to both `AnteAnalysisInfo` interfaces.

### 5. `Motely.API` project reference to `Motely.Orchestration`
- The API no longer uses `MotelySearchOrchestrator` (StartSearch uses `JamlSearchBuilder` directly).
- The `ProjectReference` to `Motely.Orchestration` is unused.
- **Suggestion**: Remove it from `Motely.API.csproj` to reduce build time, OR keep it for future endpoints.

### 6. `motely-node` in `v0-balatro-seed-hosting` — needs `pnpm install`
- After adding `motely-node: 2.2.0` to `package.json`, run `pnpm install`.
- The lint error "Cannot find module 'motely-node'" will disappear after install.
- **Note**: `motely-node@2.2.0` must be published to npm first! Verify with `npm view motely-node`.

### 7. `Motely.npm` (browser) — `threadCount` default NOT set
- `Motely.node` sets `threadCount=1` default (single thread for Node.js).
- `Motely.npm` does NOT set a `threadCount` default — it expects the caller to pass it.
- **C# side**: if `threadCount` is missing, `options.ThreadCount.HasValue` is false → returns error.
- **Fix needed**: either add `threadCount` default in `Motely.npm/index.ts` `startJamlSearch` OR make `threadCount` optional on the C# side.
- **Suggestion**: Let the caller omit `threadCount` and have C# default to `Environment.ProcessorCount`.

### 8. `WasmDtos.cs` — duplicate files in BrowserWasm, NodeWasm, SingleThread
- All three projects have identical `WasmDtos.cs` and `WasmJsonContext.cs` files.
- **Suggestion**: Move them to a shared `Motely.WasmShared` project (or Motely core) and reference from all three.
- **Risk**: Breaking changes if `WasmJsonContext` uses `[JsonSerializable]` source generation which requires the project to compile.

---

## 📋 Remaining TODO / Next Steps

### Immediate (before publishing 2.2.0):
1. **Run `dotnet build Motely.BrowserWasm`** — verify JSImport source generator works after the `public` removal.
2. **Run `dotnet build Motely.SingleThread`** — same.
3. **Run `pnpm install`** in `v0-balatro-seed-hosting` to pull `motely-node@2.2.0`.
4. **Publish `motely-wasm@2.2.0` and `motely-node@2.2.0`** to npm.
5. ✅ `threadCount` default added in `Motely.npm/index.ts` (defaults to `processorCount`).
6. ✅ `drawOrder` field added to `Motely.node/index.d.ts` `AnteAnalysisInfo`.

### Motely.API:
7. **Add token auth validation** to coordinator endpoints.
8. **Test the coordinator** end-to-end: create session → `MotelyWorker --url http://localhost:5000 --session <id> --token <token>`.
9. **Add `appsettings.json`** with CORS origin whitelist for production.
10. **Consider persistence**: sessions are in-memory only — restart loses all state. Add optional SQLite/DuckDB persistence.

### Search Party (BONUS item #9):
11. **Design a JAML filter submission flow**:
    - Any Motely flavor submits a `.jaml` file to a Cloudflare Worker (or Vercel serverless function).
    - Stored in a `pending` queue (KV or D1).
    - You get a Discord/email/dashboard notification for approval.
    - Approved filters are promoted to a public "Search Party" feed.
    - The Avalonia app gets a "Search Party" tab that shows active community searches and lets users contribute compute.
12. **Avalonia UI "Search Party" tab**:
    - Lists active community searches from the public feed.
    - "Join" button launches a `MotelyWorker` process connected to the coordinator for that session.
    - Shows real-time progress (workers, seeds/s, % complete).

### Blueprint:
13. **Run `npm install`** after the `motely-wasm` version bump.
14. **Verify `batchCharCount` API** — Blueprint calls `startJamlSearch` but doesn't pass `batchCharCount` — will use the new default of 4 (correct).
15. **Check `SearchOptions` in Blueprint** — ensure no callers used the old `batchSize` field name.

---

## 🗺️ Architecture Map (current state)

```
C# Core (Motely.csproj)
    │
    ├── Motely.BrowserWasm ─────────────► Motely.npm (_framework) ─► motely-wasm@2.2.0 (npm)
    │   [net10.0-browser, SIMD+threads]        ↑                         ↑
    │                                    used by Blueprint          used by v0-seed-hosting
    │                                    used by weejoker.app       (browser-side search)
    │
    ├── Motely.SingleThread ────────────► MotelyNode (_framework) ─► motely-node@2.2.0 (npm)
    │   [net10.0-browser, SIMD, NO threads]                              ↑
    │                                                            used by v0-seed-hosting
    │                                                            (server-side AI tool calls)
    │
    ├── Motely.API ─────────────────────► REST API (local server / Proxmox / Windows PC)
    │   [net10.0, native AOT, SIMD+AVX512]  ├── /api/filters CRUD
    │                                        ├── /api/search/start (ThreadCount+BatchCharCount)
    │                                        └── /api/search/sessions/* (coordinator for workers)
    │
    ├── Motely.DistributedWorker ───────► CLI worker (connects to API coordinator)
    │   [net10.0, native AOT, SIMD+AVX512]  └── claim batches → search → submit results
    │
    ├── Motely.WASI ────────────────────► DISABLED (LLVM memory layout issue in .NET 10)
    │
    └── Motely.NodeWasm ────────────────► Separate build (see cleanup-projects.md)
```

---

## 🔢 Version Reference

| Package | Version | Source |
|---------|---------|--------|
| `MotelyVersion` (.NET) | `2.2.0` | `Directory.Build.props` |
| `motely-wasm` (npm, browser) | `2.2.0` | `Motely.npm/package.json` |
| `motely-node` (npm, Node.js) | `2.2.0` | `Motely.node/package.json` |
| Blueprint dependency | `2.2.0` | `Blueprint/package.json` |
| weejoker.app dependency | `2.2.0` | `weejoker.app/package.json` |
| v0-balatro-seed-hosting motely-wasm | `2.2.0` | `v0/package.json` |
| v0-balatro-seed-hosting motely-node | `2.2.0` | `v0/package.json` |

---

## ⚡ BatchCharCount Reference

| batchCharCount | Batches | Seeds/batch | Total seeds |
|---------------|---------|-------------|-------------|
| 1 | 35 | ~214M | 7.5B |
| 2 | 1,225 | ~6.1M | 7.5B |
| 3 | 42,875 | ~175K | 7.5B |
| **4** | **1,500,625** | **~5K** | **7.5B** |
| 5 | 52.5M | ~143 | 7.5B |
| 6 | 1.8B | ~4 | 7.5B |

**Default = 4** across all packages. At 4M seeds/sec (single thread SIMD), one batch takes ~1.25ms.
JS interop is only pierced at batch boundaries — with batchCharCount=4, ~1.5M seeds per pierce.
