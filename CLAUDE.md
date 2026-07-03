# jaml-ui — agent notes

This repo is now focused on two things:

1. **`src/json-render/`** — zero-dependency JSON-to-React engine with a Balatro component catalog.
2. **`examples/mcp-seed-finder/`** — a working MCP App that uses json-render + motely-wasm.

The legacy Jimbo UI component library and Storybook harness were stripped. `src/ui/` now only contains the Jimbo CSS token file (`jimbo.css`) that json-render components use.

## Commands

- `pnpm install` — install root + workspace example deps.
- `pnpm build` — build the library to `dist/`.
- `pnpm typecheck` — `tsc --noEmit`.
- `pnpm lint` — ESLint.
- `cd examples/mcp-seed-finder && pnpm build` — build the MCP App single-file HTML.

## Package surface

Subpath exports:

- `jaml-ui` — main: json-render + Balatro catalog + card components.
- `jaml-ui/ui` — CSS tokens side-effect import.
- `jaml-ui/core` — sprite metadata, assets, canvas `Layer` (pure, no React, no motely-wasm).
- `jaml-ui/motely` — `bootsharp` + `Motely` re-exports + decoders.

`motely-wasm@23.x` only exposes a root export. Import namespaces from there (`MotelySearch`, `MotelyJamlyzer`, `MotelyJaml`, etc.); the old subpath imports are gone.

## Working rules

- Keep `json-render` zero runtime deps (React only).
- Don't reintroduce large component trees, design-system primitives, or storybook scaffolding without explicit user approval.
- Confirm before publishing or irreversible git operations.
