# motely-wasi

Backend-ready Motely seed searcher using WebAssembly System Interface (WASI).

Runs Motely's seed analysis and JAML validation in Node.js via wasmtime, wasmer, or Node's built-in WASI support.

## Prerequisites

Install one of these WASI runtimes:

- **wasmtime** (recommended): `curl https://wasmtime.dev/install.sh -sSf | bash`
- **wasmer**: `curl https://get.wasmer.io -sSfL | sh`
- Or use Node.js >= 16 with `--experimental-wasi-unstable-preview1`

## Quick Start

```js
const { MotelyWasi } = require('motely-wasi');

async function main() {
  const motely = await MotelyWasi.load({ runtime: 'wasmtime' });

  // Validate a JAML filter
  const result = await motely.validateJaml(`
name: Blueprint Hunt
must:
  - joker: Blueprint
    antes: [1, 2]
`);
  console.log(result); // { valid: true, name: 'Blueprint Hunt', deck: 'Red', stake: 'White' }

  // Analyze a seed
  const analysis = await motely.analyzeSeed('ABC123', 'Red', 'White');
  console.log(analysis.antes[0]); // { ante: 1, boss: 'TheHook', voucher: 'Overstock', ... }

  motely.close();
}

main();
```

## Building from Source

```bash
# From the MotelyJAML root:
dotnet publish Motely.WASI/Motely.WASI.csproj -c Release

# The .wasm binary is automatically copied to Motely-WASI.NPM/wasm/
```

## API

### `MotelyWasi.load(opts?)`

Load the WASI binary. Returns a `MotelyWasi` instance.

### `motely.validateJaml(jaml: string)`

Validate a JAML filter string.

### `motely.analyzeSeed(seed, deck, stake)`

Analyze a single seed's full 8-ante breakdown.

### `motely.getCapabilities()`

Get runtime capabilities (SIMD, threads, version).

### `motely.close()`

Kill the WASI process and clean up.

## Architecture

```
Node.js  ──stdin──→  wasmtime/wasmer  ──→  Motely.WASI.wasm
         ←stdout──                     ←──  (.NET 10 AOT+SIMD)
```

Communication uses newline-delimited JSON (NDJSON) over stdio.
No native Node.js addons, no SharedArrayBuffer, no COOP/COEP headers needed.
