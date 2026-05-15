# Bootsharp integration reference

`@`-imported by `CLAUDE.md` / `AGENTS.md`. Read this before touching anything that
crosses the C#↔JS boundary or the `Motely.Wasm` publish pipeline.

Bootsharp is the C#↔JS interop + WebAssembly packaging toolchain. `Motely.Wasm` uses
it to compile the core engine to `browser-wasm` and emit the `motely-wasm` npm package.

## The two sources

Both are consumed as NuGet packages from **local** `.nuget` feeds — they are not on
nuget.org.

| Source | Repo | Builds | Consumed as |
|---|---|---|---|
| **Core** | `D:\bootsharp` — fork (origin `OptimusPi/bootsharp`, upstream `elringus/bootsharp`), branch `feat/es-modules` | `Bootsharp`, `Bootsharp.Common`, `Bootsharp.Inject` | `0.8.0-alpha.268` |
| **Extra** | `D:\extra\bootsharp` — the "Extra" companion repo | `Bootsharp.FileSystem` (browser OPFS file-system extension) | `2026.5.14.1139` |

Versions are pinned in `Directory.Packages.props` (`<PackageVersion>` entries). Bump them
there after packing a new alpha.

## NuGet source resolution

- Per-user `%APPDATA%\NuGet\NuGet.Config` (Windows) adds the local feeds:
  - `bootsharp-local` → `D:\bootsharp\src\cs\.nuget`
  - `bootsharp-filesystem` → `D:\extra\bootsharp\cs\.nuget`
- The repo-local `nuget.config` is intentionally minimal — only `nuget.org`, no `<clear/>`
  — so the user-level feeds merge in. **No `D:\` paths are committed** (hard rule).
- If the local feeds are not configured, restore falls back to nuget.org and pulls a
  stale public Bootsharp that predates `[Import]` — the build fails with
  `error CS0246: 'ImportAttribute' could not be found`. That error means "check your
  NuGet sources", not "the code is broken".

Run all Bootsharp `.sh` scripts with `bash` (they are bash by design). Everything in
*this* repo uses PowerShell.

## Packing the alpha — core (`D:\bootsharp`)

Run from `D:\bootsharp\src\cs`:

1. **One-time / on version change:** `bash .scripts/llvm.sh` — downloads the
   NativeAOT-LLVM compiler artifacts into `.llvm/`. `pack.sh` refuses to run without them.
2. If C#/JS sources changed, bump `<Version>` in `src/cs/Directory.Build.props`
   (`0.8.0-alpha.N` → `0.8.0-alpha.N+1`).
3. `bash .scripts/pack.sh` — builds `Bootsharp.Generate`, then
   `dotnet pack` of `Bootsharp.Common`, `Bootsharp.Inject`, `Bootsharp` into
   `src/cs/.nuget/`, then `dotnet restore`.
4. Optional full validation: under `src/js`, `npm run build` → `npm run compile-test`
   → `npm run test` (the JS E2E suite). Run in that order, do not parallelize.

## Packing the alpha — extra (`D:\extra\bootsharp`)

`Bootsharp.FileSystem` references `Bootsharp.Common` with a floating `*-*` version, so
**the core must be packed first** — the new alpha has to be visible before this packs.
Its `<Version>` is auto-stamped `yyyy.MM.dd.HHmm` at pack time; no manual bump.

Local pack (from `D:\extra\bootsharp`):

```bash
rm -rf cs/Bootsharp.FileSystem/{bin,obj}
rm -f  cs/.nuget/*.nupkg
dotnet pack cs/Bootsharp.FileSystem/Bootsharp.FileSystem.csproj \
  -c Release -o cs/.nuget \
  --source 'https://api.nuget.org/v3/index.json' \
  --source 'D:\bootsharp\src\cs\.nuget'
```

Output: `cs/.nuget/Bootsharp.FileSystem.<yyyy.MM.dd.HHmm>.nupkg`. Warnings `NU5104`
(stable package → prerelease dependency) and "missing readme" are expected.

`cs/.scripts/publish.sh` is **destructive** — it packs *and* `dotnet nuget push`es to a
private feed. Do not run it for a local refresh.

After packing, update `Bootsharp.FileSystem`'s pinned version in this repo's
`Directory.Packages.props`.

## The `Bootsharp.targets` output-sink mechanism

`Bootsharp.targets` ships *inside* the `Bootsharp` nupkg. Its `BootsharpPack` target runs
at **publish time only** (`dotnet publish`, not `dotnet build`), after the WASM app is
built. It writes to **three independent sinks**:

| Property | Default | Receives |
|---|---|---|
| `BootsharpPublishDirectory` | `bin/<BootsharpName>` | ES module (`index.mjs`), `generated/`, `dotnet/`, `.d.mts` declarations |
| `BootsharpBinariesDirectory` | `<PublishDirectory>/bin` | `dotnet.native.wasm` (+ ICU `.dat` *only* if globalization is not invariant) |
| `BootsharpPackageDirectory` | project directory | the generated `package.json` |

Behaviors that dictate how you wire these:

- **`BootsharpBinariesDirectory` is `RemoveDir`'d on every pack.** It must be disposable —
  never point it at a directory you hand-maintain.
- **`package.json` is regenerated every pack with `Overwrite="true"`** from Bootsharp's
  `PackageTemplate.json`. You *cannot* hand-maintain a `package.json` in
  `BootsharpPackageDirectory` — it is destroyed and rewritten each build. The alpha.268
  template emits `{ name, type, exports, browser }` only — **no `version`, no `scripts`**.
- `%MODULE_DIR%` in the template resolves to the relative path from
  `BootsharpPackageDirectory` to `BootsharpPublishDirectory`. If those two are the same
  directory, `%MODULE_DIR%` is `.` and exports come out as the ugly `"././index.mjs"`.
  Keep the sinks in distinct directories.
- ICU `.dat` files only exist when `InvariantGlobalization` is false. Bootsharp's
  `Bootsharp.props` defaults it to `true` (bundle-size reduction), so `dist/bin/` here
  contains only `dotnet.native.wasm` — that is correct, not a missing artifact.

## How `Motely.Wasm.csproj` wires it

`motely-wasm/` — sibling of `Motely.Wasm/`, **gitignored** — is the npm-publish root.
The three sinks are set to distinct directories so `%MODULE_DIR%` resolves cleanly:

| Sink | Value | Result |
|---|---|---|
| `BootsharpPackageDirectory` | `motely-wasm/` | generated `package.json` at the npm root |
| `BootsharpPublishDirectory` | `motely-wasm/dist/` | module + `generated/` + `dotnet/`; `%MODULE_DIR%` = `dist` → clean `"./dist/index.mjs"` exports |
| `BootsharpBinariesDirectory` | `motely-wasm/bin/` | `dotnet.native.wasm`; disposable (`RemoveDir`'d each pack) |

`BootsharpBinariesDirectory` is a **package-root sibling of `dist/`, not under it.** The
`.wasm` is not a module import — `boot(resourcesUrl)` fetches it from a URL root, and
both Bootsharp's `boot()` JSDoc (`eg, /bin`) and downstream consumers (jaml-ui's
`BOOT_ROOT_CANDIDATES` includes `/node_modules/motely-wasm/bin`) expect that root at
`<package>/bin`. Putting it under `dist/` would break those consumers. Everything else
in `dist/` (`dotnet/*.js`, `generated/`) is module-relative and moves transparently.

### Version injection — the `FinalizeNpmPackage` target

Bootsharp's generated `package.json` has no `version`, but `motely-wasm` is npm-published
and needs one. The `FinalizeNpmPackage` target (`AfterTargets="BootsharpPack"`):

1. Reads the just-written `motely-wasm/package.json`.
2. Injects `<MotelyVersion>` (source of truth: `Directory.Packages.props`) after the
   `name` line.
3. Writes it back, and copies `Motely.Wasm/README.md` into the npm root (Bootsharp does
   not copy a README).
4. A hard `<Error>` fires if the injection no-ops — e.g. a future Bootsharp template
   change moves the `name` line. A missing version **fails the build** rather than
   shipping silently.

Net effect: the documented `dotnet publish Motely.Wasm -c Release` produces a complete,
versioned npm package in **one command**. There is no separate `sync-version` script —
any `scripts` block would be wiped by Bootsharp's `Overwrite="true"` anyway.

## `clean.ps1`

Repo-root script (`clean.ps1`). Run before a fresh publish so stale Bootsharp output does
not fold into the new package.

| Invocation | Effect |
|---|---|
| `.\clean.ps1` | `dotnet clean` the solution; remove every `bin/` and `obj/`; remove `motely-wasm/` |
| `.\clean.ps1 -KeepWasm` | as above but leave `motely-wasm/` in place |
| `.\clean.ps1 -KeepBinObj` | as above but leave `bin/`+`obj/` in place |

## Threading

Bootsharp `0.8.0-alpha.268` is single-threaded (post upstream #203). If multi-threaded
WASM is ever reintroduced, see the COOP/COEP "Deferred TODO" in `AGENTS.md` — it requires
Cross-Origin Isolation at the edge.
