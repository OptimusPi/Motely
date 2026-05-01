# Audit — `src/assets.ts` (jaml-ui)

Date: 2026-05-01. Read-only audit. No edits made. Pifreak observation: "hilariously over-engineered." Diagnosis: **iteration debris from an abandoned-but-not-deleted CDN-asset path. Dead code present. Live code is fine but lightly bloated.**

## Public API surface

`src/assets.ts` exports:

| symbol | type | callers | status |
|---|---|---|---|
| `JAML_ASSET_FILES` | const | many | ✅ keep |
| `JamlAssetKey`, `JamlAssetFile` | types | many | ✅ keep |
| `resolveJamlAssetUrl` | fn | 6 internal (sprites.tsx, motelySprite.ts) | ✅ keep — load-bearing |
| `setJamlAssetBaseUrl` | fn | 1 external: weejoker.app `ClientProviders.tsx` | ✅ keep |
| `clearJamlAssetBaseUrl` | fn | **0 callers** anywhere | 🪦 dead |
| `getDefaultJamlAssetUrlMap` | fn | **0 callers** anywhere | 🪦 dead |

## Internal helpers

| symbol | usage | status |
|---|---|---|
| `assetKeyByFileName` | used inside `resolveJamlAssetUrl` for filename-input overload | ✅ keep (NOT dead — it supports passing `"Jokers.png"` instead of `"jokers"` to resolver) |
| `defaultAssetUrls` | the bundled-asset URL map | ✅ keep, compress |
| `normalizeBaseUrl` | called by `joinAssetUrl` and `setJamlAssetBaseUrl` | ✅ keep |
| `joinAssetUrl` | called by `resolveJamlAssetUrl` when `customAssetBaseUrl` is set | ✅ keep — **the relative-path branch matters for weejoker's `/assets` input** |
| `customAssetBaseUrl` | mutable module state; set by setter, read by resolver | ✅ keep |

## Why it FEELS over-engineered

Three reasons:

1. **The 12-line `defaultAssetUrls` literal repeats the same construction.** Should be one `Object.fromEntries` line.
2. **`clearJamlAssetBaseUrl` and `getDefaultJamlAssetUrlMap` exist with zero callers.** Phantom API surface. Looks like the file is doing more than it is.
3. **`joinAssetUrl`'s regex-and-branch logic is uncommented.** The reason it exists (to support relative-path inputs like `/assets`) is non-obvious. Reads like premature abstraction; actually solves a real case (weejoker).

## Recommended diff (audit only — NOT applied)

### Keep
- All current exports EXCEPT `clearJamlAssetBaseUrl` and `getDefaultJamlAssetUrlMap`.
- All internal helpers.

### Delete (dead code)
- `export function clearJamlAssetBaseUrl` (zero callers across jaml-ui, d:\mmm, thelongblind6, weejoker)
- `export function getDefaultJamlAssetUrlMap` (zero callers anywhere)
- Corresponding re-exports in `src/core.ts` and `src/index.ts`

### Compress
The 12-key `defaultAssetUrls` literal:
```ts
const defaultAssetUrls: Record<JamlAssetKey, string> = {
  deck: new URL(`../assets/${JAML_ASSET_FILES.deck}`, import.meta.url).href,
  blinds: new URL(`../assets/${JAML_ASSET_FILES.blinds}`, import.meta.url).href,
  // ... 10 more identical lines
};
```
becomes:
```ts
const defaultAssetUrls = Object.fromEntries(
  (Object.entries(JAML_ASSET_FILES) as Array<[JamlAssetKey, JamlAssetFile]>).map(
    ([key, fileName]) => [key, new URL(`../assets/${fileName}`, import.meta.url).href]
  )
) as Record<JamlAssetKey, string>;
```
**12 lines → 5 lines.**

### Document
Add a one-line comment above `joinAssetUrl` explaining why the relative-path branch exists (caller is weejoker passing `"/assets"`):
```ts
// `new URL(file, base)` requires `base` to be absolute. weejoker.app passes
// `/assets` (relative), so we fall back to string concatenation for that case.
function joinAssetUrl(baseUrl: string, fileName: JamlAssetFile): string { ... }
```

### Optional simplification
`normalizeBaseUrl`'s empty-string error is redundant — `setJamlAssetBaseUrl` already short-circuits on empty input before calling. Could remove the throw and just trust the input:
```ts
function normalizeBaseUrl(baseUrl: string): string {
  return baseUrl.endsWith("/") ? baseUrl : `${baseUrl}/`;
}
```

## File size impact (estimate)

| version | lines |
|---|---|
| current | 90 |
| post-cleanup (compressed + dead-removed + commented) | ~50 |

Roughly half. Same behavior. Same public API minus 2 dead exports.

## Why the MCP app might "suck balls" — hypothesis (not investigated)

`defaultAssetUrls` uses `new URL('../assets/X.png', import.meta.url)`. This pattern resolves correctly under:
- Vite (dev + build) — yes
- Next.js / Webpack — usually yes for client bundles
- Next.js / Turbopack — sometimes problematic; assets must be in the right output location

For the **mcp-ui** sub-app (Vite-built, single-file bundle), `import.meta.url` may resolve to a `data:` or `blob:` URL after `vite-plugin-singlefile` inlines everything — which means `new URL('../assets/X.png', dataUrl).href` returns garbage and sprites 404.

**Worth investigating** as a follow-up: does mcp-ui's deployed build actually hit the right URLs at runtime, or is `setJamlAssetBaseUrl` the way to fix it (set base to your CDN where the assets are uploaded via `pnpm cdn:upload-jaml-ui-assets`)?

But that's a separate audit. This file is the over-engineering audit.

## What I did NOT do

- Did NOT edit `src/assets.ts`
- Did NOT edit `src/core.ts` or `src/index.ts` (where dead exports are re-exported)
- Did NOT publish a new jaml-ui version
- Did NOT investigate why MCP app might be broken (parked as hypothesis)

Awaiting pifreak's call to apply the diff. ~10-15 minute change including version bump if greenlit. Or wait — also fine.
