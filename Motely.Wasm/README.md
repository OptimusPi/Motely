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

const status = Motely.validateJaml(`
must:
  - joker: Blueprint
    antes: [1]
`);
if (status !== "valid") console.error(status);
```

When serving from the repository root, `/bin` must resolve to `motely-wasm/bin`. If the module is hosted under a subpath, pass that subpath's binary root, for example `await bootsharp.boot("/motely-wasm/bin")`.

Node usage can work in principle (Bootsharp boots via `fetch()`), but for this package the runtime assets must be HTTP-served or otherwise fetch-accessible; do not rely on raw `file://` paths. Browser usage remains the default path.

## Exported Contract

The generated `Motely` namespace is produced from the `[Export]` members in `Program.cs`. Method names are camel-cased on the JavaScript side.

**JAML**
- `version()` → `string` — assembly informational version
- `validateJaml(jaml)` → `string` — `"valid"`, or the parse/plan error message
- `explainJaml(jaml)` → `string` — human-readable plan summary; `""` when the config has no clauses; throws on invalid JAML

**Search & analysis**
- `createPlan(jaml)` → `JamlSearchPlan` — compiled plan (tally column count, quoted CSV header, tally labels); throws on invalid JAML
- `createSearch(jaml)` → `IMotelySearchSettingsInterop` — runnable search settings built from the JAML; throws on invalid JAML
- `createSearchSettings()` → `IMotelySearchSettingsInterop` — runnable settings with a passthrough filter (no JAML)
- `analyzeJamlSeeds(jaml, seeds)` → `MotelyJamlyzerResult` — analysis of the given seed list against the JAML

**File system (Bootsharp.FileSystem)**
- `pickRoot(options?)` → `Promise<string | null>`
- `mountRoot(root, options?)` → `Promise<string>`
- `unmountRoot(root)` → `Promise<void>`
- `readTextFile(root, uri)` → `Promise<string>`
- `writeTextFile(root, uri, text)` → `Promise<void>`

**Events**
- `onSeedMatch` — `string` (matching seed)
- `onScoredResult` — `MotelyScoredSeedResult`
- `onProgress` — `MotelyProgress`
- `onFileChanges` — `Change[]`

Search callbacks (`onSeedMatch`, `onScoredResult`, `onProgress`) are wired into the settings returned by `createSearch` / `createSearchSettings`.
