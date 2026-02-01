# motely-wasm

**Browser-only** Balatro seed analyzer and searcher — WebAssembly (SIMD + optional multi-threading). Analyze a seed or search seeds with a JAML filter. No server required.

---

## Quick start (TypeScript website, out of the box)

```bash
npm install motely-wasm
```

That’s it. The package copies its WASM build to `public/motely-wasm` on install (postinstall). Your app must serve that folder as static files (e.g. `public/` in Vite/Next/CRA). To use a different path: `npx motely-wasm-prepare public/your-path`.

In your TypeScript code:

```typescript
import { loadMotely } from 'motely-wasm';
import type { MotelyWasmApi, SeedAnalysisResult, ErrorResult } from 'motely-wasm';

// Load the API (path = where you served the WASM, default /motely-wasm)
const api: MotelyWasmApi = await loadMotely('/motely-wasm');

const json = await api.AnalyzeSeed('TACO1111', 'Red', 'White', 1, 8, '{}');
const result = JSON.parse(json) as SeedAnalysisResult | ErrorResult;
if ('error' in result) {
  console.error(result.error);
} else {
  console.log(result.seed, result.antes);
}
```

Types resolve automatically; no extra config. For multi-threading, your server must send [COOP/COEP headers](#multi-threading-coopcoep) (see below).

### Optional: DuckDB-WASM results storage

If you want browser-side persistence of search results, initialize DuckDB-WASM before running a search:

```typescript
import { loadMotely, initDuckDbWasmResults } from 'motely-wasm';

await initDuckDbWasmResults({ tableName: 'motely_results' });
const api = await loadMotely('/motely-wasm');

// Now SearchSeeds will also stream results into DuckDB-WASM
await api.SearchSeeds(jamlJson, null, 4, 1000);
```

`initDuckDbWasmResults` hooks into `globalThis.MotelyWasmOnResult` and streams each result into DuckDB-WASM.

---

## Setup (other workflows)

Copy runs automatically on `npm install`. To copy again or to a different path:

```bash
npx motely-wasm-prepare
# or
npx motely-wasm-prepare public/my-wasm
```

Or use `require('motely-wasm').getDistPath()` in your own build step.

---

## Installation

```bash
npm install motely-wasm
```

## Load the API in the browser (no bundler)

If you're not using a bundler, load the runtime directly from your public path:

```js
const { dotnet } = await import('/motely-wasm/_framework/dotnet.js');
const { getAssemblyExports, getConfig } = await dotnet.create();
const exports = await getAssemblyExports(getConfig().mainAssemblyName);
const api = exports.Motely.WASM.MotelyWasm;

const result = await api.AnalyzeSeed("TACO1111", "Red", "White", 1, 8, "{}");
console.log(JSON.parse(result));
```

### Multi-threading (COOP/COEP)

Your server MUST send these headers for WASM multi-threading to work:

```
Cross-Origin-Embedder-Policy: require-corp
Cross-Origin-Opener-Policy: same-origin
```

Without these headers, the WASM will still load but run single-threaded.

**Other frameworks:** Run `npx motely-wasm-prepare` or use `getDistPath()` in your own script so your app can serve the WASM; then load `/motely-wasm/_framework/dotnet.js` in the browser as above.

## API Reference

**Browser API:** After loading the WASM module, use `exports.Motely.WASM.MotelyWasm`. All 9 methods are exported from C# (JSExport); SearchSeeds optionally calls your JS callbacks (JSImport) for progress and results. All methods are async and return JSON strings; parse with `JSON.parse()`.

| Method | Description |
|--------|-------------|
| `AnalyzeSeed(seed, deck, stake, minAnte, maxAnte, optionsJson)` | Analyze one seed; returns ante data. |
| `SearchSeeds(jamlFilterJson, seedList, threadCount, maxResults?)` | Search seeds matching a JAML filter. Optional: set `globalThis.MotelyWasmOnProgress`, `MotelyWasmOnResult`, `MotelyWasmOnComplete` before calling. |
| `ValidateJaml(jamlString)` | Validate JAML without searching. |
| `CancelSearch()` | Cancel in-progress search. |
| `IsSearchRunning()` | Returns whether a search is running. |
| `GetSearchProgress()` | Current progress JSON (alternative to callbacks). |
| `GetLastSearchResult()` | Last search result JSON (cleared after read). |
| `GetProcessorCount()` | Number of threads (use for `threadCount`). |
| `GetVersion()` | Version info JSON. |

### AnalyzeSeed

Analyze a specific seed and return all ante data.

```typescript
await MotelyWasm.AnalyzeSeed(
  seed: string,        // e.g., "TACO1111"
  deck: string,        // "Red", "Blue", "Yellow", "Green", "Black", "Magic", "Nebula", "Ghost", etc.
  stake: string,       // "White", "Red", "Green", "Black", "Blue", "Purple", "Orange", "Gold"
  minAnte: number,     // 1-8
  maxAnte: number,     // 1-8
  optionsJson: string  // "{}" (reserved for future options)
): Promise<string>     // Returns SeedAnalysisResult or ErrorResult JSON
```

**Response:**
```typescript
interface SeedAnalysisResult {
  seed: string;
  deck: string;
  stake: string;
  erraticDeckComposition: string[];
  twos: number;
  antes: AnteAnalysis[];
}

interface AnteAnalysis {
  ante: number;
  boss: string;
  voucher: string;
  smallBlindTag: string;
  bigBlindTag: string;
  drawOrder: string;
  shopQueue: { id: string; name: string }[];
  packs: { type: string; items: string[] }[];
}
```

### SearchSeeds

Search for seeds matching a JAML filter.

```typescript
await MotelyWasm.SearchSeeds(
  jamlFilterJson: string,  // JAML filter as string
  seedList: string | null, // Comma-separated seeds to search, or null for sequential
  threadCount: number,     // Number of threads (use GetProcessorCount())
  maxResults?: number      // Max results to return (default 1000)
): Promise<string>         // Returns SearchResponse or ErrorResult JSON
```

**Callbacks:** Set these on `globalThis` BEFORE calling SearchSeeds:

```javascript
// Progress updates (called every ~200ms)
globalThis.MotelyWasmOnProgress = (progressJson) => {
  const progress = JSON.parse(progressJson);
  console.log(`Searched: ${progress.searchedCount}, Found: ${progress.foundCount}`);
};

// Each matching seed as it's found
globalThis.MotelyWasmOnResult = (seed, score, talliesStr) => {
  const tallies = talliesStr ? talliesStr.split(',').map(Number) : [];
  console.log(`Found: ${seed} (score: ${score})`);
};

// Search complete
globalThis.MotelyWasmOnComplete = (resultJson) => {
  const result = JSON.parse(resultJson);
  console.log(`Search complete! Found ${result.foundCount} seeds.`);
};
```

**Response:**
```typescript
interface SearchResponse {
  results: SearchHit[];    // Empty if using callbacks
  totalSearched: number;
  foundCount: number;
  cancelled: boolean;
}

interface SearchProgress {
  searchedCount: number;
  foundCount: number;
  status: string;
  percentComplete: number;
  seedsPerSecond: number;
  threadCount: number;
}
```

### ValidateJaml

Validate a JAML filter without running a search.

```typescript
await MotelyWasm.ValidateJaml(jamlString: string): Promise<string>
```

**Response:**
```typescript
interface ValidateResult {
  valid: boolean;
  error?: string;
  name?: string;
  deck?: string;
  stake?: string;
}
```

### CancelSearch

Cancel an in-progress search.

```typescript
await MotelyWasm.CancelSearch(): Promise<void>
```

### IsSearchRunning

Check if a search is currently running.

```typescript
await MotelyWasm.IsSearchRunning(): Promise<boolean>
```

### GetSearchProgress

Get current search progress (alternative to callbacks).

```typescript
await MotelyWasm.GetSearchProgress(): Promise<string>  // Returns SearchProgress JSON
```

### GetLastSearchResult

Get the last search result JSON (cleared after retrieval). Use as an alternative to `MotelyWasmOnComplete` when not using callbacks.

```typescript
await MotelyWasm.GetLastSearchResult(): Promise<string>  // Returns SearchResponse or empty
```

### GetProcessorCount

Get the number of processors/threads available.

```typescript
await MotelyWasm.GetProcessorCount(): Promise<number>
```

### GetVersion

Get version information.

```typescript
await MotelyWasm.GetVersion(): Promise<string>
// Returns: { version: "1.0.2", runtime: "browser-wasm", features: [...] }
```

## JAML Filter Format

JAML is a YAML-based format for defining seed search filters. By default we call it:

- **J**imbo's **A**nte **M**arkup **L**anguage

And in the wild it also cheekily stands for:

- JAML: **A**ntes, **M**ult, **L**ove

Schema file (for tooling / autocomplete): `jaml.schema.json` in this package.

```yaml
name: My Filter
deck: Red
stake: White

must:
  - joker: Blueprint
    antes: [1, 2]
  - voucher: Overstock
    antes: [1]

should:
  - joker: Showman
    score: 10
  - spectralCard: Aura
    antes: [1, 2]
```

## Troubleshooting

### "SharedArrayBuffer is not defined"

Your server is missing COOP/COEP headers. Add:
```
Cross-Origin-Embedder-Policy: require-corp
Cross-Origin-Opener-Policy: same-origin
```

### Search runs slowly

Make sure COOP/COEP headers are set for multi-threading. Without them, WASM runs single-threaded.

### Module not found

Ensure the `dist/_framework/` folder is copied to your public directory and accessible at the URL you're importing from.

## License

MIT
