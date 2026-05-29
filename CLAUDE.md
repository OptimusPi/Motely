# MotelyJAML

Balatro seed search engine. Filters are written in JAML (a YAML dialect). The engine is C# (`Motely/`); the consumer surface is the `motely-wasm` npm package built by `Motely.Wasm/` via Bootsharp + NativeAOT-LLVM. Engine version lives in `Directory.Packages.props` → `MotelyVersion`. The JAML contract is the TypeScript types in `motely-wasm/dist/`.

## Layout

- `Motely/` — core search engine (C# library).
- `Motely.CLI/`, `Motely.TUI/` — command-line + Terminal.Gui frontends.
- `Motely.Wasm/` — Bootsharp/NativeAOT-LLVM WASM build that produces the JS package. `dotnet publish Motely.Wasm -c Release` is the publish gate.
- `motely-wasm/` — published npm package output. `Motely.Wasm/README.md` is the consumer-facing API doc.
- `Motely.DataLake/` — DuckDB-backed result store.
- `Motely.Tests/` — xunit suite.

## Working with Bootsharp

**Trust Bootsharp. Do not fight it.** Use the documented APIs and MSBuild properties. Do not spelunk Bootsharp source looking for undocumented knobs. The docs below cover every supported configuration point — if it's not in the docs, it's not a feature.

@D:/bootsharp/docs/guide/index.md
@D:/bootsharp/docs/guide/getting-started.md
@D:/bootsharp/docs/guide/build-config.md
@D:/bootsharp/docs/guide/sideloading.md
@D:/bootsharp/docs/guide/serialization.md
@D:/bootsharp/docs/guide/interop-modules.md
@D:/bootsharp/docs/guide/interop-instances.md
@D:/bootsharp/docs/guide/llvm.md
@D:/bootsharp/docs/guide/declarations.md
@D:/bootsharp/docs/guide/preferences.md
@D:/bootsharp/docs/guide/extensions/dependency-injection.md
@D:/bootsharp/docs/guide/extensions/file-system.md

**Local repack chain** (when Elringus pushes new commits to Bootsharp):
```
cd D:/bootsharp && git fetch --all --prune && git reset --hard origin/feat/spec
cd src/js && npm run build
cd ../cs && bash .scripts/pack.sh
dotnet pack D:/extra/bootsharp/cs/Bootsharp.FileSystem/Bootsharp.FileSystem.csproj -c Release -o D:/extra/bootsharp/cs/.nuget
```
- Branch is **`feat/spec`** (was `feat/delegates` before May 28). Force-pushed, so `git pull` would create merge commits — always use `git reset --hard`.
- `Bootsharp.FileSystem` is a sponsor-only extension. Its version is a build-time `yyyy.MM.dd.HHmm` timestamp; read the emitted value from the pack log and bump `Directory.Packages.props` to match.
- Bump all three `Bootsharp.*` pins together + the separate `Bootsharp.FileSystem` timestamp pin.

**Publish gate** (after WASM-facing changes):
```
dotnet publish Motely.Wasm -c Release
node Motely.Wasm/motely.test.mjs     # expect RESULT: PASS
node Motely.Wasm/pack-consumer-smoke.mjs
```

`TreatWarningsAsErrors` is on. Don't suppress; fix.

## How npm package.json is finalized

Bootsharp writes a minimal `package.json` (name, type, exports, browser — see `D:/bootsharp/src/cs/Bootsharp/Build/PackageTemplate.json`). It does not set `version` or `types`. `Motely.Wasm/finalize-package.mjs` is a 20-line Node script invoked from the `FinalizeNpmPackage` MSBuild target that adds those fields plus TS-aware exports. This is load-bearing; don't delete it without replacing it.

## API surface

`Motely.Wasm/Program.cs` is the JS-facing API. JAML enters through `ParseJaml(string yaml) → JamlConfig`. All search/explain/plan/analyze methods take `JamlConfig`, not raw YAML — no double-parsing. Packed-int item decoding is JS-side (jaml-ui owns it); Motely returns raw ints and stops there.
