# jaml-ui — agent notes

This is a pnpm workspace centered on:

1. **`src/json-render/`** — zero-dependency JSON-to-React engine with a Balatro component catalog; its Storybook stories live in `src/json-render/stories/`.
2. **`examples/mcp-seed-finder/`** — a working MCP App that uses json-render + motely-wasm (plus `examples/seed-finder`, the web variant).
3. **`packages/`** — `jaml-codemirror` (editor integration, a workspace dep of the MCP example), `jaml-lang` (VS Code extension), and `jaml-lsp`.

`src/ui/` holds the Jimbo CSS tokens (`jimbo.css`, `jimbo-tokens.css`, `tokens.ts`) that json-render components use.

## Commands

- `pnpm install` — install root + workspace example deps.
- `pnpm build` — build the library to `dist/`.
- `pnpm typecheck` — `tsc --noEmit` for `src/`.
- `pnpm typecheck:all` — the above plus `typecheck` in every `packages/*` workspace member.
- `pnpm lint` — ESLint.
- `pnpm storybook` — Storybook dev server on port 3141 for the json-render stories.
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
- Keep the library surface lean; get explicit user approval before adding large component trees or design-system primitives.
- Confirm before publishing or irreversible git operations.
