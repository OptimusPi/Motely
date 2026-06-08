# motely-wasm

Balatro seed search + per-seed analysis, powered by JAML (Jimbo's Ante Markup Language). Vectorized SIMD engine, compiled to WebAssembly via Bootsharp.

## Boot

WASM is embedded. No binary files to serve or copy.

```ts
import * as bootsharp from "motely-wasm";
import type { MotelyScoredSeedResult } from "motely-wasm/motely";

// 1. Assign [Import] bindings BEFORE boot().
bootsharp.Motely.jimmolatePredicate = (result: MotelyScoredSeedResult) => {
  return myProbe(result.seed);
};

// Optional: init file system (browser File System Access API) BEFORE boot().
// import { fs } from "motely-wasm/generated/modules/fs.g.mjs";
// fs.init(myFileMounter);

// 2. Boot (embedded, no args).
await bootsharp.boot();

// 3. Engine is ready.
```

> **Important:** `[Import]` bindings are snapshotted at `boot()`. Assign them all before calling it. You can swap the `jimmolatePredicate` dispatcher post-boot by reassigning `bootsharp.Motely.jimmolatePredicate`.

## Subpath exports

| Import | Contents |
|--------|----------|
| `motely-wasm` | Top-level: `boot()`, `Motely` namespace, `Program` namespace |
| `motely-wasm/motely` | Types: `MotelyScoredSeedResult`, etc. |
| `motely-wasm/motely/wasm` | `Program` namespace (seed search, JAML parsing) |
| `motely-wasm/motely/enums` | Engine enums |
| `motely-wasm/motely/filters/jaml` | JAML / aesthetic filter types |

## API

```ts
import { Program } from "motely-wasm/motely/wasm";

// Parse JAML — throws on invalid.
const config = Program.parseJaml(jamlString);

// Run seed search (streaming via jimmolatePredicate).
await Program.jimmolate(config);
```

## Browser shims

The `browser` field in `package.json` stubs out Node built-ins (`node:fs`, `node:url`, etc.) to `false` so bundlers (Vite, Webpack) skip them cleanly.

## Build

```sh
npm run build   # dotnet publish ../Motely.Wasm/Motely.Wasm.csproj -c Release
```

Output lands in `dist/`. Bootsharp regenerates the entire `dist/` directory on every publish.

## Publish

```sh
npm version patch   # bump version (never hand-edit)
npm publish
```
