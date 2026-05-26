# MotelyJAML — agent guide

This submodule is the **Motely** seed-search engine plus the **JAML** filter DSL. The parent app (BalatroSeedOracle) embeds it; the browser head consumes a WASM build packaged by **Bootsharp**.

The Bootsharp docs below are imported in full. Read them; do not paraphrase from memory.

## Bootsharp upstream — full doc set

Upstream lives at `D:\bootsharp` on branch `feat/spec` (force-pushed, never merged). Sponsor extension `Bootsharp.FileSystem` lives at `D:\extra\bootsharp` (no docs of its own; spec is in the file-system guide below).

### Agent + project meta
@D:/bootsharp/AGENTS.md
@D:/bootsharp/README.md
@D:/bootsharp/PLAN.md

### User guide
@D:/bootsharp/docs/guide/index.md
@D:/bootsharp/docs/guide/getting-started.md
@D:/bootsharp/docs/guide/build-config.md
@D:/bootsharp/docs/guide/declarations.md
@D:/bootsharp/docs/guide/serialization.md
@D:/bootsharp/docs/guide/interop-modules.md
@D:/bootsharp/docs/guide/interop-instances.md
@D:/bootsharp/docs/guide/preferences.md
@D:/bootsharp/docs/guide/sideloading.md
@D:/bootsharp/docs/guide/llvm.md

### Extensions
@D:/bootsharp/docs/guide/extensions/dependency-injection.md
@D:/bootsharp/docs/guide/extensions/file-system.md

## Load-bearing facts (cross-reference, not restate)

- **Export vs Import is C#-centric.** `[Export]` = C# → JS surface; `[Import]` = JS → C# surface. Same vocabulary in both languages. See `AGENTS.md`.
- **Module interfaces.** `[assembly: Export(typeof(IBackend))]` / `[assembly: Import(typeof(IFrontend))]` — Bootsharp generates the binding. Imported modules **must** be interfaces. Exported can be interface or concrete class. See `interop-modules.md`.
- **Mutable types pass by reference** (interop instances), immutable types (struct/record/read-only collections) pass by value (binary serializer). BCL types are never bound as instances — that would leak the runtime. See `interop-instances.md` and `serialization.md`.
- **Release publish ⇒ NativeAOT-LLVM + Binaryen.** `dotnet publish -c Release` auto-enables LLVM backend + speed-tuned codegen + trimming. Install Binaryen and put `wasm-opt` on PATH or you'll get an unoptimized binary + warning. See `llvm.md`.
- **Globalization is off by default.** `<InvariantGlobalization>false</InvariantGlobalization>` to opt in; `WasmIncludeFullIcuData` for full ICU. See `build-config.md`.
- **Sideloading.** Default behavior base64-embeds the WASM + assemblies (~30% bundle bloat). Set `BootsharpBinariesDirectory` to externalize, then `boot("/url")` or pass `BootResources`. See `sideloading.md`.
- **DI integration.** `services.AddBootsharp()...BuildServiceProvider().RunBootsharp()` — the first call injects generated imports, the second initializes exports. See `dependency-injection.md`.
- **Preferences are regex pair arrays.** `Space` / `Name` / `Method` / `Property` / `Event` — `(pattern, replacement)` fed to `Regex.Replace`, evaluated in order. See `preferences.md`.

## NuGet — where the Bootsharp packages come from

`Directory.Packages.props` pins these (central package management is on):

- `Bootsharp`, `Bootsharp.Common`, `Bootsharp.Inject` → `0.8.0-alpha.NNN`
- `Bootsharp.FileSystem` → `YYYY.MM.DD.HHmm` timestamp

These versions are **not on nuget.org**. They are served from local feeds produced by the upstream build:

| Package(s) | Source feed | Built by |
|---|---|---|
| `Bootsharp*` (core + Inject + Common) | `D:\bootsharp\src\cs\.nuget` | `bash D:\bootsharp\src\cs\.scripts\pack.sh` |
| `Bootsharp.FileSystem` | `D:\extra\bootsharp\cs\.nuget` | `dotnet pack D:\extra\bootsharp\cs -c Release -o D:\extra\bootsharp\cs\.nuget` |

### Wiring the feeds — user-level NuGet.Config

This repo has **no `nuget.config`** (deleted with the rest of the dead WASM scaffolding). Both feeds must be registered in the user-level config: `%APPDATA%\NuGet\NuGet.Config`.

```xml
<configuration>
  <packageSources>
    <add key="bootsharp-local"      value="D:\bootsharp\src\cs\.nuget" />
    <add key="bootsharp-filesystem" value="D:\extra\bootsharp\cs\.nuget" />
  </packageSources>
</configuration>
```

Do not add `<clear/>` — nuget.org is still needed for `Microsoft.*`, `DuckDB.NET.*`, `Terminal.Gui`, etc.

### Bumping versions

Bumping `Bootsharp` alpha:
1. Rebuild core: `npm run build` under `D:\bootsharp\src\js`, then `bash D:\bootsharp\src\cs\.scripts\pack.sh`.
2. Bump `<Version>` in `D:\bootsharp\src\cs\Directory.Build.props` if you packed a new alpha.
3. Update the three pinned versions in `Directory.Packages.props` (`Bootsharp`, `Bootsharp.Common`, `Bootsharp.Inject`) to match.

Bumping `Bootsharp.FileSystem`:
1. `dotnet pack D:\extra\bootsharp\cs -c Release -o D:\extra\bootsharp\cs\.nuget`. Version becomes the current `yyyy.MM.dd.HHmm`.
2. Update the pin in `Directory.Packages.props`.

### Cache gotcha

NuGet keeps `%USERPROFILE%\.nuget\packages\<pkg>\<version>` forever. Reusing a version number (re-packing the same alpha/timestamp) serves the **old cached copy**, not the new pack. Purge before reuse:

```powershell
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\bootsharp\<version>"
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\bootsharp.common\<version>"
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\bootsharp.inject\<version>"
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\bootsharp.filesystem\<timestamp>"
```

The cleanest habit is to never reuse a version — always bump.

## Packaging Bootsharp (when modifying upstream)

Order is non-negotiable (per `AGENTS.md`):

1. `npm run build` under `src/js`
2. Bump alpha in `src/cs/Directory.Build.props` (`0.8.0-alpha.N` → `0.8.0-alpha.N+1`)
3. `bash src/cs/.scripts/pack.sh` under `src/cs`
4. `npm run compile-test` under `src/js`
5. `npm run test` under `src/js`

Coverage policy is **100% C# and JS**, branch coverage included. Generated-output dumps from failing publish tests land at `src/cs/Bootsharp.Publish.Test/last-failed-test-dump.txt`.

## Local patch stance

Upstream `feat/spec` is the source of truth. Any local-only deltas vendored against it should:
- live as patch files applied in lexical order on top of an `origin/feat/spec` reset
- target the smallest possible surface
- be sent upstream once they rebase cleanly + green smoke

When a patch lands upstream, drop it.

## Things that bite

- **`Bootsharp.Common` resolving to a stale alpha** — NuGet cache hit on a reused version. Purge `%USERPROFILE%\.nuget\packages\bootsharp*` for the affected version.
- **`NETSDK1083: 'browser-wasm' not recognized`** — `dotnet workload install wasm-tools`.
- **Generated emit contains a backtick (`` ` ``) in a class name** — `Task<T>` was inspected as an instance instead of unwrapped. Surface-inspector bug; check `SurfaceInspector` against `SerializedInspector`.
