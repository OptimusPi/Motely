# Fix Motely.node + JAMMY Once and For All

One plan. Two repos.

**Official refs:** [.NET Native AOT for Node.js](https://microsoft.github.io/node-api-dotnet/features/dotnet-native-aot.html) · [js-aot-module scenario](https://microsoft.github.io/node-api-dotnet/scenarios/js-aot-module.html) · [MSBuild props](https://microsoft.github.io/node-api-dotnet/reference/msbuild-props.html)

---

## Part 1: Motely.node (MotelyJAML repo)

### 1.1 loadMotely — DONE

Already added in `Motely.node/index.js`: `export function loadMotely(/* options */) { return Promise.resolve(api); }` and types in `index.d.ts`. server.js now resolves. No further change.

### 1.2 Bin path (DONE in source)

C# project uses `PublishDir` + `PublishMultiPlatformNodeModule`; index.js loads from `bin/<rid>/Motely.NodeAddon.node` (win-x64, linux-x64, osx-x64). Implemented in `Motely.NodeAddon.csproj` and `Motely.node/index.js`.

[node-api-dotnet MSBuild props](https://microsoft.github.io/node-api-dotnet/reference/msbuild-props.html). In `Motely.NodeAddon.csproj`:

```xml
<PublishDir>$(MSBuildThisFileDirectory)..\Motely.node\bin</PublishDir>
<PublishMultiPlatformNodeModule>true</PublishMultiPlatformNodeModule>
```

Then `dotnet publish -r win-x64` (and linux-x64, osx-x64) writes the `.node` into `Motely.node/bin/<rid>/`.

### 1.3 After changing layout

1. From repo root: `dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r win-x64` (and linux-x64 if you have it).
2. Confirm the `.node` file is under `Motely.node/bin/<rid>/`.
3. `cd Motely.node && npm run build && npm pack`.
4. Publish: `npm publish`.

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

- [x] **Motely.node:** loadMotely present in index.js + index.d.ts.
- [x] **Motely.node:** Bin layout: C# PublishDir + PublishMultiPlatformNodeModule; index.js uses `bin/<rid>/`.
- [ ] **Motely.node:** `dotnet publish -r win-x64` (and linux-x64, osx-x64) then confirm .node in `Motely.node/bin/<rid>/`.
- [ ] **Motely.node:** `cd Motely.node && npm run build && npm pack` → inspect tarball: `bin/` must contain the RID dirs and the .node files.
- [ ] **Motely.node:** `npm publish`.
- [x] **JAMMY:** `components/chat/ChatInterface.tsx` (conversation + prompt input).
- [ ] **JAMMY:** `pnpm update motely-node`.
- [ ] **JAMMY:** `pnpm run build` — green.
