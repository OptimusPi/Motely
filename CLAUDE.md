# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Package manager is `pnpm` (lockfile is `pnpm-lock.yaml`).

- `pnpm build` — Vite library build, emits `dist/` with five entry bundles + `dist/ui/jimbo.css` + `.d.ts` via `vite-plugin-dts`.
- `pnpm dev` — `vite build --watch` (this is a library, not an app — there is no dev server for the library itself).
- `pnpm typecheck` — `tsc --noEmit --pretty false`.
- `pnpm lint` — ESLint over the repo. Custom design-rule plugin lives in `eslint-rules/jaml-design.js` (rules: `no-raw-button`, `no-emoji-jsx`, `no-uppercase-text`, `no-bold-style`) — these enforce the design rules below at lint time.
- `pnpm storybook` — Storybook dev server on `:6006`. Stories are the primary visual dev surface.
- `pnpm build-storybook` / `pnpm serve:storybook` — build static Storybook, then serve on `:3141` with CORS (used by MCP/iframe consumers).
- Tests run via `vitest` driven by `@storybook/addon-vitest`: stories double as tests, executed in headless Chromium through `@vitest/browser-playwright` (see `vitest.config.ts`). To run a single story-as-test, use `pnpm vitest run -t "<Story Title>"` or filter by file path. There are no separate `*.test.*` files.
- `examples/seed-finder` is the canonical end-to-end consumer app (boots motely-wasm, renders `JamlIde`, runs real searches): `cd examples/seed-finder && pnpm install && pnpm dev`. (There is no top-level `pnpm demo` — the script may exist in `package.json` but the `demo/` directory does not.)

## Architecture

This is a multi-entry React component library with five subpath exports, all bundled by Vite in library mode (`vite.config.ts`):

| Entry | Source | Purpose |
| ----- | ------ | ------- |
| `jaml-ui` | `src/index.ts` | Game card components, JAML IDE, motely-bound hooks. Side-effect-imports `jimbo.css`. |
| `jaml-ui/ui` | `src/ui.ts` | Jimbo design system primitives (panels, buttons, modals, tokens). Side-effect-imports `jimbo.css`. |
| `jaml-ui/core` | `src/core.ts` | Pure helpers — sprite metadata, asset URL resolution, canvas `Layer`. **No React, no motely-wasm.** Safe for Next.js server components. |
| `jaml-ui/motely` | `src/motely.ts` | Re-exports `bootsharp`/`Motely` from `motely-wasm` plus item-decode helpers and the `useJamlLibrary` hook. |
| `jaml-ui/r3f` | `src/r3f.ts` | 3D card via React Three Fiber. r3f stack is an optional peer. |

Every entry point is a barrel — the public API is exactly what these five files re-export. Add a new public component by exporting from the relevant barrel; if it isn't re-exported there, it isn't part of the public surface.

All hooks and components carry `"use client"` at the top of the file for Next.js RSC compatibility. Add it to any new hook or component.

### `src/lib/` — pure utilities

`src/lib/` is a pure-utility layer with no React and no motely-wasm: JAML parsing (`jamlParser.ts`, `jamlSchema.ts`, `jamlCompletion.ts`), card/TTS display helpers, constants, and shared types. Anything that must be safe for server components or workers lives here. The `src/hooks/` layer builds on top of it to expose React-specific state.

### Externalized peers

`vite.config.ts` externalizes `react`, `react-dom`, `three`, `@react-three/*`, `react-icons`, `motely-wasm`, and `@rewaffle/bootsharp-file-system`. Consumers are expected to resolve these. Storybook (`.storybook/main.ts`) strips the `dts` plugin and forces `motely-wasm` to bundle so stories work; it also serves `node_modules/motely-wasm/bin` at `/motely-wasm/bin/`.

### Asset bundling

Vite bundles the sprite PNGs and other static assets via the imports in `src/assets.ts` — every `JAML_ASSET_FILES` entry is a real `import x from "../assets/x.png"`, and `resolveJamlAssetUrl()` returns the bundled URL. Consumers do nothing. There is no base URL to wire up.

### Search hooks

`useSearch` (`src/hooks/useSearch.ts`) runs the WASM search on the main thread — suitable for low-volume runs. `useSearchPool` (`src/hooks/useSearchPool.ts`) shards work across Web Workers (up to `navigator.hardwareConcurrency`, capped at 8) for throughput-intensive searches. Key constraint: **aesthetic mode always forces a single worker** in `useSearchPool` because the aesthetic enumerator is shared state inside one WASM runtime; multiple workers would restart and produce duplicates.

Both hooks call `ensureMotelyReady()` (from `src/lib/motely/runtime.ts`, also exported from `jaml-ui/motely`) before any WASM call. Workers load via Vite's `?worker` lazy import and receive serialised `PoolStartMessage` objects.

### motely-wasm runtime contract

`motely-wasm` is Bootsharp-generated and must be booted once before any `Motely.*` call. The canonical pattern — copied from the bootsharp react sample and `motely-wasm/README.md` — is top-level await in the consumer's entry point:

```ts
import bootsharp, { Motely } from "motely-wasm";
import { createRoot } from "react-dom/client";

await bootsharp.boot("/motely-wasm/bin");
createRoot(document.getElementById("root")!).render(<App />);
```

By the time any component mounts, the runtime is up. Consumers are responsible for making `bin/` reachable at that URL (Storybook does this via `staticDirs`; consuming apps must do the equivalent).

Hooks like `useSearch`, `useAnalyzer`, and `useJamlLibrary` also inline a Standby-guard internally (see `src/hooks/useSearch.ts`: `bootsharp.getStatus() === bootsharp.BootStatus.Standby` → boot) so they work whether or not the consumer did the top-level await. **Don't add JS wrappers around motely-wasm** — import and call it directly (per `AGENTS.md`). There is no `MotelyProvider` / `useMotelyRuntime` indirection layer; do not reintroduce one.

### Updating motely-wasm

`motely-wasm` is a published npm package. Bump it with `pnpm update motely-wasm` (respects the `^` range) or raise the range and `pnpm install`.

Bootsharp codegen sometimes relocates generated exports across subpaths. If `pnpm typecheck` reports "no exported member" after a bump, the symbol moved — locate it under `node_modules/motely-wasm/dist/generated/` and update the import. Example (18.2.x): `MotelyDeck`/`MotelyStake` → `motely-wasm/motely/enums`, `JamlAesthetic` → `motely-wasm/motely/filters/jaml`.

### CSS / styling

`dist/ui/jimbo.css` is the design-system stylesheet, emitted by Vite as a single asset (`cssCodeSplit: false`, custom `assetFileNames`). `src/index.ts` and `src/ui.ts` import it as a side effect, so any consumer importing from `jaml-ui` or `jaml-ui/ui` automatically gets the CSS. `sideEffects` in `package.json` is configured to preserve this through tree-shaking.

For DOM components, always use CSS custom properties (`--j-red`, `--j-darkest`, etc.) defined in `jimbo.css`. Use the JS constants in `src/ui/tokens.ts` (`JimboColorOption`) only in contexts that cannot use CSS — R3F/Three.js, canvas drawing, inline SVG fills, or imperative animation APIs. Do not use the JS constants in JSX styles.

### Fonts

Two font tokens, both defined in `jimbo.css` (`:root`):

- **`--j-font: 'm6x11plus', 'm6x11', monospace`** — the UI font for everything player-facing (the Balatro pixel font). All `.j-text--*` size classes and every `Jimbo*` component render in this. This is the default; use it for all UI text.
- **`--j-font-code: 'JetBrains Mono', 'Roboto Mono', monospace`** — the coding font, used only by `JamlCodeEditor` and `.j-code-block` for JAML source. The fallback chain is OS-native so code still reads as code if the Google Fonts stylesheet fails to load.

Never hardcode a `font-family`; reference one of these two tokens. `m6x11`/`m6x11plus` are bitmap pixel fonts — keep `line-height` ≥ ~1.1 (never `1`), or ascenders/descenders clip.

## Design rules

Source of truth is `AGENTS.md`. Summary of the hard constraints for any UI work in this repo:

- Never use ALL CAPS.
- Never use bold / heavy font-weight.
- Never put grey text on a grey background. `tone="grey"` text on any `--j-darkest` / `--j-dark-grey` / `--j-teal-grey` / `--j-surface-inset` surface is grey-on-grey.
- **Every component is a `Jimbo*` component.** No raw `<button>`, no inline anonymous components in consumer screens. Missing primitive? Add a `Jimbo*` to `src/ui/` with a story.
- **No emoji as icons.** Use `react-icons` (`react-icons/fi` preferred).
- **Item names go in `JimboTooltip`, not inline labels.** Sprite + tooltip-on-hover. Players recognize the art; the 320px surface can't afford permanent inline labels.
- Canonical surface is **iPhone SE 5 portrait, 320×568, HARD LOCKED — for every consumer, including MCP App embeds and desktop.** The `.j-app` shell is fixed at 320×568 — no scroll, no stretch, no reflow. We design for 320×568 first; we widen only after the 320 experience is right. If your component doesn't fit, redesign the component, don't relax the lock.
- "Juice" comes from CSS animations (`.j-font-dance-char`, `scale(1.05) translateY(-2px)`, etc.) — not JS wrappers.
- No visible scrollbars. Use magnetic scroll snapping.

## Component placement convention

Components live in `jaml-ui` with a Storybook story, and consuming apps only compose them — they do not define their own inline React components. If you find yourself sketching a component inline in a consumer repo, the right move is to add it here (with a story) and import it from the appropriate barrel. See `AGENTS.md`.
