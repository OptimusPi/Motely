# Bootsharp local build pipeline

This is the **only** doc for rebuilding Bootsharp locally. If anything else (CLAUDE.md, agent memory, a comment somewhere) disagrees, this file wins — open a PR to fix the other.

## TL;DR

```powershell
pwsh ./scripts/build-bootsharp.ps1
```

That's the build. The script does everything: resets Bootsharp to upstream, applies the patch series under `patches/`, bumps the alpha, packs core + Bootsharp.FileSystem to their local NuGet feeds, bumps the pins in `Directory.Packages.props`, publishes `Motely.Wasm`, and runs the Node smoke tests. Green or red.

Run `pwsh ./scripts/build-bootsharp.ps1 -WhatIf` first if you want to see every command without executing.

## What the script does (11 steps)

1. **Sanity-check tooling** — git, dotnet, node, npm, bash all on PATH.
2. **Reset `D:\bootsharp` to `origin/feat/spec`** — `fetch --all --prune` then `reset --hard`. Force-pushed upstream, never merge. Refuses to continue if tracked-file changes remain.
3. **Apply `patches/*.patch` in lexical order** — `git apply --check` then `git apply`. Hard failure if a patch doesn't apply — that means it's stale vs. upstream and needs hand-rebasing (which is also the cue to send Elringus a PR).
4. **Bump `<Version>` in `D:\bootsharp\src\cs\Directory.Build.props`** — increments the NNN in `0.8.0-alpha.NNN`. Override with `-AlphaBumpMode none` or `-AlphaBumpMode <explicit-version>`.
5. **Build JS, then pack C#** — `npm --prefix src/js run build`, then `bash src/cs/.scripts/pack.sh`. Output lands in `D:\bootsharp\src\cs\.nuget`.
6. **Pack Bootsharp.FileSystem** — `dotnet pack D:\extra\bootsharp\cs -c Release -o D:\extra\bootsharp\cs\.nuget`. Version is auto-generated from the current timestamp (`yyyy.MM.dd.HHmm`).
7. **Purge the NuGet user cache** for the new alpha + new FileSystem timestamp, so the consumer restore doesn't serve a stale entry.
8. **Bump pins in `Directory.Packages.props`** — XML-edit (preserves formatting) of `Bootsharp`, `Bootsharp.Common`, `Bootsharp.Inject` → new alpha and `Bootsharp.FileSystem` → new timestamp.
9. **`dotnet publish Motely.Wasm -c Release`** — produces `motely-wasm/dist`.
10. **Smoke** — `node Motely.Wasm/motely.test.mjs` and `node Motely.Wasm/getctx-wasm.test.mjs`. Skip with `-SkipSmoke`.
11. **Summary** — versions, feed paths, `RESULT: PASS`.

## Branch and feed setup (one-time)

| Repo | Local path | Branch | Local feed |
|---|---|---|---|
| Bootsharp core | `D:\bootsharp` | `feat/spec` (force-pushed) | `D:\bootsharp\src\cs\.nuget` |
| Bootsharp.FileSystem (sponsor) | `D:\extra\bootsharp` | — (timestamp-versioned) | `D:\extra\bootsharp\cs\.nuget` |

Register both feeds in your **user-level** `NuGet.Config` (`%APPDATA%\NuGet\NuGet.Config` on Windows). The repo's `nuget.config` deliberately omits `<clear/>` so these merge in:

```xml
<configuration>
  <packageSources>
    <add key="bootsharp-local"      value="D:\bootsharp\src\cs\.nuget" />
    <add key="bootsharp-filesystem" value="D:\extra\bootsharp\cs\.nuget" />
  </packageSources>
</configuration>
```

Override the paths if your checkouts live elsewhere:

```powershell
pwsh ./scripts/build-bootsharp.ps1 `
    -BootsharpRoot   C:\src\bootsharp `
    -BootsharpFsRoot C:\src\bootsharp-filesystem
```

## Patch series

The series lives under `patches/` and is the canonical record of every Bootsharp delta Motely needs that hasn't landed upstream yet. Two files today:

### `patches/01-projectability.patch`

Status today: **not yet vendored** — exists as a placeholder (`patches/01-projectability.patch.PLACEHOLDER`) until the maintainer exports the local stash. Until that's done, `dotnet publish Motely.Wasm` will fail when the inspector hits a non-projectable interop member.

Touches: `src/cs/Bootsharp.Common/Global/GlobalType.cs`, `src/cs/Bootsharp.Common/Inspection/TypeInspector.cs`.

Effect: skips non-projectable interop members (ref structs, byref `T&`, delegates, bare `IEnumerable<T>`) and drops imported interfaces that have any such member.

Smallest test it must pass: `dotnet publish Motely.Wasm -c Release` succeeds. Failure mode without it: the publish-time inspector throws on Motely's `MotelySingleSearchContext` and friends.

One-time export (run on the machine that has the stash):

```powershell
cd D:\bootsharp
git stash list   # find "On feat/spec: !!GitHub_Desktop<feat/spec>"
git stash show -p stash@{N} `
    > X:\JammySeedFinder\src\MotelyJAML\patches\01-projectability.patch
# then delete patches/01-projectability.patch.PLACEHOLDER
```

### `patches/02-publish-fixes.patch`

Three-commit series against `Bootsharp.Publish`. Without it the package doesn't even compile against Motely's surface.

- **Commit 1** — `TypeDeclarationGenerator.cs`: migrate to the post-`6edaa2c` `SolutionInspection.Surfaces` / `.Types` split. Members now live on `InstanceMeta` (the surface), not `InstancedMeta` (the type).
- **Commit 2** — `InstanceGenerator.cs`: qualify the runtime root with `global::Bootsharp.*` everywhere in the emit template (otherwise the generated wrapper namespace `Bootsharp.Generated.Imports.Bootsharp.FileSystem` shadows the root); rename `JSImported` → `JSProxy` to match the runtime API.
- **Commit 3** — `SurfaceInspector.cs`: unwrap `Task<T>` before instance inspection. Mirrors `SerializedInspector`. Without it the inspector recurses into `Task<T>`'s public methods and emits invalid `JSConfiguredTaskAwaitable\`1` proxies.

Smallest tests it must pass:

- `dotnet build` on `D:\bootsharp\src\cs\Bootsharp.Publish` succeeds (commit 1 unblocks compile).
- The Bootsharp E2E suite (`npm --prefix src/js run compile-test`) emits no class containing a backtick in its name (commit 3).
- `dotnet publish Motely.Wasm -c Release` succeeds and `node Motely.Wasm/motely.test.mjs` exits 0 (commit 2).

## Troubleshooting

| Error you'll actually see | Cause | Fix |
|---|---|---|
| `CS0234: 'JSImported' does not exist in the namespace 'Bootsharp.Generated.Imports.Bootsharp'` | `patches/02-publish-fixes.patch` not applied | Re-run script; check step 3 log for the `--check` failure |
| Generated emit contains `JSConfiguredTaskAwaitable\`1` | `patches/02` partial apply (commit 3 missing) | Re-apply the whole series (delete `D:\bootsharp` working changes and re-run) |
| `Bootsharp.Common` resolves to a stale alpha | NuGet cache hit | Re-run the script (step 7 purges); or manually `Remove-Item -Recurse $env:USERPROFILE\.nuget\packages\bootsharp*` |
| `NETSDK1083: The specified RuntimeIdentifier 'browser-wasm' is not recognized` | Missing wasm-tools workload | `dotnet workload install wasm-tools` |
| `node motely.test.mjs` prints `{}` instead of populated data | Bootsharp marshaling regression (see CLAUDE.md "Interop debugging rules") | Assert shape, not just "didn't throw"; check that every `[Export]`-ed type in `Program.cs` is projectable |
| The publish-time Bootsharp inspector throws on a `ref struct` / delegate / bare `IEnumerable<T>` | `patches/01-projectability.patch` not yet vendored | Export the stash (see § Patch series) |
| `Bootsharp.FileSystem` package not found at restore | `D:\extra\bootsharp\cs\.nuget` not registered as a feed | Add it to the user-level `NuGet.Config` (see § Branch and feed setup) |

## Upstreaming

The maintainer sponsors Elringus at the $100 tier. When `patches/02-publish-fixes.patch` rebases cleanly against `feat/spec` HEAD and the smoke tests pass, send Elringus a PR for the three commits — the patch file's commit messages and author trailers are already in the right shape (`git am patches/02-publish-fixes.patch` will replay them). Same for `01-projectability.patch` once it's vendored.

When a patch lands upstream, delete the file from `patches/`. The script applies whatever's there — none means none.

## NuGet cache gotcha (already handled by step 7)

Reusing a version number you've already restored once (e.g., you re-ran the script with `-AlphaBumpMode none`) makes NuGet serve the cached old package. The script's step 7 purges these eagerly, but if you ever hit the symptom in a manual run:

```powershell
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\bootsharp\<version>"
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\bootsharp.common\<version>"
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\bootsharp.inject\<version>"
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\bootsharp.filesystem\<timestamp>"
```
