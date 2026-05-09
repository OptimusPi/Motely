# motely-wasm

SIMD-vectorized [Balatro](https://www.playbalatro.com/) seed search in the browser — the [Motely](https://github.com/OptimusPi/MotelyJAML) engine compiled to WebAssembly via NativeAOT-LLVM, driven by **JAML** (Jimbo's Ante Markup Language) filters.

- **Single-file ES module.** Embedded WASM binary, no sideloaded resources, no `.wasm` fetch path to configure.
- **Single-threaded.** No `SharedArrayBuffer`, no COOP/COEP headers required — runs on Vercel, Cloudflare Pages, GitHub Pages, MCP Apps iframes, and every other locked-down host.
- **Browser + Node + Deno + Bun + Edge.** Pure ES module, no Node-only APIs.
- **Typed.** Generated TypeScript declarations for the full Motely / JAML surface ship in `types/`.

## Install

```bash
npm install motely-wasm
```

## Boot & search

```ts
import motely, { MotelyWasm, MotelyWasmEvents } from "motely-wasm";

await motely.boot();

const jaml = `
name: Blueprint Copy Engine
deck: Red
stake: White
must:
  - rareJoker: Blueprint
    antes: [1, 2, 3]
should:
  - rareJoker: Brainstorm
    score: 80
`;

// Events are mutable handler slots — assign your callback, do NOT call .subscribe().
MotelyWasmEvents.notifyResult   = (seed, score, tallyColumns) => console.log(seed, score, tallyColumns);
MotelyWasmEvents.notifyProgress = (seedsSearched, matchingSeeds) => { /* … */ };
MotelyWasmEvents.notifyComplete = (status, totalSeedsSearched, matchingSeeds) => { /* … */ };

const search = MotelyWasm.startRandomSearch(jaml, 10_000);
// later: search.cancel(); search.dispose();
```

> JAML is **not** YAML — it's the Motely filter language. The schema ships at [`motely-wasm/jaml.schema.json`](./jaml.schema.json) and is also returned at runtime by `MotelyWasm.getJamlSchema()`.

## Search modes

| Method | Purpose |
|---|---|
| `startSequentialSearch(jaml, batchCharCount, startBatch, endBatch)` | Deterministic walk through a slice of the 35⁸ ≈ 2.25T seed space. |
| `startRandomSearch(jaml, randomSeedCount)` | Random sampling. |
| `startSeedListSearch(jaml, seeds[])` | Verify a known list of seeds against a JAML filter. |
| `startKeywordSearch(jaml, keywordsCsv, paddingChars)` | Match seeds containing keywords (CSV input, padding controls anchoring). |
| `startAestheticSearch(jaml, JamlAesthetic)` | Curated themed pools: `Palindrome`, `Psychosis`, `Gross`, `Nsfw`, `Funny`, `Balatro`. |

All searches return an `IMotelyWasmSearch` with `getSnapshot()`, `cancel()`, `waitForCompletion()`, `dispose()`. Hits are streamed through `MotelyWasmEvents.notifyResult`.

## Single-seed inspection

```ts
const ctx = MotelyWasm.createSearchContext("DPADD313", JamlDeck.Red, JamlStake.White);
const boss = ctx.getBossForAnte(1);
// + voucher / tag / booster / shop item / joker / tarot / spectral / planet streams,
// each with a *Chunk variant for batched pulls.
ctx.dispose();
```

## Loading without a bundler

For environments where bundling 11 MB of WASM into every deployment artifact is wrong (MCP Apps single-HTML resources, Cloudflare Workers, Vercel Functions, plain `<script type="module">`), import the package directly from a public npm CDN:

```ts
const mod = await import(
  "https://unpkg.com/motely-wasm@16.0.0/index.mjs"
);
await mod.default.boot();
const { MotelyWasm, MotelyWasmEvents } = mod;
```

Equivalent jsDelivr URL: `https://cdn.jsdelivr.net/npm/motely-wasm@16.0.0/index.mjs`. **Pin the version** — `@latest` defeats long-term browser caching.

### Content Security Policy

For sandboxed iframes (MCP Apps), allow both script loading and fetch/import from whichever CDN you chose:

```
script-src https://unpkg.com https://cdn.jsdelivr.net
connect-src https://unpkg.com https://cdn.jsdelivr.net
```

## Types

```ts
import type {
  IMotelyWasmSearch,
  IMotelyWasmSearchContext,
  MotelyWasmSearchSnapshot,
  MotelyWasmSearchCompletion,
  JamlDeck, JamlStake, JamlAesthetic,
} from "motely-wasm";
```

The generated `types/bindings.g.d.ts` is the source of truth — Bootsharp emits it from the C# interfaces, so it never drifts from runtime behavior.

## Build details

NativeAOT-LLVM with SIMD enabled (`-msimd128`), threads explicitly disabled, binaries embedded in the single `index.mjs`. The `MONO_WASM:` prefix in console logs is Bootsharp's glue log — this is **not** a Mono runtime build.

## License

MIT — © pifreak
