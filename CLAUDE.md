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

Version source of truth: `<MotelyVersion>` in `Directory.Packages.props`. Bootsharp regenerates `motely-wasm/package.json` from its template on every pack (no version field); the `FinalizeNpmPackage` target in `Motely.Wasm.csproj` injects `<MotelyVersion>` into the generated file right after `BootsharpPack` runs and copies `Motely.Wasm/README.md` into the npm root. Confirm the published version with `npm view motely-wasm version` — the npm CLI notice can lie.

## npm publish procedure — follow in order

Pre-publish, after `dotnet publish Motely.Wasm`:

1. **`node Motely.Wasm/motely.test.mjs`** — must report `RESULT: PASS`. Node suite covering the documented `Motely.*` WASM surface, structured to mirror the xUnit shape in `Motely.Tests` (named test functions, arrange/act/assert, runner at bottom). Source of truth for "the package boots and the public API hasn't regressed." Mirror new xUnit cases here when they cover behaviour the JS consumer also depends on.
2. **Eyeball `motely-wasm/package.json` `exports`.** Must be `{ ".": "./dist/index.mjs", "./*": "./dist/generated/*.g.mjs" }`. Known-broken historic shapes: `17.3.1` shipped `"./../motely-wasm/index.mjs"`, `17.3.2` shipped `"././index.mjs"`. Both make Node refuse `import`. Bail before publishing if you see either.
3. **`npm publish --dry-run`** from `motely-wasm/`. Confirm file count + tarball size are in line with prior releases (47 files / ~2.3 MB at 17.4.x).

Post-publish, against the registry (not the local emit):

4. **`npm view motely-wasm@<version> exports`** — must match step 2 byte-for-byte. If not, `npm unpublish motely-wasm@<version>` within 72h and republish a bumped patch. Local emit being clean is necessary, not sufficient — the publish pipeline has historically mangled exports on the way to the registry.

`Motely.Wasm/test-browser.html` is the same coverage in-browser; boot path is host-chosen via `?bin=...` query param. Use it when the failure mode is browser-only (OPFS, worker boot, exports resolution against an HTTP server).

CDN delivery (unpkg/jsdelivr) is automatic after `npm publish` — no manual upload step.

## Agent rules

- **No Bash tool.** Use Read, Grep, Glob, Edit, Write for all file work. Output commands for the user to run via `! <command>` in the prompt — never invoke them yourself.

## Hard rules

- **No private paths in public files.** No `D:\…`, `X:\…`, local NuGet feeds, or personal drive layouts in `.csproj` / `.props` / `.config` / package metadata. `nuget.config` deliberately omits `<clear/>` so per-user feeds merge in without leaking.
- **Warnings are errors.** `TreatWarningsAsErrors` is set in `Directory.Packages.props`. Fix the cause.
- **Browser-only stays browser-only.** `Bootsharp.FileSystem` lives in `Motely.Wasm`. Do not leak it into core `Motely`, CLI, or other targets. Do not force native/server packages (DuckDB, Terminal.Gui) into `browser-wasm`.
- **No facade wrappers.** Export the real Motely public surface from `Motely.Wasm`. Fix the contract in core, do not paper over it in JS.
- **PRNG changes invalidate every saved seed.** `MotelySingleSearchContext.*.cs` and `MotelyVectorSearchContext.*.cs` carry stream generation; any output change here breaks reproducibility against Balatro. `Motely.Tests` will catch it — run the suite before committing.
- **Generated artifacts come from the generator.** Do not hand-edit `jaml.schema.json` or `jaml-schema.cs` — they are emitted by `Motely.CLI/MotelyJAML.schema.generator.cs`. Do not hand-edit anything under `Motely.Wasm/obj/` (Bootsharp output) or `motely-wasm/` (Bootsharp publish root, gitignored).

## Bootsharp source and docs

motely-wasm builds against a **local NuGet feed** of Bootsharp 0.8.0-preview (locally patched). Read these files directly — do not rely on public Bootsharp docs:

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
- **`FinalizeNpmPackage` never sets `SkipUnchangedFiles`** when copying the README. The destination is gitignored and may carry stale manual edits with newer mtimes — skipping would ship the scribble to npm.

## Interop debugging rules

- **Empty `{}` in JS = Bootsharp marshaling, not engine logic.** When a Motely method returns/emits `{}` instead of populated data, the .NET side ran fine — the generated JS bindings dropped fields or serialized to empty silently. Tests that only check "didn't throw" will pass; assert shape (`assert("boss" in seed.analysis.antes[0])`, `assertArray(r.tallies)`) to catch this.
- **Upstream-fix path is real.** The user sponsors Elingus (Bootsharp author) at the $100 tier. When an issue has the shape of a Bootsharp bug, say so — "fix upstream" is on the table, not just "patch locally forever." Don't propose hacky workarounds without flagging this option.
- **No pin comments in tests.** Don't add `// Pins commit abc123`, `// Mirrors xUnit Foo.Bar`, or `// Regression for #42`. Tests assert behavior; they don't cite lineage. ✅ `// long → BigInt; number here means Bootsharp binding regressed.` ❌ `// Mirrors xUnit test — pins commit abc123.`

## Regression fixtures

- `Motely.Tests/filters/` — auto-discovered by `V0FilterRegressionTests`. Drop a `.jaml` here to assert it parses, compiles into a search plan, and list-searches a small probe set without throwing.
- `Motely.Tests/GoldenJamlFiles/` — the parse-stability corpus, asserted by `JamlCorpusRegressionTests`. Add here when a new construct lands.

## Common tasks

- **Add a new JAML clause** — start in `Motely/Filters/` (one descriptor per clause type, e.g. `JokerFilterDesc.cs`), wire it in `JamlClause.cs` / `JamlConfigLoader.*.cs`, regenerate the schema by running `Motely.CLI` schema generation, and add a fixture under `Motely.Tests/filters/` plus a golden file if the syntax is new.
- **Touch the search PRNG** — run `Motely.Tests` end-to-end, not just the changed file. `SearchConsistencyTests` + `V0FilterRegressionTests` are the canary.
- **Bump `MotelyVersion`** — edit `Directory.Packages.props` only. The constant propagates to assembly version, npm `package.json`, and `Motely.version()`.
