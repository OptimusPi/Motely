# MotelyJAML — Bootsharp build & repack

Project overview, repo layout, the publish gate, and the JS API surface are in [CLAUDE.md](CLAUDE.md). This file is the deep reference for building/repacking the **Bootsharp** dependency locally — a sponsor-gated workflow on pifreak's own machine, so the `D:/...` / `X:/...` paths and `@`-imports below are local to that box.

## Bootsharp source and docs

motely-wasm builds against Bootsharp pinned in `Directory.Packages.props` (`Bootsharp`, `Bootsharp.Common`, `Bootsharp.Inject` — all the same version; sponsor `Bootsharp.FileSystem` is versioned separately). Read these files directly — do not rely on public Bootsharp docs:

### Building Bootsharp locally

Source: `D:\bootsharp`. Branch sets the interop ABI: `feat/raw-interop` = NativeAOT-LLVM raw C-ABI (alpha.31x); older alphas use `[JSImport]`/`[JSExport]`.

**Updating to the latest push — it is NOT `git pull`.** Elringus force-pushes / rebases the active branch (currently **`feat/spec`**; was `feat/delegates` before ~May 28 — confirm the current name in CLAUDE.md), so the remote history is rewritten under the same commit message (e.g. "implement delegates support" gets a new hash each push). A `git pull` sees "diverged" and would make a merge commit. Instead:

```
cd D:/bootsharp && git fetch --all --prune && git reset --hard origin/feat/spec
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
@D:/bootsharp/docs/guide/sideloading.md
@D:/bootsharp/docs/guide/serialization.md
@D:/bootsharp/docs/guide/interop-modules.md
@D:/bootsharp/docs/guide/interop-instances.md
@D:/bootsharp/docs/guide/llvm.md
@D:/bootsharp/docs/guide/declarations.md
@D:/bootsharp/docs/guide/preferences.md
@D:/bootsharp/docs/guide/extensions/dependency-injection.md
@D:/bootsharp/docs/guide/extensions/file-system.md

**JS source:**
@D:/bootsharp/src/js/src/exports.mts
@D:/bootsharp/src/js/src/boot.mts
@D:/bootsharp/src/js/src/index.mts

**C# publish source:**
@D:/bootsharp/src/cs/Bootsharp.Publish/

**Sample — minimal (canonical NativeAOT-LLVM boot, no framework):**
@D:/bootsharp/samples/minimal/README.md
@D:/bootsharp/samples/minimal/cs/Minimal.csproj
@D:/bootsharp/samples/minimal/cs/Program.cs
@D:/bootsharp/samples/minimal/main.mjs
@D:/bootsharp/samples/minimal/index.html

**Sample — react (Vite + React app consuming Bootsharp ESM package):**
@D:/bootsharp/samples/react/README.md
@D:/bootsharp/samples/react/package.json
@D:/bootsharp/samples/react/vite.config.ts
@D:/bootsharp/samples/react/tsconfig.json
@D:/bootsharp/samples/react/index.html
@D:/bootsharp/samples/react/src/main.tsx
@D:/bootsharp/samples/react/src/computer.tsx
@D:/bootsharp/samples/react/src/donut.tsx
@D:/bootsharp/samples/react/backend/package.json
@D:/bootsharp/samples/react/backend/Backend.WASM/Backend.WASM.csproj
@D:/bootsharp/samples/react/backend/Backend.WASM/Program.cs
@D:/bootsharp/samples/react/backend/Backend/Backend.csproj
@D:/bootsharp/samples/react/backend/Backend/IComputer.cs
@D:/bootsharp/samples/react/backend/Backend.Prime/Backend.Prime.csproj
@D:/bootsharp/samples/react/backend/Backend.Prime/IPrimeUI.cs
@D:/bootsharp/samples/react/backend/Backend.Prime/Options.cs
@D:/bootsharp/samples/react/backend/Backend.Prime/Prime.cs

**Local patch vs upstream:**
@X:/JammySeedFinder/src/MotelyJAML/bootsharp-fixes-vs-6edaa2c.patch

## Sponsor-gated features

- **`Bootsharp.FileSystem`** — The file system extension (`PickRoot`, `MountRoot`, `UnmountRoot`, `ReadTextFile`, `WriteTextFile`, `OnFileChanges`) is exclusive to [Bootsharp sponsors](https://github.com/sponsors/elringus). The NuGet package (`Bootsharp.FileSystem`) and its sample repo are not publicly available on NuGet or GitHub.
  @D:/bootsharp/docs/guide/extensions/file-system.md