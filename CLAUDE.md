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

## Bootsharp source and docs

motely-wasm builds against Bootsharp pinned in `Directory.Packages.props` (`Bootsharp`, `Bootsharp.Common`, `Bootsharp.Inject` — all the same version; sponsor `Bootsharp.FileSystem` is versioned separately). Read these files directly — do not rely on public Bootsharp docs:

### Building Bootsharp locally

Source: `D:\bootsharp`. Branch sets the interop ABI: `feat/raw-interop` = NativeAOT-LLVM raw C-ABI (alpha.31x); older alphas use `[JSImport]`/`[JSExport]`.

**Updating to the latest push — it is NOT `git pull`.** Elringus force-pushes / rebases `feat/delegates`, so the remote history is rewritten under the same commit message (e.g. "implement delegates support" gets a new hash each push). A `git pull` sees "diverged" and would make a merge commit. Instead:

```
cd D:/bootsharp && git fetch --all --prune && git reset --hard origin/feat/delegates
```

This discards the local pointer and lands directly on his rewritten commit — linear, no merge commit. (Working tree is normally clean here, so nothing is lost.)

Repack (from `D:\bootsharp\AGENTS.md`, in order):

1. Once: `bash src/cs/.scripts/llvm.sh` (downloads NativeAOT-LLVM to `src/cs/.llvm`).
2. `cd src/js && npm run build`.
3. Bump `<Version>` in `src/cs/Directory.Build.props` (only if sources changed).
4. `cd src/cs && bash .scripts/pack.sh` (packs to `src/cs/.nuget`).

**Then rebuild the sponsor FileSystem extension** — `Bootsharp.FileSystem` pins `Bootsharp.Common` as `*-*` (floats to local latest), so whenever you repack `Bootsharp.Common` you must repack FileSystem against it or the consumer restores a FileSystem built against a stale Common:

5. `dotnet pack D:/extra/bootsharp/cs/Bootsharp.FileSystem/Bootsharp.FileSystem.csproj -c Release -o D:/extra/bootsharp/cs/.nuget` (packs the C# NuGet to the extra feed). `D:/extra/bootsharp/scripts/package.sh` is the separate JS-side build — it bundles the TypeScript and runs `npm publish` of `@rewaffle/bootsharp-file-system` to the GitHub registry; it is not part of the local C# NuGet loop.

`Bootsharp.FileSystem` has no `<Version>`; it stamps a build-time timestamp `yyyy.MM.dd.HHmm` (NuGet normalizes to e.g. `2026.5.22.1237`). Read the actual emitted version from the pack log — that's the pin to use below.

Local feeds are user-level (`%APPDATA%\NuGet\NuGet.Config`), not committed: `bootsharp-local` → `D:\bootsharp\src\cs\.nuget`; `Bootsharp.FileSystem` feed → `D:\extra\bootsharp\cs\.nuget`.

Consume here: bump all three `Bootsharp*` versions in `Directory.Packages.props` together, **and** bump `Bootsharp.FileSystem` to the timestamp from step 5 (it tracks the same Common, just versioned separately). Validate with `dotnet publish Motely.Wasm -c Release` then `node Motely.Wasm/motely.test.mjs` (`RESULT: PASS`) — Bootsharp's own E2E suite passing does not prove Motely's types generate valid bindings.

**Docs:**
@D:/bootsharp/docs/guide/index.md
@D:/bootsharp/docs/guide/getting-started.md
@D:/bootsharp/docs/guide/build-config.md
@D:/bootsharp/docs/guide/events.md
@D:/bootsharp/docs/guide/serialization.md
@D:/bootsharp/docs/guide/interop-modules.md
@D:/bootsharp/docs/guide/interop-instances.md
@D:/bootsharp/docs/guide/llvm.md
@D:/bootsharp/docs/guide/nullability.md
@D:/bootsharp/docs/guide/declarations.md
@D:/bootsharp/docs/guide/namespaces.md
@D:/bootsharp/docs/guide/preferences.md
@D:/bootsharp/docs/guide/extensions/dependency-injection.md
@D:/bootsharp/docs/guide/extensions/file-system.md

**JS source:**
@D:/bootsharp/src/js/src/exports.mts
@D:/bootsharp/src/js/src/boot.mts
@D:/bootsharp/src/js/src/index.mts

**C# publish source:**
@D:/bootsharp/src/cs/Bootsharp.Publish/

**Local patch vs upstream:**
@X:/JammySeedFinder/src/MotelyJAML/bootsharp-fixes-vs-6edaa2c.patch

## Sponsor-gated features

- **`Bootsharp.FileSystem`** — The file system extension (`PickRoot`, `MountRoot`, `UnmountRoot`, `ReadTextFile`, `WriteTextFile`, `OnFileChanges`) is exclusive to [Bootsharp sponsors](https://github.com/sponsors/elringus). The NuGet package (`Bootsharp.FileSystem`) and its sample repo are not publicly available on NuGet or GitHub.
  @D:/bootsharp/docs/guide/extensions/file-system.md

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
