# motely-node

MotelyJAML for **Node.js** — Balatro seed analyzer and JAML filter engine.

**High-level:** This package is for **Node.js only** (not WASI, not browser). The intended path is [*Develop a Node.js addon module in C# with .NET Native AOT*](https://microsoft.github.io/node-api-dotnet/scenarios/): a native `.node` binary that loads fast and does not depend on the .NET runtime. Today you can use either the **node-api-dotnet addon** (DLL + `addonPath`) or a **WASM fallback** (browser-style .NET WASM run in Node via `_framework`). Native AOT will produce a single platform-specific `.node` file for the addon path.

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

From the package directory (after `dotnet publish` has populated `_framework/`):

```bash
npm run prove
```

Writes `prove-node-result.txt` with getCapabilities, validateJaml, and analyzeSeed results. Exit code 0 = OK.

## Usage

Initialize the runtime, then call the API. (Use [CommonJS or ES](#commonjs-and-es) imports as above.)

```javascript
import { loadMotely } from 'motely-node';

// Initialize the runtime
const motely = await loadMotely();

// Get capabilities
const caps = await motely.getCapabilities();
console.log('SIMD:', caps.simd);
console.log('Runtime:', caps.runtime);

// Validate JAML
const validation = await motely.validateJaml(`
name: "Test Filter"
deck: Red
stake: White
filters:
  - joker: Joker
    ante: 1
`);
console.log('Valid:', validation.valid);

// Analyze a seed
const analysis = await motely.analyzeSeed('ABCD1234', 'Red', 'White');
console.log('Boss at ante 1:', analysis.antes[0].boss);
console.log('Draw order:', analysis.antes[0].drawOrder); // e.g., "H S D C"
analysis.antes[0].shopQueue.forEach(item => {
  console.log(`  Shop: ${item.name}`);
});

// Search with JAML
await motely.startJamlSearch(jamlContent, {
  randomSeeds: 1000,
  threadCount: 4,       // default: all available cores
  batchCharCount: 4,    // default: 4 (1.5M seeds per batch, range 1-7)
  onProgress: (searched, matches, elapsed, count) => {
    console.log(`Searched: ${searched}, Matches: ${matches}`);
  },
  onResult: (seed, score) => {
    console.log(`Found: ${seed} (score: ${score})`);
  }
});

// Cleanup
motely.dispose();
```

## API

### `loadMotely(options?): Promise<MotelyNodeApi>`

Initialize Motely. Prefer **node-api-dotnet** addon when `addonPath` is set (in-process, faster); otherwise use .NET WASM.

**Options:** ([reference](https://microsoft.github.io/node-api-dotnet/reference/js/))
- `addonPath?: string` - Path to `Motely.NodeAddon.dll`. Uses `node-api-dotnet` (optional dependency). Build from `Motely.NodeAddon` in the MotelyJAML repo.
- `frameworkPath?: string` - When using WASM, path to the `_framework` directory (default: `./_framework`)

### `MotelyNodeApi`

#### `getCapabilities(): Promise<CapabilitiesInfo>`

Get runtime capabilities (SIMD, threads, processor count).

#### `analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>`

Analyze a Balatro seed and get ante-by-ante breakdown.

#### `validateJaml(jamlContent: string): Promise<ValidateResult>`

Validate JAML filter syntax.

#### `startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<void>`

Start a JAML-based seed search.

**Options:**
- `randomSeeds?: number` - Number of random seeds to search (default: 1000)
- `cutoff?: string` - Search cutoff criteria
- `onProgress?: (searched, matches, elapsed, count) => void` - Progress callback
- `onResult?: (seed, score) => void` - Result callback

#### `dispose(): void`

Cleanup and free resources.

## Node API for .NET (addon)

For in-process, faster execution use the **node-api-dotnet** addon:

1. Build the addon: from the MotelyJAML repo run `dotnet build Motely.NodeAddon/Motely.NodeAddon.csproj -c Release`.
2. Install optional dependency: `npm install node-api-dotnet` (or it is listed as optionalDependency).
3. Load with the DLL path:

```javascript
import { loadMotely } from 'motely-node';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const dir = path.dirname(fileURLToPath(import.meta.url));
const motely = await loadMotely({
  addonPath: path.join(dir, 'path-to-addon', 'Motely.NodeAddon.dll'),
});
```

Same API; `runtime` in capabilities will be `"node-addon"`. Supports multi-threading. See [Node API for .NET](https://microsoft.github.io/node-api-dotnet/) and [.NET module for Node.js](https://microsoft.github.io/node-api-dotnet/scenarios/js-dotnet-module.html).

## Differences from Browser Version

- Default: same .NET WASM runtime, adapted for Node.js. Optional: node-api-dotnet addon for in-process execution.
- No browser-specific APIs (no DOM, no window object)
- File system access via Node.js APIs
- Multi-threading support (addon or WASM when Node supports SharedArrayBuffer)
- Works in Node.js (Deno/Bun support may vary)

## License

MIT
