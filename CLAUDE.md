# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

This is a library package — there is no dev server, test suite, or linter configured.

- `pnpm install` — install dependencies (lockfile is `pnpm-lock.yaml`; Node >= 18).
- `npm run build` — emit `dist/` via `tsc --pretty false`.
- `npm run dev` — `tsc --watch` for incremental compilation.
- `npm run typecheck` — `tsc --noEmit`; primary correctness check for this repo.
- `npm run prepack` — runs the build (invoked automatically on `npm pack` / publish).

## Module boundaries (the package shape is the architecture)

`package.json` defines **five public entry points** that callers can import independently. Keep the dependency and runtime assumptions of each entry strictly in their own lane — this is what allows `jaml-ui/core` to be server-safe and the optional peers (`motely-wasm`, `three`, etc.) to stay optional.

| Entry              | Source file         | Runtime                | May depend on                                                            |
| ------------------ | ------------------- | ---------------------- | ------------------------------------------------------------------------ |
| `jaml-ui`          | `src/index.ts`      | Browser / React client | React, sprite data, canvas renderer, asset helpers                       |
| `jaml-ui/core`     | `src/core.ts`       | Server-safe / pure     | No React, no `motely-wasm`, no DOM — only asset metadata + bit decoders  |
| `jaml-ui/motely`   | `src/motely.ts`     | Browser / React client | `motely-wasm` (peer); motely enum decoding + display-name tables         |
| `jaml-ui/ui`       | `src/ui.ts`         | Browser / React client | Shared UI primitives (`panel`, `codeBlock`, `footer`, design tokens)     |
| `jaml-ui/r3f`      | `src/r3f.ts`        | Browser / React client | `three`, `@react-three/fiber`, `@react-spring/three` (all optional peers)|

When adding an export, put it in the entry whose peer-dep envelope it already lives in. Do not add `"use client"` files or React imports to `core.ts`. Do not import `motely-wasm` outside `motely.ts` / `decode/motelyItemDecoder.ts` / `motelyDisplay.ts`. Do not import `three` outside `src/r3f/`.

## TypeScript / ESM conventions

- The package is ESM-only (`"type": "module"`), compiled with `moduleResolution: "bundler"`, `target: ES2022`, `strict: true`, `jsx: "react-jsx"`. Source lives in `src/`, output in `dist/`.
- **All relative imports must use the `.js` extension**, even when importing `.ts`/`.tsx` source (e.g. `import { Layer } from "./render/Layer.js"`). This is required for the emitted ESM to resolve at runtime. Copy the pattern from existing files.
- Client components must start with `"use client";` so Next.js consumers get the correct boundary. Server-safe modules (everything reachable from `core.ts`) must not.
- Peer dependencies in `package.json`: `react`/`react-dom` are required; `motely-wasm`, `three`, `@react-three/fiber`, `@react-spring/three`, and `react-icons` are marked optional — guard any new usage accordingly.
- `sideEffects: false` is set — avoid top-level side effects in new modules so tree-shaking stays effective. The one intentional exception is the module-scoped `ITEM_MAP` build in `src/sprites/spriteMapper.ts`.

## Sprite / asset pipeline

Rendering is a layered 2D-canvas system driven by sprite-atlas metadata. Understanding this chain is the fastest way to be productive:

1. **`src/assets.ts`** — source of truth for PNG asset resolution. `JAML_ASSET_FILES` maps keys (`jokers`, `tarots`, `vouchers`, …) to filenames under `/assets`. Default URLs use `new URL("../assets/...", import.meta.url)`. Consumers override via `setJamlAssetBaseUrl(url)` / `clearJamlAssetBaseUrl()`. Always route asset URL resolution through `resolveJamlAssetUrl` so the override keeps working.
2. **`src/sprites/spriteData.ts`** — grid dimensions per sheet (`SPRITE_SHEETS[key].columns/rows/src`) plus exhaustive `{ name, pos: {x,y} }` tables (`JOKERS`, `JOKER_FACES`, `TAROTS_AND_PLANETS`, `VOUCHERS`, `BOSSES`, `TAGS`, `BOOSTER_PACKS`, `CONSUMABLE_FACES`) and the `EDITION_MAP` / `STICKER_MAP` / `RANK_MAP` / `SUIT_MAP` / `ENHANCER_MAP` / `SEAL_MAP` position maps. When adding a new Balatro item, this is where its sprite coordinates live.
3. **`src/sprites/spriteMapper.ts`** — builds `ITEM_MAP` once at module load by inserting each entry under multiple normalizations (original, lowercase, no-spaces, both). `getSpriteData(name)` does O(1) lookup and strips prefixes like `Joker | `. Add normalizations here, not at call sites.
4. **`src/render/Layer.ts`** — `Layer` class describing one sprite draw (source URL, atlas grid, position, z-order, `animated` flag).
5. **`src/render/CanvasRenderer.tsx`** — `JamlCardRenderer` component. Preloads all sheets into an image cache, sorts layers by `order`, draws them into a single `<canvas>`, sizes the canvas from layer 0's slice, and (optionally) runs a `requestAnimationFrame` loop that applies a sinusoidal offset/alpha for layers flagged `animated`. `hoverTilt` adds pointer-driven 3D transforms on the container.
6. **`src/components/GameCard.tsx`** — composes the layer stack for a specific item (`JamlGameCard`, `JamlVoucher`, `JamlTag`, `JamlBoss`), combining base sprite + edition + stickers + seal/enhancer for playing cards. Also exposes `resolveAnalyzerShopItem` which turns an analyzer `{id,name}` into the appropriate renderable shape.

The R3F path (`src/r3f/Card3D.tsx`) reuses the same sprite metadata but slices `THREE.Texture` UVs via `repeat`/`offset` instead of drawing to a canvas; it maintains its own `_textureCache`.

## Decoding / Motely integration

Two independent decoders coexist — don't conflate them:

- **`src/decode/packedBalatroItem.ts`** (exported via `core`) — pure bit-math for the Balatro/analyzer shop encoding: top nibble = category (`PlayingCard=1 … Joker=5, Invalid=0xf`), next two bits = joker rarity, low bits = index. No runtime dependencies.
- **`src/decode/motelyItemDecoder.ts`** (exported via `motely`) — decodes `motely-wasm`'s `MotelyItem.Value` packed ints. Different masks (`0xf000` category, then seal `0x70000`, enhancement `0x780000`, edition `0x3800000`). Returns `DecodedMotelyItem` + `decodeMotelyItemToJamlCard` which maps directly onto `JamlGameCard` props. Uses a cache — warm it with `warmMotelyItemCache()` for hot paths.
- **`src/motelyDisplay.ts`** — enum-key ↔ human-readable label tables for bosses, booster packs, tags, vouchers. Use these when bridging motely enum strings to UI strings; don't sprinkle ad-hoc mappings elsewhere.
- **`src/utils/itemUtils.ts`** — `getItemDisplayName` (PascalCase → spaced, with special cases like `ChaostheClown`, `OopsAll6s`, playing-card codes like `D10`), plus `getItemCategory` (classifies a motely enum key into `joker|tarot|planet|spectral|playing`).

`src/hooks/useShopStream.ts` (`useMotelyStream`) is a **generic** streaming hook — it takes `initStream` / `nextItem` callbacks and a `deps` array. It's engine-agnostic by design; keep motely-specific logic at the call site so the hook stays reusable for any analyzer stream.

## JAML preview / IDE flow

- `src/utils/jamlMapPreview.ts` — `extractVisualJamlItems(jaml)` parses raw JAML text and groups clauses into `must | should | mustNot` buckets of `{ visualType, value }`. The `CLAUSE_VISUAL_TYPES` map controls which clause keys map to which renderable (`joker | consumable | voucher | tag | boss`). Add new JAML clause keys here.
- `src/components/JamlMapPreview.tsx` — consumes the groups and renders each via the appropriate `Jaml*` component from `GameCard.tsx`.
- `src/components/JamlIde.tsx` + `JamlIdeToolbar.tsx` — intentionally lightweight textarea-based shell. Per the README, rich editor integrations (Monaco, LSPs, extension hosts) belong in downstream app packages, **not** here.

## Git / branch policy

All work in this session must be committed to branch `claude/add-claude-documentation-XB3Hp` and pushed there. Do not push to `main` or any other branch without explicit permission.
