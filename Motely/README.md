# motely

**Balatro seed search, analysis, and JAML filtering.** Works in Node.js and the browser via WebAssembly.

## Installation

```bash
npm install motely
```

## Node.js Usage

```javascript
const { MotelyWasm } = require("motely");

// Use native Node addon directly
const result = MotelyWasm.searchSeeds(/* ... */);
```

## Browser Usage

```javascript
import { boot, MotelyWasm } from "motely";

// Boot the WASM runtime once
await boot();

// Use the same API
const result = MotelyWasm.searchSeeds(/* ... */);
```

## How It Works

- **Node.js**: Uses native addon compiled with .NET NativeAOT
- **Browser**: Uses WASM compiled from .NET with Bootsharp runtime

The package automatically selects the correct version based on the environment. Both targets expose the same `MotelyWasm` API.

## Building

From the repo root:

```bash
./build-and-pack.ps1
```

This will:
1. Auto-bump the patch version
2. Compile both targets (net10.0 + net10.0-browser)
3. Stage artifacts into `dist/`
4. Pack into npm tarball

## Publishing

```bash
cd motely
npm publish motely-X.Y.Z.tgz --access public
```

## License

MIT
