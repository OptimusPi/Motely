# MotelyJAML — agent guide

Motely is the Balatro seed-search engine. JAML is its YAML-based filter language. This repo is the engine, CLI, TUI, WASM publish target, and the test suite that keeps them in lockstep.

## Project map

| Project | Purpose | Target |
|---|---|---|
| `Motely` | Core engine, JAML parser, scoring, SIMD search contexts | `net10.0` |
| `Motely.CLI` | Command-line searcher (`MotelyCLI`) | `net10.0`, opt-in AOT |
| `Motely.TUI` | Terminal.Gui frontend (editor, library, results, API server) | `net10.0` |
| `Motely.DataLake` | DuckDB result + seed sinks | `net10.0` |
| `Motely.Wasm` | Bootsharp entry; publishes the npm package | `net10.0` + `browser-wasm` |
| `Motely.Tests` | xUnit tests, golden JAML corpus, regression fixtures | `net10.0` |

`motely-wasm/` is the Bootsharp publish output (gitignored). It is the npm-publish root, not a source project.

## Build, test, publish

```powershell
# build + test
dotnet build Motely.slnx -c Release
dotnet test Motely.Tests

# publish the npm package
dotnet publish Motely.Wasm -c Release
node Motely.Wasm/motely.test.mjs        # must report RESULT: PASS
cd motely-wasm
npm publish --access public
```

Version source of truth: `<MotelyVersion>` in `Directory.Packages.props`. Bootsharp regenerates `motely-wasm/package.json` from its template on every pack (no version field); the `FinalizeNpmPackage` target in `Motely.Wasm.csproj` injects `<MotelyVersion>` into the generated file right after `BootsharpPack` runs and copies `Motely.Wasm/README.md` plus root `jaml.schema.json` into the npm root. Confirm the published version with `npm view motely-wasm version` — the npm CLI notice can lie.

## npm publish procedure

After `dotnet publish Motely.Wasm -c Release`:

1. **`node Motely.Wasm/motely.test.mjs`** — `RESULT: PASS`.
2. **`node Motely.Wasm/pack-consumer-smoke.mjs`** (optional) — `npm pack` → fresh install → import → boot.
3. **Check `motely-wasm/package.json` `exports`:** `{ ".": "./dist/index.mjs", "./*": "./dist/generated/*.g.mjs" }`.
4. **`npm publish --access public`** from `motely-wasm/`.

If a bad version ships, bump `<MotelyVersion>` and publish a fixed patch — do not block fixes on unpublish/rollback rituals.

`Motely.Wasm/test-browser.html` — browser boot via `?bin=...` when debugging host-only issues.

CDN (unpkg/jsdelivr) updates after `npm publish`.

## Agent rules

- **Use PowerShell for commands.** Use the PowerShell tool to run build, test, and publish commands directly. Use Read, Grep, Glob, Edit, Write for all file work — not Bash or shell one-liners for file operations.

## Hard rules

- **No private paths in public files.** No `D:\…`, `X:\…`, local NuGet feeds, or personal drive layouts in `.csproj` / `.props` / `.config` / package metadata. `nuget.config` deliberately omits `<clear/>` so per-user feeds merge in without leaking.
- **Warnings are errors.** `TreatWarningsAsErrors` is set in `Directory.Packages.props`. Fix the cause.
- **Browser-only stays browser-only.** `Bootsharp.FileSystem` lives in `Motely.Wasm`. Do not leak it into core `Motely`, CLI, or other targets. Do not force native/server packages (DuckDB, Terminal.Gui) into `browser-wasm`.
- **No facade wrappers.** Export the real Motely public surface from `Motely.Wasm`. Fix the contract in core, do not paper over it in JS.
- **PRNG changes invalidate every saved seed.** `MotelySingleSearchContext.*.cs` and `MotelyVectorSearchContext.*.cs` carry stream generation; any output change here breaks reproducibility against Balatro. `Motely.Tests` will catch it — run the suite before committing.
- **Generated artifacts come from the generator.** Do not hand-edit `jaml.schema.json` or `jaml-schema.cs` — they are emitted by `Motely.CLI/MotelyJAML.schema.generator.cs`. Do not hand-edit anything under `Motely.Wasm/obj/` (Bootsharp output) or `motely-wasm/` (Bootsharp publish root, gitignored).

## Bootsharp local build

Build pipeline — branch, patch series, version pins, troubleshooting — is documented in `BOOTSHARP-BUILD.md` and automated as one PowerShell script:

```powershell
pwsh ./scripts/build-bootsharp.ps1
```

That's the build. Resets `D:\bootsharp` to upstream, applies `patches/*.patch`, packs core + Bootsharp.FileSystem, bumps pins in `Directory.Packages.props`, publishes `Motely.Wasm`, runs smoke tests. If it doesn't run green, read `BOOTSHARP-BUILD.md` § Troubleshooting before improvising. Never duplicate build prose here — the script and `BOOTSHARP-BUILD.md` are the source of truth.

## Sponsor-gated features

- **`Bootsharp.FileSystem`** — The file system extension (`PickRoot`, `MountRoot`, `UnmountRoot`, `ReadTextFile`, `WriteTextFile`, `OnFileChanges`) is exclusive to [Bootsharp sponsors](https://github.com/sponsors/elringus). The NuGet package (`Bootsharp.FileSystem`) and its sample repo are not publicly available on NuGet or GitHub.

## Things that look weird but aren't

- **`motely-wasm/` lives next to `Motely.Wasm/`, sibling not child.** Intentional — keeping the Bootsharp sinks in separate directories lets `%MODULE_DIR%` resolve to a clean `dist`, so generated `exports` read `./dist/index.mjs` instead of `././index.mjs`. See the `BootsharpPackageDirectory` / `BootsharpPublishDirectory` / `BootsharpBinariesDirectory` props in `Motely.Wasm.csproj`.
- **`Motely.TUI` pulls files from `..\Motely\TUI\*.cs` via globbed `<Compile Include>`.** Not a mistake — the TUI shares those types with the engine.
- **`Motely.Wasm.csproj` generates `MotelyVersion.g.cs` at build time** to bake the version as an IL const. Done because NativeAOT trims `AssemblyInformationalVersionAttribute` reflection, and Motely.csproj disables `GenerateAssemblyInfo` anyway.
- **`FinalizeNpmPackage` never sets `SkipUnchangedFiles`** when copying README and `jaml.schema.json`. The destination is gitignored and may carry stale manual edits with newer mtimes — skipping would ship the scribble to npm.

## Interop debugging rules

- **Empty `{}` in JS = Bootsharp marshaling, not engine logic.** When a Motely method returns/emits `{}` instead of populated data, the .NET side ran fine — the generated JS bindings dropped fields or serialized to empty silently. Tests that only check "didn't throw" will pass; assert shape (`assert("boss" in seed.analysis.antes[0])`, `assertArray(r.tallies)`) to catch this.
- **Upstream-fix path is real.** The user sponsors Elringus (Bootsharp author) at the $100 tier. When an issue has the shape of a Bootsharp bug, say so — fix upstream, not a local workaround forever. Don't propose hacky workarounds without flagging this option.
- **No pin comments in tests.** Comments explain behavior or failure mode only. Forbidden: `Pins commit`, `Mirrors xUnit Class.Method`, `Regression for #N`, `Same probe seeds as xUnit …`. Allowed: `// long must be BigInt — number means binding regressed.` `Motely.Wasm/tests/*.test.mjs` must cover WASM behaviours JS consumers depend on (publish step 1); add tests and behavior comments, not lineage pins.

## Regression fixtures

- `Motely.Tests/filters/` — auto-discovered by `V0FilterRegressionTests`. **Not** repo-root `JamlFilters/` (scratch; no CI). Each `.jaml` must: parse; compile a plan; pass sequential smoke (`Filter_ParsesAndRuns`); have `name` + non-empty `must`; valid deck/stake; must clauses selective on 256 list-search probes; optional inline must/mustNot checks. Drop new community filters here.
- `Motely.Tests/GoldenJamlFiles/` — `JamlCorpusRegressionTests`. Canonical files must parse clean; legacy-key files must fail with `"Unknown property"`. Add canonical files when new syntax lands.

## Common tasks

- **Add a new JAML clause** — follow `docs/JAML-SCHEMA.md` (loader + `PropertyToRef` checklist). Regenerate: `dotnet run --project Motely.CLI/Motely.CLI.csproj -c Release -- schema` or `.\regen-jaml-schema.ps1`. Fixtures in `Motely.Tests/filters/` plus `GoldenJamlFiles/` when syntax is new.
- **Touch the search PRNG** — run `Motely.Tests` end-to-end. Canary: `SearchConsistencyTests`, `V0FilterRegressionTests`; add `JamlCorpusRegressionTests` when parse/schema changes.
- **Bump `MotelyVersion`** — edit `Directory.Packages.props` only. The constant propagates to assembly version, npm `package.json`, and `Motely.version()`.
