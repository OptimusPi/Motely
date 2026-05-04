# Bootsharp Local Build → MotelyJAML Install

When upstream Bootsharp `main` has changes you need locally before they're
published to NuGet (e.g. tracking a merged PR ahead of a stable cut).

## Prerequisites

- Fresh clone of upstream Bootsharp at `D:\bootsharp`
- Bootsharp.FileSystem source at `D:\extra\bootsharp`
- Git Bash at `C:\Program Files\Git\bin\bash.exe` (NOT WSL bash — pulls Windows
  paths into the Linux filesystem view and breaks the scripts)

> **Bash exception, Comrade:** the global "no bash on Windows, use PowerShell"
> rule is **suspended for this doc only**. Bootsharp's `llvm.sh`, `build.sh`,
> and `pack.sh` are bash-only and *require* Git Bash on Windows. Do NOT try to
> port them to PowerShell.
- User-level local NuGet feed at `%LOCALAPPDATA%\Temp\bootsharp-local`,
  registered as source name `bootsharp-local` in `%APPDATA%\NuGet\NuGet.Config`

## The build chain (in order)

### 1. Build Bootsharp

```pwsh
$bash = 'C:\Program Files\Git\bin\bash.exe'
cd D:\bootsharp\src\cs
& $bash ./.scripts/llvm.sh         # one-time per fresh clone — fetches NativeAOT-LLVM into .llvm/
cd ..\js
& $bash scripts/build.sh           # rebuild JS dist (instances.ts/imports.ts → dist/)
cd ..\cs
& $bash ./.scripts/pack.sh         # produces .nupkg in .nuget/
```

Output: `D:\bootsharp\src\cs\.nuget\Bootsharp.<version>.nupkg` (plus
`Bootsharp.Common`, `Bootsharp.Inject`).

### 2. Build Bootsharp.FileSystem

```pwsh
cd D:\extra\bootsharp\cs
dotnet pack Bootsharp.FileSystem -c Release -o .nuget
```

Output: `D:\extra\bootsharp\cs\.nuget\Bootsharp.FileSystem.<yyyy.MM.dd.HHmm>.nupkg`
(the `.csproj` time-stamps the version automatically).

### 3. Confirm NEW versions

Each `.nupkg` must have a version higher than what's currently in
`MotelyJAML/Directory.Packages.props`. Otherwise the NuGet global cache will
reuse the stale extracted package and the local rebuild is a silent no-op.

```pwsh
Get-ChildItem D:\bootsharp\src\cs\.nuget\Bootsharp*.nupkg |
  Sort-Object LastWriteTime -Descending | Select-Object -First 5
Get-ChildItem D:\extra\bootsharp\cs\.nuget\Bootsharp.FileSystem*.nupkg |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1
```

### 4. Stage to local feed

```pwsh
$feed = "$env:LOCALAPPDATA\Temp\bootsharp-local"
New-Item -ItemType Directory -Force -Path $feed | Out-Null
Copy-Item D:\bootsharp\src\cs\.nuget\Bootsharp*.nupkg $feed -Force
Copy-Item D:\extra\bootsharp\cs\.nuget\Bootsharp.FileSystem*.nupkg $feed -Force
```

### 5. Install in MotelyJAML

Bump versions in `Directory.Packages.props`:

```xml
<PackageVersion Include="Bootsharp" Version="<new-version>" />
<PackageVersion Include="Bootsharp.Inject" Version="<new-version>" />
<PackageVersion Include="Bootsharp.FileSystem" Version="<new-fs-version>" />
```

The committed `MotelyJAML/nuget.config` ships with `<clear />` + `nuget.org`
only (deliberate — "no private machine paths in public files"). To make the
local feed visible for the restore, edit `nuget.config` *uncommitted* per the
AGENTS.md "Making the local feed actually visible to a restore" template, then
revert before commit.

```pwsh
cd X:\JammySeedFinder\src\MotelyJAML
dotnet restore Motely.sln
```

### 6. Smoke test

```pwsh
dotnet build Motely.Wasm\Motely.Wasm.csproj -c Release
```

If the build fails at AOT analysis or rollup with `X is not exported by
instances.js` or new `IL3050`/`IL2104`/`IL3053` errors, the consumer
(`Motely.Wasm/Program.cs`) is out of sync with upstream renames — fix the
consumer to match upstream, never patch upstream.

## When to re-run this chain

- Upstream Bootsharp `main` advanced past your local feed
- Source changed in `src/js/src/instances.ts`, `imports.ts`, or attribute renames
- A new `0.8.0-alpha.<n>` PR merged that you need to track
- Bootsharp.FileSystem source changed (rare — pifreak edits these)

## What this doc does NOT cover

- `MotelyVersion` bumps (separate from Bootsharp version) — `Directory.Packages.props` line 4
- Schema regeneration — see `AGENTS.md` "JAML schema rules"
- motely-wasm npm publish — see `AGENTS.md` "Release process"
- Upstream Bootsharp source edits — pifreak only
