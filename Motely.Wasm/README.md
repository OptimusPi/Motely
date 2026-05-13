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

const version = Motely.version();
const result = Motely.loadJaml(`
must:
  - joker: Blueprint
    antes: [1]
`);
if (!result.ok) console.error(result.error);
```

When serving from the repository root, `/bin` must resolve to `motely-wasm/bin`. If the module is hosted under a subpath, pass that subpath's binary root, for example `await bootsharp.boot("/motely-wasm/bin")`.

Node usage can work in principle (Bootsharp boots via `fetch()`), but for this package the runtime assets must be HTTP-served or otherwise fetch-accessible; do not rely on raw `file://` paths. Browser usage remains the default path.

## Exported Contract

The generated `Motely` namespace currently exposes:

**JAML**
- `version()` → string (assembly informational version)
- `loadJaml(yaml)` → `{ ok: boolean, error: string | null }`
- `explainJaml(yaml)` → `{ ok: boolean, error: string | null, explanation: string | null }`

**File system (Bootsharp.FileSystem)**
- `pickRoot(options?)` → `Promise<string | null>`
- `mountRoot(root, options?)` → `Promise<string>`
- `unmountRoot(root)` → `Promise<void>`
- `readTextFile(root, uri)` → `Promise<string>`
- `writeTextFile(root, uri, text)` → `Promise<void>`
- `onFileChanges` — event of `Change[]`
