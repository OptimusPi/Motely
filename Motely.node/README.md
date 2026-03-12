# motely-node

MotelyJAML for **Node.js** — Balatro seed analyzer and JAML filter engine.

This package ships a **platform-specific native `.node` addon** for Node.js. It does **not** use the browser WASM runtime, and consumers should not need hand-rolled loader glue to use it.

## Installation

```bash
npm install motely-node
```

## Requirements

- Node.js >= 24.0.0 (LTS)

## CommonJS and ES

The package supports **both** CommonJS and ES modules. Use the one that matches your project.

**CommonJS**

```javascript
const { loadMotely } = require('motely-node');

(async () => {
  const motely = await loadMotely();
  const caps = await motely.getCapabilities();
  console.log('Runtime:', caps.runtime);
  motely.dispose();
})();
```

**ES**

```javascript
import { loadMotely } from 'motely-node';

const motely = await loadMotely();
const caps = await motely.getCapabilities();
console.log('Runtime:', caps.runtime);
motely.dispose();
```

## Proof it works

From the package directory (after `dotnet publish` has populated `addon/win-x64/` through `prepack` or staging):

```bash
npm run prove
```

Writes `prove-node-result.txt` with getCapabilities, validateJaml, and analyzeSeed results. Exit code 0 = OK.

## Usage

Initialize the native addon once, then reuse the returned API. (Use [CommonJS or ES](#commonjs-and-es) imports as above.)

```javascript
import { loadMotely } from 'motely-node';

// Initialize the packaged native addon
const motely = await loadMotely();

// Get capabilities
const caps = await motely.getCapabilities();
console.log('SIMD:', caps.simd);
console.log('Runtime:', caps.runtime);
console.log('Threads available:', motely.getAvailableThreadCount());

// Validate JAML
const validation = await motely.validateJaml(`
name: Test Filter
deck: Red
stake: White
must:
  - joker: Joker
`);
console.log('Valid:', validation.valid);

// Analyze a seed
const analysis = await motely.analyzeSeed('ABCD1234', 'Red', 'White');
console.log('Boss at ante 1:', analysis.antes[0].boss);
console.log('Draw order:', analysis.antes[0].drawOrder); // e.g., "H S D C"
console.log('Erratic deck:', analysis.erraticDeckComposition);
analysis.antes[0].shopQueue.forEach(item => {
  console.log(`  Shop: ${item.name}`);
});

// Search with JAML
const results = await motely.startJamlSearch(`
name: Demo Search
deck: Red
stake: White
must:
  - joker: Joker
`, {
  randomSeeds: 1000,
  threadCount: 4,
  batchCharCount: 4,
  onProgress: (searched, matches, elapsed, count) => {
    console.log(`Searched: ${searched}, Matches: ${matches}`);
  },
  onResult: (seed, score) => {
    console.log(`Found: ${seed} (score: ${score})`);
  }
});
console.log('Final result count:', results.length);

// Cleanup
motely.dispose();
```

## Loading behavior

By default, `loadMotely()` resolves the addon at:

```text
addon/<rid>/Motely.NodeAddon.node
```

For the currently published package flow, that means:

```text
addon/win-x64/Motely.NodeAddon.node
```

You can override the path when embedding or testing:

- `addonPath` — full path to a specific `.node` file
- `addonDirectory` — directory containing RID folders such as `win-x64/`
- `frameworkPath` — legacy alias retained for older consumers; treated like `addonDirectory`

## Server helpers

For server runtimes like Next.js route handlers, the package also exports:

```javascript
import { analyzeSeedServer, getServerApi, startJamlSearchServer } from 'motely-node/server';
```

That subpath provides a cached singleton API loader so app code does not need to maintain its own drifting `motelyServer.ts` wrapper.

## API

### `loadMotely(options?): Promise<MotelyNodeApi>`

Initialize Motely using the packaged native Node addon.

**Options:**
- `addonPath?: string` - Full path to a specific native `.node` addon file
- `addonDirectory?: string` - Directory containing RID folders such as `win-x64/`
- `frameworkPath?: string` - Legacy alias for `addonDirectory`
- `pollIntervalMs?: number` - Poll interval for progress collection during `startJamlSearch`

### `MotelyNodeApi`

#### `getCapabilities(): Promise<CapabilitiesInfo>`

Get runtime capabilities (SIMD, threads, processor count).

#### `analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>`

Analyze a Balatro seed and get ante-by-ante breakdown.

The returned object includes normalized consumer-facing fields used by downstream apps:

- `erraticDeckComposition: string[]`
- `antes[].drawOrder: string`

#### `validateJaml(jamlContent: string): Promise<ValidateResult>`

Validate JAML filter syntax.

#### `startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchResultInfo[]>`

Start a JAML-based seed search and resolve with the collected matching results.

**Options:**
- `randomSeeds?: number` - Number of random seeds to search (default: 1000)
- `cutoff?: string | number` - Search cutoff criteria
- `specificSeed?: string` - Search exactly one seed
- `seeds?: string[]` - Restrict the search to an explicit list of seeds
- `keyword?: string` - Restrict the search to seeds matching a keyword pattern
- `threadCount?: number` - Requested search thread count
- `batchCharCount?: number` - Search batch width (1-7)
- `onProgress?: (searched, matches, elapsed, count) => void` - Progress callback
- `onResult?: (seed, score) => void` - Result callback

#### `dispose(): void`

Cleanup and free resources.

## Notes for server-app integrations

- `motely-node` is a **server/runtime package**, not a browser package.
- In Next.js or similar SSR apps, load it only in **Node runtime** code paths.
- You should not need a wrapper just to normalize `analyzeSeed()` payload fields or hand-resolve the packaged addon path.

## License

MIT
