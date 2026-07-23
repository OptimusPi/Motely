# macOS setup — Bootsharp.FileSystem + @rewaffle

Sponsor bits only. Public nuget.org has **no** `Bootsharp.FileSystem`.
Public npm has **no** `@rewaffle/bootsharp-file-system` without GitHub Packages auth.

## 1. GitHub Packages (npm) — `.npmrc` in this folder

`Motely.Wasm/.npmrc` points `@rewaffle` at `npm.pkg.github.com` and reads the token from **`NODE_AUTH_TOKEN`** (standard GitHub Packages env name; no secrets in the file).

```sh
# Create a classic PAT: https://github.com/settings/tokens
# Scope: read:packages  (and repo if the package is private)

export NODE_AUTH_TOKEN=ghp_your_token_here

cd Motely.Wasm
npm install    # pulls @rewaffle/bootsharp-file-system via GitHub Packages
```

If install 401s: token missing, expired, or not invited as a [Bootsharp / rewaffle sponsor](https://github.com/sponsors/elringus).

## 2. NuGet — Bootsharp.FileSystem (C#)

Repo `nuget.config` only lists nuget.org on purpose. **User-level** feed holds the sponsor package:

```sh
mkdir -p ~/.config/NuGet
```

```sh
dotnet nuget add source https://nuget.pkg.github.com/rewaffle/index.json \
  --name github-rewaffle \
  --username YOUR_GITHUB_USERNAME \
  --password "$NODE_AUTH_TOKEN" \
  --store-password-in-clear-text
```

Or drop `.nupkg` files in a local folder and add that path as a source in `~/.config/NuGet/NuGet.Config`.

Pinned version in `Directory.Packages.props`:

```xml
<PackageVersion Include="Bootsharp.FileSystem" Version="2026.7.1.1608" />
```

```sh
dotnet restore Motely.Wasm/Motely.Wasm.csproj
dotnet list Motely.Wasm/Motely.Wasm.csproj package | grep -i FileSystem
```

## 3. Build

```sh
export NODE_AUTH_TOKEN=...

cd Motely.Wasm
npm install
dotnet publish Motely.Wasm.csproj -c Debug
node scripts/patch-dist-base64-polyfill.mjs
```

## 4. JS boot order

```js
import bootsharp, { Bootsharp, MotelyFileSystem } from "./dist/index.mjs";
import * as fs from "@rewaffle/bootsharp-file-system";

fs.init(Bootsharp.FileSystem.FileMounter); // before boot
await bootsharp.boot();
```

## Checklist

| Piece | Where | Auth |
|-------|--------|------|
| `@rewaffle/bootsharp-file-system` | npm GitHub Packages | `NODE_AUTH_TOKEN` + `.npmrc` |
| `Bootsharp.FileSystem` nupkg | user NuGet feed | same PAT / local folder |
| `MotelyFileSystem.cs` | in motely-wasm | needs nupkg restore |
