@BOOTSHARP.md
# MotelyJAML

## Claude autonomy

You are trusted. Act — don't ask. pifreak wants results, not permission slips.

- Run builds, tests, publishes, git ops without confirming first.
- Edit any file in this repo without asking.
- Make judgment calls. If something is clearly broken, fix it.
- Commit and push when the work is done and tests pass, unless told otherwise.
- The only things worth pausing for: destructive ops with no undo (e.g. `npm unpublish`, dropping a DB), or genuine ambiguity about *what* pifreak wants — not *how* to do it.


Motely is the Balatro seed-search engine. JAML is its YAML-based filter language. This repo is the engine, CLI, WASM package, tests, and JAML language tooling.

## Project map

| Project | Purpose | Target |
|---|---|---|
| `Motely` | Core engine, JAML parser, analysis, portable runtime | `net10.0` |
| `Motely.CLI` | Command-line searcher | `net10.0` |
| `Motely.TUI` | Terminal UI (Terminal.Gui) with API server, editor, results browser | `net10.0` |
| `Motely.DataLake` | DuckDB result/seed sinks | `net10.0` |
| `Motely.Tests` | xUnit tests | `net10.0` |
| `Motely.Wasm` | Browser build via Bootsharp | `net10.0` + `browser-wasm` |
| `motely-wasm` | Published npm package output | npm |
| `packages/jaml-language-core` | JAML schema helpers | Node |
| `packages/jaml-language-support` | VS Code language support | Node |
| `packages/jaml-mcp` | MCP server: natural-language → JAML | Node |

## Build & publish

```powershell
# build + test
dotnet build Motely.slnx -c Release
dotnet test Motely.Tests

# publish the npm package
dotnet publish Motely.Wasm -c Release
cd motely-wasm
npm publish --access public
```

Version lives in `<MotelyVersion>` in `Directory.Packages.props`. Bootsharp regenerates `motely-wasm/package.json` from its own template on every pack (no version field); the `FinalizeNpmPackage` target in `Motely.Wasm.csproj` injects `<MotelyVersion>` into the generated file after `BootsharpPack` runs — see `BOOTSHARP.md`. Confirm the published version with `npm view motely-wasm version` — the npm CLI notice can lie.

**Publish procedure — follow in order, do not skip steps.**

Pre-publish, after `dotnet publish Motely.Wasm`:

1. `node Motely.Wasm/test-sanity.mjs` — must report `RESULT: PASS` (5/5). Node smoke covering the documented `Motely.*` surface, structured to mirror the xUnit shape in `Motely.Tests` (named test functions, arrange/act/assert, runner at bottom). Source of truth for "the package boots and the public API hasn't regressed."
2. Eyeball `motely-wasm/package.json` `exports`. Must be `{ ".": "./dist/index.mjs", "./*": "./dist/generated/*.g.mjs" }`. Known-broken historic shapes: `17.3.1` shipped `"./../motely-wasm/index.mjs"`, `17.3.2` shipped `"././index.mjs"`. Both make Node refuse `import`. Bail before publishing if you see either.
3. `npm publish --dry-run` from `motely-wasm/`. Confirm file count + tarball size are in line with prior releases (47 files / ~2.3 MB at 17.4.x).

Post-publish, against the registry (not the local emit):

4. `npm view motely-wasm@<version> exports` — must match step 2 byte-for-byte. If not, `npm unpublish motely-wasm@<version>` within 72h and republish a bumped patch. Local emit being clean is necessary, not sufficient — the publish pipeline has historically mangled exports on the way to the registry.

`Motely.Wasm/test-browser.html` is the same coverage in-browser; boot path is host-chosen via `?bin=...` query param. Use it when the failure mode is browser-only (OPFS, worker boot, exports resolution against an HTTP server).

`packages/jaml-mcp` pins an exact known-good `motely-wasm` version for the same reason — consumers should not float across the registry until a release has passed step 4.

CDN delivery (unpkg/jsdelivr) is automatic after `npm publish`. No manual upload step.

## Hard rules

- **No private paths in public files.** No `D:\…`, `X:\…`, local NuGet feeds, or personal drive layouts in `.csproj` / `.props` / `.config` / package metadata.
- **Warnings are errors.** Fix the cause.
- **Browser-only stays browser-only.** `Bootsharp.FileSystem` lives in `Motely.Wasm`. Do not leak it into core `Motely`, CLI, or other targets. Do not force native/server packages into `browser-wasm`.
- **No facade wrappers.** Export the real Motely public surface from `Motely.Wasm`. Fix the contract in core, do not paper over it in JS.
- **PRNG changes invalidate every saved seed.** `MotelySingleSearchContext.*.cs` and `MotelyVectorSearchContext.*.cs` carry stream generation; any output change here breaks reproducibility against Balatro. `Motely.Tests` will catch it — run the suite before committing.
- **Generated artifacts come from the generator.** Do not hand-edit `jaml.schema.json` or other generated outputs.

## Read before editing integrations

- `BOOTSHARP.md` — Bootsharp reference (compiled from `D:\bootsharp\docs\` + `D:\extra\bootsharp\AGENTS.md`). Read this instead of the raw docs directories.
- DuckDB: native packages do not compile to `browser-wasm`. Use DuckDB's own WASM/JS path separately when the browser needs it.
