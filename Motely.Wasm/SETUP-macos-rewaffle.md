# macOS setup — Bootsharp.FileSystem + @rewaffle

Sponsor bits only. Public nuget.org has **no** `Bootsharp.FileSystem`.
Public npm has **no** `@rewaffle/bootsharp-file-system` without GitHub Packages auth.

## 1. GitHub Packages (npm) — already wired in `.npmrc`

`Motely.Wasm/.npmrc` points `@rewaffle` at `npm.pkg.github.com` and reads the token from **`RNV`** (no secrets in the file).

```sh
# Create a classic PAT: https://github.com/settings/tokens
# Scope: read:packages  (and repo if the package is private to a private org)
# Or fine-grained: Packages → Read for the rewaffle org/repo

export RNV=ghp_your_token_here

# Optional: persist for this machine only (shell profile, not the repo)
echo 'export RNV=ghp_...' >> ~/.zshrc   # prefer a secrets manager if you have one

cd Motely.Wasm
npm install    # pulls @rewaffle/bootsharp-file-system via GitHub Packages
```

If install 401s: token missing, expired, or not invited as a [Bootsharp / rewaffle sponsor](https://github.com/sponsors/elringus).

## 2. NuGet — Bootsharp.FileSystem (C#)

Repo `nuget.config` only lists nuget.org on purpose. **User-level** feed holds the sponsor package:

```sh
mkdir -p ~/.config/NuGet
```

Edit `~/.config/NuGet/NuGet.Config` (merge with existing sources — no `<clear />` if you can help it):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <!-- Pick ONE of the following that matches how you receive the package: -->

    <!-- A) GitHub Packages NuGet (if elringus/rewaffle publish there) -->
    <add key="github-rewaffle" value="https://nuget.pkg.github.com/rewaffle/index.json" />

    <!-- B) Local folder of .nupkg drops (common on sponsor machines) -->
    <!-- <add key="bootsharp-local" value="/Users/YOU/path/to/bootsharp-nupkgs" /> -->
  </packageSources>

  <packageSourceCredentials>
    <github-rewaffle>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <!-- Prefer env var: set NUGET_AUTH_TOKEN or paste once; never commit this file -->
      <add key="ClearTextPassword" value="%RNV%" />
    </github-rewaffle>
  </packageSourceCredentials>
</configuration>
```

NuGet on macOS often wants the literal password or a credential provider; if `%RNV%` does not expand, put the token once in `ClearTextPassword` in this **user** file only, or use:

```sh
dotnet nuget add source https://nuget.pkg.github.com/rewaffle/index.json \
  --name github-rewaffle \
  --username YOUR_GITHUB_USERNAME \
  --password "$RNV" \
  --store-password-in-clear-text
```

Pinned version lives in repo `Directory.Packages.props`:

```xml
<PackageVersion Include="Bootsharp.FileSystem" Version="2026.7.1.1608" />
```

Bump when elringus ships a newer date stamp. Confirm restore:

```sh
dotnet restore Motely.Wasm/Motely.Wasm.csproj
dotnet list Motely.Wasm/Motely.Wasm.csproj package | grep -i FileSystem
```

## 3. Build WASM with FileSystem

```sh
export RNV=...   # npm + nuget if using GitHub

cd Motely.Wasm
npm install
dotnet publish Motely.Wasm.csproj -c Debug   # faster smoke
# Release needs NativeAOT-LLVM package (Bootsharp 0.9 auto); install when ready:
#   runtime.osx-arm64.Microsoft.DotNet.ILCompiler.LLVM via Bootsharp's restore path

node scripts/patch-dist-base64-polyfill.mjs
```

## 4. JS boot order (test UI already does this)

```js
import bootsharp, { Bootsharp, MotelyFileSystem } from "./dist/index.mjs";
import * as fs from "@rewaffle/bootsharp-file-system";

fs.init(Bootsharp.FileSystem.FileMounter); // before boot
await bootsharp.boot();
// then MotelyFileSystem.pickAndMountFolder() etc.
```

## Checklist

| Piece | Where | Auth |
|-------|--------|------|
| `@rewaffle/bootsharp-file-system` | npm GitHub Packages | `RNV` + `.npmrc` |
| `Bootsharp.FileSystem` nupkg | user NuGet feed | same PAT / local folder |
| `MotelyFileSystem.cs` | compiled into motely-wasm | needs nupkg restore |
| Engine search / JAML | public | none |

No tokens in the repo. `RNV` stays in the shell (or a private secrets store).
