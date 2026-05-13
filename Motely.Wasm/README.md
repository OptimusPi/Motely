# Motely.Wasm

Bootsharp host for the browser/JavaScript Motely package. It keeps all Bootsharp attributes, dependency injection, and JavaScript interop concerns out of the core `Motely` library.

## Build

```powershell
dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Debug
```

Debug publishes use the regular .NET browser WASM toolchain and are the quickest way to verify the contract. Release publishes enable Bootsharp's NativeAOT-LLVM path and require the local Bootsharp alpha feed/toolchain described in the repo `nuget.config`; install Binaryen's `wasm-opt` for fully optimized release output.

The generated ES module is written to `motely-wasm/`:

- `motely-wasm/index.mjs` and generated bindings
- `motely-wasm/package.json`
- `motely-wasm/bin/` runtime and WASM binaries

## JavaScript Usage

Bootsharp 0.8's browser boot API takes the runtime resource root directly:

```js
import bootsharp, { Motely } from "./motely-wasm/index.mjs";

await bootsharp.boot("/bin");

const info = JSON.parse(Motely.getHostInfo());
const validation = JSON.parse(Motely.validateJaml(`
must:
  - joker: Blueprint
    antes: [1]
`));
```

When serving from the repository root, `/bin` must resolve to `motely-wasm/bin`. If the module is hosted under a subpath, pass that subpath's binary root, for example `await bootsharp.boot("/motely-wasm/bin")`.

## Exported Contract

The generated `Motely` namespace exposes JSON-returning methods:

- `getHostInfo()`
- `validateJaml(jaml)`
- `analyzeSeed(seed, deck?, stake?)`
- `analyzeJamlSeed(jaml, seed, includeSeedAnalysis?)`
- `analyzeJamlSeeds(jaml, seeds?, includeSeedAnalysis?)`
- `searchJamlPage(jaml, startBatch?, endBatch?, batchCharacterCount?, includeSeedAnalysis?)`

`searchJamlPage` deliberately runs Motely's bounded Jamlyzer path with one worker thread. That keeps the browser surface deterministic and avoids leaning on the known multi-threaded search issues until the engine's threading path is fixed end-to-end.

Browser file-system access is not included. The current host accepts JAML text and seed arrays directly; add `Bootsharp.FileSystem` later only if a UI needs user-selected local files or mounted directories.
