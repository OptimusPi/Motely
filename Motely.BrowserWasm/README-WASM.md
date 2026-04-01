# Motely browser WASM (`motely-wasm` / `motely-wasm-mt`)

## What Bootsharp actually does

**`dotnet publish Motely.BrowserWasm`** is the **entire** build. [Bootsharp](https://github.com/korif/Bootsharp) emits the **complete** npm package: `index.mjs`, WASM, TypeScript typings, and the JS interop glue. You are **not** hand-assembling an npm package.

After publish, the output folder (`Motely.BrowserWasm/motely-wasm/` or `motely-wasm-mt/`) is a **real** package you can `npm pack`, depend on with `"file:…"`, or publish to the registry.

The only extra step in this repo is **MSBuild**: it copies `Motely/package.json` (and `jaml.schema.json`) **on top of** Bootsharp’s output so `name`, `description`, `exports`, etc. match what you want on npm. **Version** comes from `$(MotelyVersion)` in `Directory.Packages.props` via the project `Version` property — not from editing `package.json` by hand for every release.

### JavaScript: default export = boot API, **`Motely` = named export**

Bootsharp’s `index.mjs` ends with something like `export { Event, Motely, … as default }`. Use it like this:

```js
import dotnet, { Motely } from "motely-wasm";

await dotnet.boot(); // or dotnet.boot({ root: "/path/to/motely-wasm-mt/bin" } for threaded)

const Program = Motely.BrowserWasm.MotelyProgram;
const ver = Program.getVersion();
```

Types are under `motely-wasm/types/`; `Motely` holds enums (`MotelyDeck`, `MotelyStake`) and namespaces (`Motely.BrowserWasm.MotelyProgram`, `Motely.BrowserWasm.SearchEvents`).

---

## Commands (from repo root `MotelyJAML`)

### Single-thread package (default)

```powershell
dotnet publish Motely.BrowserWasm -c Release
```

Output: **`Motely.BrowserWasm/motely-wasm/`**

### Multi-thread (pthread) package

Some LLVM/SDK combos fail to link threaded WASM — only ship after a **successful** local publish.

```powershell
dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release /p:MotelyWasmThreads=true
```

Output: **`Motely.BrowserWasm/motely-wasm-mt/`** (includes `bin/` with `*.wasm` for the threaded loader).

Threaded WASM in the browser needs **cross-origin isolation** (`COOP: same-origin`, `COEP: require-corp` or similar). The default single-thread package does **not** require these.

### Optional: `npm pack` / `npm publish`

Tarball verification or registry publish still use the **npm CLI** (requires Node on that machine, or run in CI):

```powershell
cd Motely.BrowserWasm/motely-wasm
npm pack --dry-run
```

From repo root, **`./Build-NpmPackages.ps1`** stamps version from `Directory.Packages.props` and runs `npm pack` / `npm publish`.

---

## SeedSearcherWebsite

Static QA: **`build-website.sh`** / **`Build-Website.ps1`** copy `motely-wasm/`, `motely-wasm-mt/`, and the HTML/JS harness into `dist/` (no bundler). Deploy `dist/` with Vercel or any host that serves `*.wasm` with the right `Content-Type` and COOP/COEP where needed.
