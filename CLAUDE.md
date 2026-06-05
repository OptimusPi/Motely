# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```sh
# Build
dotnet build Motely.slnx
dotnet build Motely.slnx -c Release

# Test
dotnet test Motely.Tests/Motely.Tests.csproj"

# Publish CLI (AOT native binary)
dotnet publish Motely.CLI/Motely.CLI.csproj -c Release

# Publish WASM (outputs to motely-wasm/dist/) — MUST be Release for NativeAOT/LLVM; debug = Mono = broken tests
dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release

# Clean WASM publish (nuke ALL stale artifacts first — bin/obj/dist hold garbage that breaks the build)
Remove-Item -Recurse -Force motely-wasm/dist, Motely.Wasm/bin, Motely.Wasm/obj, Motely/bin, Motely/obj
dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release

# Publish motely-wasm to npm (run after WASM publish)
cd motely-wasm && npm publish

# WASM Node tests (requires WASM publish first)
node Motely.Wasm/motely.test.mjs
```

**Never kick off a live seed search to verify changes.** Build and run tests. `JamlyzerUnitTests` has known-seed ground-truth assertions.

## Architecture

MotelyJAML forks [Motely](https://github.com/Tacodiva/Motely) and adds JAML (a declarative filter language), JAMLyzer (per-seed analysis), and a WASM/JS distribution.

### Projects

- **`Motely/`** — core library: SIMD engine, JAML compiler, JAMLyzer, all game-domain simulation
- **`Motely.CLI/`** — AOT CLI (`McMaster.Extensions.CommandLineUtils`)
- **`Motely.TUI/`** — Terminal.Gui TUI; its source lives in `Motely/TUI/` and is compiled in via `<Compile Include>`
- **`Motely.Wasm/`** — `browser-wasm` target via Bootsharp; publishes to `motely-wasm/`
- **`Motely.DataLake/`** — DuckDB result sink for bulk runs
- **`Motely.Tests/`** — xunit

Version is centrally managed via `MotelyVersion` in `Directory.Packages.props`. All package versions live there, not in individual `.csproj` files.

### Engine: two parallel simulation paths

- **Vector path** (`MotelyVectorSearchContext.*.cs`) — processes 8 seeds at once using AVX-512. Hot search path.
- **Scalar path** (`MotelySingleSearchContext.*.cs`) — one seed at a time.

Both are split into partial classes per game domain: `.Jokers`, `.Shop`, `.Tarot`, `.Vouchers`, `.Tags`, `.Packs`, `.Planets`, `.Boss`, `.Spectral`, `.StandardCards`, `.Shuffle`, `.Misc`.

PRNG/seed math: `SeedMath.cs`, `LuaRandom.cs` / `VectorLuaRandom.cs`, `MotelyPrngKeys.cs`.

A filter is an `IMotelySeedFilterDesc` → `IMotelySeedFilter`; `Filter(ref MotelyVectorSearchContext)` returns a `VectorMask` of which of the 8 lanes passed. `MotelySearch.cs` drives the outer loop.

### JAML (`Motely/Filters/Jaml/`)
`must`/`mustNot` (filters), `should` (scoring)

- Grammar reference: `jaml.schema.jaml`
- Worked example: `index.jaml`
- Parse: `JamlConfigLoader.TryLoad()` → `JamlConfig`
- Compile to runnable plan: `JamlSearchBuilder.CreatePlan()` → `JamlSearchPlan`

### Jimmolate (`Motely/Filters/Native/JimmolateFilterDesc.cs`)

Scalar `MotelyIndividualSeedSearcher => bool` predicate that runs only on seeds that passed the base vector filter.

### JAMLyzer (`Motely/Analysis/`)

Analyzes one seed against a JAML doc, returning what it generates per ante. Entry: `MotelyJamlyzer.AnalyzeSeed()` / `MotelyJamlyzer.Analyze()`.

### Bootsharp docs

Local clone at `D:\bootsharp`. **Read these from disk (not GitHub) before touching `Motely.Wasm/Program.cs` or the interop surface.** Every page is `@`-linked — click it.

@D:\bootsharp\docs\guide\serialization.md
@D:\bootsharp\docs\guide\interop-instances.md
@D:\bootsharp\docs\guide\interop-modules.md
@D:\bootsharp\docs\guide\renaming.md
@D:\bootsharp\docs\guide\declarations.md
@D:\bootsharp\docs\guide\sideloading.md
@D:\bootsharp\docs\guide\llvm.md
@D:\bootsharp\docs\guide\specialization.md
@D:\bootsharp\docs\guide\build-config.md
@D:\bootsharp\docs\guide\getting-started.md
@D:\bootsharp\docs\guide\index.md
@D:\bootsharp\docs\guide\extensions\dependency-injection.md
@D:\bootsharp\docs\guide\extensions\file-system.md

### WASM (`Motely.Wasm/` → `motely-wasm/`)

Compiled to `browser-wasm` via Bootsharp (version `0.8.0`, the public release — released 2026-06-01). Release publishes automatically use NativeAOT-LLVM; debug builds use Mono (faster compile, larger output).

**Module layout** — `BootsharpRenamers` in `Program.cs` folds every `Motely.*` namespace into the `index` module and renames the `Program` node to `Motely`. The root barrel (`index.g.mjs`) is empty — use the package's subpath exports (`./*` → `./dist/generated/modules/*.g.mjs`):

```ts
import bootsharp from "motely-wasm";
import { Program as Motely } from "motely-wasm/motely/wasm";
import * as enums from "motely-wasm/motely/enums";
```

**Sideloading** — `BootsharpBinariesDirectory` is set, so binaries are published as separate files to `dist/bin/` instead of being base64-inlined (~30% smaller bundle). Boot by passing the bytes directly (Node can't `fetch` `file://` URLs):

```ts
const wasm = await readFile(resolve(distDir, "bin", bootsharp.manifest.wasm));
await bootsharp.boot({ wasm });
```

**`[Import]` / `[Export]`** — `Program.cs` is the entire interop surface: exports (C# → JS) use `[Export]`, imports (JS → C#) use `[Import]` and `partial`. `jimmolateProbe` and `reportWasmError` are JS → C# imports that consumers must assign before use.

**`motely-wasm/package.json`** is written by Bootsharp on every publish.

**Bootsharp.FileSystem** (`IFileMounter`, `IFileSystem`, `IFileWatcher`) is a sponsors-only extension. It is already wired in `Program.cs` via DI (`AddBootsharp()` injects the generated implementation). The JS side must call `fs.init(...)` before `bootsharp.boot()`.

### Test fixtures

- `Motely.Tests/GoldenJamlFiles/` — canonical JAML corpus; `JamlCorpusRegressionTests` asserts all files parse clean. Add new fixtures here when adding selectors/modifiers.
- `Motely.Tests/filters/` — per-test JAML files used by specific test classes.
- `JamlFilters/` — user-authored filters at the repo root (not test fixtures).
