# jaml-ui

Zero-dependency JSON-to-React engine (`json-render`) + Balatro card sprite rendering for MCP Apps.

## What's inside

- **`json-render`** — a tiny, zero-dep JSON-to-React engine in `src/json-render`. A JsonNode tree + a registry = rendered UI. Ships with a Balatro catalog (`Panel`, `Stack`, `Grid`, `SeedCard`, `SearchStats`, `JamlGameCard`, ...).
- **Balatro card sprites** — canvas-based `JamlGameCard`, `StandardCard`, `DeckSprite`, plus sprite metadata.
- **MCP App example** — `examples/mcp-seed-finder` is a single-file MCP App that talks to an MCP server and renders search results with `json-render`.

## Install

```bash
npm install jaml-ui motely-wasm react react-dom
```

`motely-wasm` is a peer dependency — boot it once before rendering.

## Quick start — render JSON UI

```tsx
import { render, balatroRegistry } from "jaml-ui";

const spec = {
  type: "Panel",
  props: { title: "Hello" },
  children: [
    { type: "Text", props: { body: "Seed: ALEEB", variant: "title" } },
    { type: "JamlGameCard", props: { type: "joker", card: { name: "Blueprint" } } },
  ],
};

export default function App() {
  return <div>{render(spec, balatroRegistry)}</div>;
}
```

## Boot motely-wasm

```tsx
import bootsharp from "motely-wasm";

await bootsharp.boot("/motely-wasm/bin");
```

## MCP App example

```bash
cd examples/mcp-seed-finder
pnpm install
pnpm dev
```

## Package exports

| Entry | What's in it |
| ----- | ------------ |
| `jaml-ui` | json-render engine, Balatro registry/catalog, card components |
| `jaml-ui/ui` | Jimbo CSS tokens only |
| `jaml-ui/core` | Sprite metadata, asset URLs, canvas `Layer` — no React, no motely-wasm |
| `jaml-ui/motely` | `bootsharp` + `Motely` re-exports, item decoders |

## Scripts

```bash
pnpm build      # Vite library build → dist/
pnpm dev        # vite build --watch
pnpm typecheck  # tsc --noEmit
pnpm lint       # ESLint
```

## License

MIT — see [LICENSE](LICENSE). Font files in `assets/fonts/` are third-party assets by Daniel Linssen; attribution required when redistributing or displaying the pixel fonts.
