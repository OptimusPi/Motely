# Motely.WASM

.NET WebAssembly build of Motely (seed analyzer + search). No UI — use from React, Next.js, or any host.

---

## Two ways to use this

| You want to… | Do this |
|--------------|--------|
| **Test locally** (no npm) | `.\run.ps1` → open http://localhost:3333 |
| **Use in an app** (npm) | `npm install motely-wasm` then `npx motely-wasm-copy-to-public` → load `/motely-wasm/main.js` in your app |

That’s it. Details below.

---

### 1. Test locally (run.ps1)

From this directory:

```powershell
.\run.ps1
```

Builds the project and serves the AppBundle at http://localhost:3333. Use the page to call `MotelyWasm.AnalyzeSeed(...)` etc. Don’t use `dotnet run`; use this script.

Optional: `.\run.ps1 -NoBuild` (serve only), `.\run.ps1 -Configuration Debug`.

**Test the published bundle locally** (same output as npm / deploy):

```powershell
.\run-published.ps1
```

Runs `dotnet publish -c Release` then serves the publish folder at http://localhost:3333 with COOP/COEP. Use this to verify the trimmed bundle before pushing to npm or deploying. Optional: `.\run-published.ps1 -NoPublish` (serve existing publish only), `.\run-published.ps1 -Port 4444`.

---

### 2. Use in an app (npm)

In your app:

```bash
npm install motely-wasm
npx motely-wasm-copy-to-public
```

That copies the WASM bundle into `public/motely-wasm` (or your `PUBLIC_DIR`). Serve your app with COOP/COEP headers. In the browser, load `/motely-wasm/main.js`; then `globalThis.MotelyWasm` has `AnalyzeSeed`, `SearchSeeds`, `ValidateJaml`, etc.

From Node (e.g. build script): `const { getAppBundlePath } = require('motely-wasm');` → path to the bundle.

---

## API (JS)

After loading `main.js`, `globalThis.MotelyWasm`. In the browser these return **Promises** (you must `await`):

- `await MotelyWasm.AnalyzeSeed(seed, deck, stake, minAnte, maxAnte, optionsJson)` → JSON string
- `await MotelyWasm.SearchSeeds(...)` → JSON string
- `await MotelyWasm.ValidateJaml(jamlString)` → JSON string
- `CancelSearch()` (void), `await IsSearchRunning()`, `await GetSearchProgress()`, `await GetProcessorCount()`, `await GetVersion()`

---

## DuckDB in the browser?

This build **does not use DuckDB** — search uses in-memory storage only (`useInMemoryStorage: true`). DuckDB is still pulled in via Orchestration → Motely.DB and adds trim weight; we don’t call it in WASM.

If you ever wanted DuckDB in the browser:

- **User’s PC:** The official **DuckDB WASM** (JS, [duckdb-wasm](https://github.com/duckdb/duckdb-wasm)) can read a file from the user’s machine **if the user picks the file** (file input or File System Access API). It doesn’t get arbitrary disk access.
- **Remote public DuckLake / HTTP:** **Yes.** DuckDB WASM (JS) supports reading from HTTP URLs (Parquet, etc.) via httpfs; you can `SELECT * FROM 'https://...'`. So a **remote public DuckLake** (or any HTTP-served Parquet/DB) can be queried from the browser with the **official JS duckdb-wasm**, not with this .NET stack.
- **This stack:** We use .NET WASM + in-memory only. To use DuckDB in the browser for remote or user-picked files you’d need the JS duckdb-wasm (or a .NET WASM–compatible DuckDB with httpfs), not DuckDB.NET.Data.

---

## COOP/COEP

Multi-threading needs these response headers:

- `Cross-Origin-Embedder-Policy: require-corp`
- `Cross-Origin-Opener-Policy: same-origin`

`run.ps1` and the AppBundle’s `serve.json` send them when using `npx serve`.

---

## Publish to npm

- **From CI:** Push tag `motely-wasm-v1.0.0` or run the “Publish Motely WASM to npm” workflow. Needs `NPM_TOKEN` in repo secrets. See [NPM_PUBLISH.md](./NPM_PUBLISH.md).
- **From here:** `npm run build` then `npm publish`.

---

## Optional: POC (npm consumer in this repo)

To prove the npm flow from this repo:

```bash
npm run build
cd poc && npm install && npm start
```

Open http://localhost:3333. See `poc/README.md`.
