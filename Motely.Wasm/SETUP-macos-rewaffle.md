# macOS — Bootsharp.FileSystem + @rewaffle (the real gate)

**Shape (already in the tree):**

| Build | Package ref | `MotelyFileSystem` exports |
|-------|-------------|----------------------------|
| default | no `Bootsharp.FileSystem` | off (`#if BOOTSHARP_FILESYSTEM`) |
| `-p:EnableFileSystem=true` | sponsor nupkg required | on |

**npm:** `@rewaffle/bootsharp-file-system` is an **optional peer** only. Public `npm install motely-wasm` must not 404. Sponsors install the peer separately.

---

## A. Engine only (no folder picker) — works without sponsor feed

```sh
cd Motely.Wasm
npm install                  # no rewaffle required
dotnet publish Motely.Wasm.csproj -c Debug
# or: npm run build:debug
```

---

## B. Folder picker on (your machine has the feed)

### B1. NuGet — get `Bootsharp.FileSystem` on this Mac

Pick **one**:

**Local pack** (common when you have `rewaffle/extra` or Bootsharp sponsor sources):

```sh
mkdir -p ~/.nuget/local-feed
dotnet nuget add source "$HOME/.nuget/local-feed" --name bootsharp-local

# from wherever the FileSystem csproj lives, e.g. rewaffle/extra:
dotnet pack path/to/Bootsharp.FileSystem.csproj -o ~/.nuget/local-feed

# pin in Directory.Packages.props must match the packed version
dotnet restore Motely.Wasm/Motely.Wasm.csproj -p:EnableFileSystem=true
```

**GitHub Packages** (if your sponsor access is the rewaffle org feed):

```sh
# PAT: read:packages — keep in the shell / Keychain, not the repo
export NODE_AUTH_TOKEN=ghp_...

dotnet nuget add source "https://nuget.pkg.github.com/rewaffle/index.json" \
  --name rewaffle \
  --username YOUR_GITHUB_USERNAME \
  --password "$NODE_AUTH_TOKEN" \
  --store-password-in-clear-text

dotnet restore Motely.Wasm/Motely.Wasm.csproj -p:EnableFileSystem=true
dotnet list Motely.Wasm/Motely.Wasm.csproj package | grep -i FileSystem
```

User-level config (merges with repo `nuget.config`): `~/.config/NuGet/NuGet.Config`  
Repo never commits the private source.

Pinned version: `Directory.Packages.props` → `Bootsharp.FileSystem` (date stamp, e.g. `2026.7.1.1608`).

### B2. npm — JS half of the extension

`Motely.Wasm/.npmrc` already points `@rewaffle` at GitHub Packages and uses **`${NODE_AUTH_TOKEN}`** (no token in the file).

```sh
export NODE_AUTH_TOKEN=ghp_...   # same PAT as NuGet is fine if it has read:packages

cd Motely.Wasm
npm install @rewaffle/bootsharp-file-system --save-dev
# peer is optional; install only when you want the picker in testui
```

### B3. Publish with FileSystem compiled in

```sh
cd Motely.Wasm
dotnet publish Motely.Wasm.csproj -c Debug -p:EnableFileSystem=true
node scripts/patch-dist-base64-polyfill.mjs
# or: npm run build:debug:fs
```

### B4. Boot order (testui already does this)

```js
import bootsharp, { Bootsharp, MotelyFileSystem } from "./dist/index.mjs";
import * as fs from "@rewaffle/bootsharp-file-system";

fs.init(Bootsharp.FileSystem.FileMounter); // before boot
await bootsharp.boot();
await MotelyFileSystem.pickAndMountFolder();
```

---

## Checklist

| Step | Command / place |
|------|------------------|
| Feed on machine | local-feed pack **or** `dotnet nuget add source` rewaffle |
| Restore with FS | `dotnet restore … -p:EnableFileSystem=true` |
| Build with FS | `… -p:EnableFileSystem=true` or `npm run build:debug:fs` |
| JS peer | `NODE_AUTH_TOKEN` + `.npmrc` + `npm i @rewaffle/bootsharp-file-system` |
| Default CI / public | **no** flag, **no** hard rewaffle dependency |

If restore still says `NU1101 Bootsharp.FileSystem`, the feed is missing or the pin/version does not match a package on that feed — fix the feed, not the engine gate.
