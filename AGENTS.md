@BOOTSHARP.md
# MotelyJAML

Motely is the Balatro seed-search engine. JAML is its YAML-based filter language. This repo is the engine, CLI, WASM package, tests, and JAML language tooling.

## Note for agents working in this repo

This goes for **every** agent — Claude Code in a Windows 11 terminal, a cloud agent, an IDE extension, whatever you are:

- **Defer to pifreak (the repo owner).** The conventions in this file are deliberate. Don't fight the established setup, don't "fix" things that aren't broken, and don't undo decisions already made by pifreak or in the open PR.
- **Read this whole file before you touch anything.** Most "surprises" are already documented here or in `BOOTSHARP.md`.
- **When agents disagree, the human breaks the tie** — not whichever agent ran last or argued hardest. If the cloud agent's PR and a local change conflict, stop and ask pifreak instead of overwriting each other's work.
- **Listen.** If pifreak says the build works a certain way, it works that way. Verify, don't override.

## Project map

| Project | Purpose | Target |
|---|---|---|1`
| `Motely` | Core engine, JAML parser, analysis, portable runtime | `net10.0` |
| `Motely.CLI` | Command-line searcher | `net10.0` |
| `Motely.Tests` | xUnit + golden/corpus regression | `net10.0` |
| `Motely.Wasm` | Browser build via Bootsharp | `net10.0` + `browser-wasm` |
| `motely-wasm` | Published npm package output | npm |
| `packages/jaml-language-core` | JAML schema helpers | Node |
| `packages/jaml-language-support` | VS Code language support | Node |
| `packages/jaml-mcp` | MCP server: natural-language → JAML | Node |
| `Motely.Run` | Minimal host showing the core search shape | `net10.0` |

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
- **JAML is JAML.** Not YAML. User-facing surfaces and docs say JAML.
- **No facade wrappers.** Export the real Motely public surface from `Motely.Wasm`. Fix the contract in core, do not paper over it in JS.
- **PRNG files are fragile.** `MotelySingleSearchContext.*.cs` and `MotelyVectorSearchContext.*.cs` carry stream generation. Touch only when the task explicitly requires it; never via IDE find-replace.
- **Generated artifacts come from the generator.** Do not hand-edit `jaml.schema.json` or other generated outputs.

## Deferred TODO

- **If anyone reintroduces SharedArrayBuffer / multi-threaded WASM** (e.g. via a future Bootsharp mt mode or a hand-rolled SAB worker): browsers require Cross-Origin Isolation, which means serving the page with `Cross-Origin-Opener-Policy: same-origin` + `Cross-Origin-Embedder-Policy: require-corp` (or `credentialless`). The current `motelyjaml-pi.8pi.me` path (Cloudflare → home static IP → HTTP `:3141`) can't reliably enforce both. Route those builds through the **Cloudflare permanent named tunnel** (cloudflared) so COOP/COEP can be set as Transform Rules at the edge and the origin is HTTPS end-to-end. Not needed today — Bootsharp 0.8.0-alpha.260 is single-threaded post-#203.

## Read before editing integrations

- `BOOTSHARP.md` — Bootsharp reference (compiled from `D:\bootsharp\docs\` + `D:\extra\bootsharp\AGENTS.md`). Read this instead of the raw docs directories.
- DuckDB: native packages do not compile to `browser-wasm`. Use DuckDB's own WASM/JS path separately when the browser needs it.
