# Fix motely-node + JAMMY Once and For All

One plan. Two repos.

**Official refs:** [.NET Native AOT for Node.js](https://microsoft.github.io/node-api-dotnet/features/dotnet-native-aot.html) · [js-aot-module scenario](https://microsoft.github.io/node-api-dotnet/scenarios/js-aot-module.html) · [MSBuild props](https://microsoft.github.io/node-api-dotnet/reference/msbuild-props.html)

---

## Part 1: motely-node (MotelyJAML repo)

### 1.1 loadMotely — DONE

Already added in `motely-node/index.js`: `export function loadMotely(/* options */) { return Promise.resolve(api); }` and types in `index.d.ts`. server.js now resolves. No further change.

### 1.2 Bin path (DONE in source)

C# project uses `PublishDir` + `PublishMultiPlatformNodeModule`; index.js loads from `bin/<rid>/Motely.NodeAddon.node` (win-x64, linux-x64, osx-x64). Implemented in `Motely.NodeAddon.csproj` and `motely-node/index.js`.

[node-api-dotnet MSBuild props](https://microsoft.github.io/node-api-dotnet/reference/msbuild-props.html). In `Motely.NodeAddon.csproj`:

```xml
<PublishDir>$(MSBuildThisFileDirectory)..\motely-node\bin</PublishDir>
<PublishMultiPlatformNodeModule>true</PublishMultiPlatformNodeModule>
```

Then `dotnet publish -r win-x64` (and linux-x64, osx-x64) writes the `.node` into `motely-node/bin/<rid>/`.

### 1.3 After changing layout

Use the full workflow so both packages share one version: **[BUILD_NPM_PACKAGES_WORKFLOW.md](BUILD_NPM_PACKAGES_WORKFLOW.md)**. In short:

1. From repo root: `dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r win-x64`.
2. Run `./build-linux.ps1` (Windows) or `./build-linux.sh` (Unix) — builds linux-x64 in Docker (Ubuntu 22.04) for Vercel GLIBC 2.35. (No official `10.0-jammy` image; use `Dockerfile.linux-node`.)
3. Confirm the `.node` file is under `motely-node/bin/<rid>/`.
4. `cd motely-node && npm pack` (no build script; pack uses bin/ as-is).
5. Publish: `npm publish` when ready.

---

## Part 2: JAMMY (seed-finder-app repo)

### 2.1 ChatInterface component

Scenes (ChattingScene, HomeScene) use `ChatInterface` so the chat UI lives in one place — scenes stay separate, no spaghetti.

**If the file was missing:** Create `components/chat/ChatInterface.tsx`: accepts `ChatInterfaceProps` (see AppShell), renders conversation + prompt input with ai-elements (`Conversation`, `Message`, `PromptInput`, etc.). Scenes import it; they don’t own the chat markup.

### 2.2 motely-node version

After Part 1 is done and motely-node is published to npm:

- In JAMMY: `pnpm update motely-node` (or `pnpm add motely-node@<version>`).
- Run `pnpm run build`. The "Export loadMotely doesn't exist" error should be gone; fix any remaining errors (e.g. ChatInterface) per 2.1.

---

## Checklist

- [x] **motely-node:** loadMotely present in index.js + index.d.ts.
- [x] **motely-node:** Bin layout: C# PublishDir + PublishMultiPlatformNodeModule; index.js uses `bin/<rid>/`.
- [ ] **motely-node:** `dotnet publish -r win-x64`, then `./build-linux.sh` (or `.ps1`), then confirm .node in `motely-node/bin/<rid>/`.
- [ ] **motely-node:** `cd motely-node && npm run build && npm pack` → inspect tarball: `bin/` must contain the RID dirs and the .node files.
- [ ] **motely-node:** `npm publish`.
- [x] **JAMMY:** `components/chat/ChatInterface.tsx` (conversation + prompt input).
- [ ] **JAMMY:** `pnpm update motely-node`.
- [ ] **JAMMY:** `pnpm run build` — green.
