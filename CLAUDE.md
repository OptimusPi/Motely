# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Package manager is `pnpm` (lockfile is `pnpm-lock.yaml`).

- `pnpm build` — Vite library build, emits `dist/` with five entry bundles + `dist/ui/jimbo.css` + `.d.ts` via `vite-plugin-dts`.
- `pnpm dev` — `vite build --watch` (this is a library, not an app — there is no dev server for the library itself).
- `pnpm typecheck` — `tsc --noEmit --pretty false`.
- `pnpm lint` — ESLint over the repo.
- `pnpm storybook` — Storybook dev server on `:6006`. Stories are the primary visual dev surface.
- `pnpm build-storybook` / `pnpm serve:storybook` — build static Storybook, then serve on `:3141` with CORS (used by MCP/iframe consumers).
- Tests run via `vitest` driven by `@storybook/addon-vitest`: stories double as tests, executed in headless Chromium through `@vitest/browser-playwright`. To run a single story-as-test, use `pnpm vitest run -t "<Story Title>"` or filter by file path. There are no separate `*.test.*` files.

## Architecture

This is a multi-entry React component library with five subpath exports, all bundled by Vite in library mode (`vite.config.ts`):

| Entry | Source | Purpose |
| ----- | ------ | ------- |
| `jaml-ui` | `src/index.ts` | Game card components, JAML IDE, Analyzer Explorer, motely-bound hooks. Side-effect-imports `jimbo.css`. |
| `jaml-ui/ui` | `src/ui.ts` | Jimbo design system primitives (panels, buttons, modals, tokens). Side-effect-imports `jimbo.css`. |
| `jaml-ui/core` | `src/core.ts` | Pure helpers — sprite metadata, asset URL resolution, canvas `Layer`. **No React, no motely-wasm.** Safe for Next.js server components. |
| `jaml-ui/motely` | `src/motely.ts` | Re-exports `bootsharp`/`Motely` from `motely-wasm` plus item-decode helpers. |
| `jaml-ui/r3f` | `src/r3f.ts` | 3D card via React Three Fiber. r3f stack is an optional peer. |

Every entry point is a barrel — the public API is exactly what these five files re-export. Add a new public component by exporting from the relevant barrel; if it isn't re-exported there, it isn't part of the public surface.

### Externalized peers

`vite.config.ts` externalizes `react`, `react-dom`, `three`, `@react-three/*`, `react-icons`, `motely-wasm`, and `@rewaffle/bootsharp-file-system`. Consumers are expected to resolve these. Storybook (`.storybook/main.ts`) strips the `dts` plugin and forces `motely-wasm` to bundle so stories work; it also serves `node_modules/motely-wasm/bin` at `/motely-wasm/bin/`.

### motely-wasm runtime contract

`motely-wasm` is Bootsharp-generated and must be booted once before any `Motely.*` call:

```ts
import bootsharp, { Motely } from "motely-wasm";
await bootsharp.boot("/motely-wasm/bin");
```

Consumers are responsible for making `bin/` reachable at that URL (Storybook does this via `staticDirs`; consuming apps must do the equivalent). Hooks like `useSearch`, `useAnalyzer`, `useJamlLibrary`, `useMotelyRuntime` handle the boot internally — see `src/hooks/useSearch.ts` for the pattern (`bootsharp.getStatus() === bootsharp.BootStatus.Standby` → boot). **Don't add JS wrappers around motely-wasm** — import and call it directly (per `AGENTS.md`).

### Local motely-wasm iteration

When you need motely-wasm changes that aren't published yet, follow the flow in the auto-memory note `motely-wasm-local-publish-flow`: bump `MotelyVersion` in `Directory.Packages.props` in the MotelyJAML repo → `dotnet publish Motely.Wasm` → `npm pack` → copy tgz here → add `pnpm.overrides: { "motely-wasm": "file:./motely-wasm-<version>.tgz" }` in `package.json` → `pnpm install`. Remove the override when the new version actually ships.

### CSS / styling

`dist/ui/jimbo.css` is the design-system stylesheet, emitted by Vite as a single asset (`cssCodeSplit: false`, custom `assetFileNames`). `src/index.ts` and `src/ui.ts` import it as a side effect, so any consumer importing from `jaml-ui` or `jaml-ui/ui` automatically gets the CSS. `sideEffects` in `package.json` is configured to preserve this through tree-shaking.

## Design rules (from AGENTS.md)

These are hard constraints for any UI work in this repo:

- Never use ALL CAPS.
- Never use bold / heavy font-weight.
- Never put grey text on a grey background.
- Mobile-first at 375px width.
- "Juice" comes from CSS animations (`.j-font-dance-char`, `scale(1.05) translateY(-2px)`, etc.) — not JS wrappers.
- No visible scrollbars. Use magnetic scroll snapping.

## Component placement convention

Components live in `jaml-ui` with a Storybook story, and consuming apps only compose them — they do not define their own inline React components. If you find yourself sketching a component inline in a consumer repo, the right move is to add it here (with a story) and import it from the appropriate barrel.
