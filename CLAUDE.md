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
| `jaml-ui` | `src/index.ts` | Game card components, JAML IDE, motely-bound hooks. Side-effect-imports `jimbo.css`. |
| `jaml-ui/ui` | `src/ui.ts` | Jimbo design system primitives (panels, buttons, modals, tokens). Side-effect-imports `jimbo.css`. |
| `jaml-ui/core` | `src/core.ts` | Pure helpers — sprite metadata, asset URL resolution, canvas `Layer`. **No React, no motely-wasm.** Safe for Next.js server components. |
| `jaml-ui/motely` | `src/motely.ts` | Re-exports `bootsharp`/`Motely` from `motely-wasm` plus item-decode helpers and the `useJamlLibrary` hook. |
| `jaml-ui/r3f` | `src/r3f.ts` | 3D card via React Three Fiber. r3f stack is an optional peer. |

Every entry point is a barrel — the public API is exactly what these five files re-export. Add a new public component by exporting from the relevant barrel; if it isn't re-exported there, it isn't part of the public surface.

### Externalized peers

`vite.config.ts` externalizes `react`, `react-dom`, `three`, `@react-three/*`, `react-icons`, `motely-wasm`, and `@rewaffle/bootsharp-file-system`. Consumers are expected to resolve these. Storybook (`.storybook/main.ts`) strips the `dts` plugin and forces `motely-wasm` to bundle so stories work; it also serves `node_modules/motely-wasm/bin` at `/motely-wasm/bin/`.

### Asset bundling

Vite bundles the sprite PNGs and other static assets via the imports in `src/assets.ts` — every `JAML_ASSET_FILES` entry is a real `import x from "../assets/x.png"`, and `resolveJamlAssetUrl()` returns the bundled URL. Consumers do nothing. There is no base URL to wire up.

### motely-wasm runtime contract

`motely-wasm` is Bootsharp-generated and must be booted once before any `Motely.*` call. The canonical pattern — copied from the bootsharp react sample and `motely-wasm/README.md` — is top-level await in the consumer's entry point:

```ts
import bootsharp, { Motely } from "motely-wasm";
import { createRoot } from "react-dom/client";

await bootsharp.boot("/motely-wasm/bin");
createRoot(document.getElementById("root")!).render(<App />);
```

By the time any component mounts, the runtime is up. Consumers are responsible for making `bin/` reachable at that URL (Storybook does this via `staticDirs`; consuming apps must do the equivalent).

Hooks like `useSearch`, `useAnalyzer`, and `useJamlLibrary` also inline a Standby-guard internally (see `src/hooks/useSearch.ts`: `bootsharp.getStatus() === bootsharp.BootStatus.Standby` → boot) so they work whether or not the consumer did the top-level await. **Don't add JS wrappers around motely-wasm** — import and call it directly (per `AGENTS.md`). As of this revision, the legacy `MotelyProvider` / `useMotelyRuntime` indirection layer is being removed; new code should not import them.

### Local motely-wasm iteration

When you need motely-wasm changes that aren't published yet, follow the flow in the auto-memory note `motely-wasm-local-publish-flow`: bump `MotelyVersion` in `Directory.Packages.props` in the MotelyJAML repo → `dotnet publish Motely.Wasm` → `npm pack` → copy tgz here → add `pnpm.overrides: { "motely-wasm": "file:./motely-wasm-<version>.tgz" }` in `package.json` → `pnpm install`. Remove the override when the new version actually ships.

### CSS / styling

`dist/ui/jimbo.css` is the design-system stylesheet, emitted by Vite as a single asset (`cssCodeSplit: false`, custom `assetFileNames`). `src/index.ts` and `src/ui.ts` import it as a side effect, so any consumer importing from `jaml-ui` or `jaml-ui/ui` automatically gets the CSS. `sideEffects` in `package.json` is configured to preserve this through tree-shaking.

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
