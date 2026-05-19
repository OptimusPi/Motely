# jaml-ui

React components, UI tokens, sprites, and utilities for Balatro/JAML seed-finder apps.

Wraps [motely-wasm](https://www.npmjs.com/package/motely-wasm) — the Bootsharp-built WebAssembly port of the C# Motely seed engine — and ships a Balatro-styled design system called **Jimbo**.

## Install

```bash
npm install jaml-ui motely-wasm react react-dom
```

The CSS is loaded automatically as a side-effect import. No PostCSS, no Tailwind, no config.

## Boot motely-wasm before rendering

`motely-wasm` is a Bootsharp module. Boot it once at app startup, before React mounts. Two requirements:

1. Top-level `await bootsharp.boot(<bin path>)` in your entry point.
2. The `motely-wasm/bin/*` files must be served at that path. Copy them into your `public/` (Vite) or wire a static route (Next.js).

```ts
// src/main.tsx
import "jaml-ui/jimbo.css"; // already side-effect-imported by jaml-ui; safe either way
import { createRoot } from "react-dom/client";
import bootsharp from "motely-wasm";
import App from "./App";

await bootsharp.boot("/motely-wasm/bin");
createRoot(document.getElementById("root")!).render(<App />);
```

Vite serves `/motely-wasm/bin` if you copy `node_modules/motely-wasm/bin` into `public/motely-wasm/bin/`. See `examples/seed-finder` for a working setup.

> If you can't use top-level await (legacy Webpack, server components), the hooks (`useSearch`, `useAnalyzer`, `useJamlLibrary`) inline a Standby-guard and will boot lazily on first call. The top-level pattern is still preferred — it removes the boot wait from the first interaction.

## Quick start — a real seed finder in ~30 lines

```tsx
import { useState } from "react";
import { JamlIde, useSearch, type JamlIdeSearchResult } from "jaml-ui";

const STARTER_JAML = `must:\n  - joker: Blueprint\n    antes: [1,2,3,4,5,6,7,8]\ndeck: Red\nstake: White\n`;

export default function App() {
  const [jaml, setJaml] = useState(STARTER_JAML);
  const search = useSearch();

  const results: JamlIdeSearchResult[] = search.results.map((r) => ({
    seed: r.seed,
    score: r.score,
    tallyColumns: r.tallyColumns,
  }));

  return (
    <JamlIde
      jaml={jaml}
      onChange={setJaml}
      searchResults={results}
      isSearching={search.status === "running"}
      onSearch={() => search.startAesthetic(jaml, /* aesthetic */ 0)}
    />
  );
}
```

That's the whole integration. `useSearch` handles boot guarding, search lifecycle, progress events, and result accumulation; `JamlIde` renders the editor, visual filter, results column, and toolbar.

## Package exports

Five subpath entries, each a barrel. The public API is exactly what they re-export.

| Entry | What's in it | When you import it |
| ----- | ------------ | ------------------ |
| `jaml-ui` | Game cards, JAML IDE, search hooks | The default surface — most apps only need this |
| `jaml-ui/ui` | Jimbo design system (JimboPanel, JimboButton, JimboModal, tokens) | Building custom Balatro-styled UI |
| `jaml-ui/core` | Sprite metadata, asset URLs, canvas `Layer` — pure, **no React, no motely-wasm** | Next.js server components, server-side rendering |
| `jaml-ui/motely` | Re-exports `bootsharp` + `Motely` from motely-wasm, plus item-decode helpers and `useJamlLibrary` | Direct motely-wasm access, file-system mount |
| `jaml-ui/r3f` | 3D card via React Three Fiber | Optional, has its own peer deps |

```tsx
import { JamlGameCard, useSearch } from "jaml-ui";
import { JimboPanel, JimboButton, JimboColorOption } from "jaml-ui/ui";
import { resolveJamlAssetUrl, SPRITE_SHEETS } from "jaml-ui/core";
import { Motely, decodeMotelyItemName } from "jaml-ui/motely";
```

## Examples

| Path | What it shows |
| ---- | ------------- |
| `examples/seed-finder` | End-to-end Vite app: boots motely-wasm, renders `JamlIde`, runs real searches. Copy it, point it at your published `jaml-ui`, ship. |

```bash
cd examples/seed-finder
pnpm install
pnpm dev
```

## Core components

### Game card

```tsx
import { JamlGameCard } from "jaml-ui";

<JamlGameCard
  type="joker"
  card={{ name: "Blueprint", edition: "Foil", isEternal: true, scale: 1.5 }}
/>
```

### JAML IDE

The full editor — code view, visual filter, results column, toolbar — in one component:

```tsx
import { JamlIde } from "jaml-ui";

<JamlIde
  jaml={jaml}
  onChange={setJaml}
  searchResults={results}
  onSearch={handleSearch}
  isSearching={isSearching}
/>
```

### Standalone code editor

```tsx
import { JamlCodeEditor } from "jaml-ui";

<JamlCodeEditor value={jaml} onChange={setJaml} />
```

### JAML map preview

```tsx
import { JamlMapPreview } from "jaml-ui";

<JamlMapPreview jaml={jaml} />
```

### Jimbo UI primitives

```tsx
import { JimboPanel, JimboButton, JimboModal } from "jaml-ui/ui";

<JimboPanel sway onBack={() => setOpen(false)}>
  <JimboButton variant="primary" onClick={handleSearch}>Search</JimboButton>
</JimboPanel>
```

Button variants: `primary`, `secondary`, `danger`, `back`, `ghost`.

## Hooks

All hooks lazily boot motely-wasm on first call if you skipped the top-level `bootsharp.boot()`.

### `useSearch()` — run JAML filter searches

```ts
import { useSearch } from "jaml-ui";

const {
  results,           // SearchResult[]
  status,            // "idle" | "running" | "completed" | "cancelled" | "error"
  totalSearched,     // bigint
  matchingSeeds,     // bigint
  seedsPerSecond,    // number
  error,             // string | null
  startAesthetic,    // (jaml, aesthetic) => Promise<void>
  startSeedList,     // (jaml, seeds[]) => Promise<void>
  startRandom,       // (jaml, count) => Promise<void>
  cancel,
  reset,
  clearError,
} = useSearch();
```

### `useAnalyzer()` — analyze a known seed

```ts
import { useAnalyzer } from "jaml-ui";
const { status, result, error, analyze } = useAnalyzer();
await analyze(jaml, "ALEEB");
```

### `useSearchPool()` — multi-worker parallel search

For long searches, spreads work across a pool of web workers.

### `useJamlLibrary()` — browser file picker for `.jaml` files

```tsx
import { useJamlLibrary } from "jaml-ui/motely";

const library = useJamlLibrary();
await library.mount();                              // prompts user to pick a folder
const source = await library.loadFile(library.files[0]);
await library.saveFile("filters/example.jaml", source);
```

Requires `@rewaffle/bootsharp-file-system` (optional peer). When absent, `status === "unsupported"`.

## Core utilities (no React)

```ts
import { SPRITE_SHEETS, getSpriteData, resolveJamlAssetUrl, Layer } from "jaml-ui/core";
```

`resolveJamlAssetUrl(key)` returns a bundled asset URL — Vite imports the PNGs at build time, so consumers don't have to wire any base URL.

## Motely decode helpers

```ts
import {
  decodeMotelyItem,
  decodeMotelyItemName,
  motelyItemDisplayName,
  motelyItemTypeName,
} from "jaml-ui/motely";
```

## 3D card (optional)

```bash
npm install three @react-three/fiber @react-three/drei @react-spring/three
```

```tsx
import { Card3D } from "jaml-ui/r3f";

<Card3D itemName="Blueprint" />
```

## Next.js

- Use `jaml-ui/core` from server components — it has no React and no motely-wasm imports.
- Use `jaml-ui` / `jaml-ui/motely` only from client components (mark the file with `"use client"`).
- Serve `motely-wasm/bin` at `/motely-wasm/bin` via a public folder or a catch-all route, then call `await bootsharp.boot("/motely-wasm/bin")` from a client component or your client entry script.
- If you're consuming this as a workspace package:

  ```ts
  // next.config.ts
  const nextConfig = { transpilePackages: ["jaml-ui"] };
  ```

## Peer dependencies

| Peer | Required for | Optional? |
| ---- | ------------ | --------- |
| `react`, `react-dom` | All components | No |
| `motely-wasm` | All search/analyzer/decode functionality | No (direct dep) |
| `react-icons` | Components that render icons | Yes |
| `@rewaffle/bootsharp-file-system` | `useJamlLibrary` folder mount | Yes |
| `three`, `@react-three/fiber`, `@react-three/drei`, `@react-spring/three` | `jaml-ui/r3f` only | Yes |

## Troubleshooting

- **"Motely is not initialized"** — you didn't call `bootsharp.boot()` before rendering, and the lazy guard hasn't fired yet. Add `await bootsharp.boot("/motely-wasm/bin")` to your entry point.
- **`404 /motely-wasm/bin/...`** — copy `node_modules/motely-wasm/bin/` into `public/motely-wasm/bin/` (Vite) or add a static route (Next.js).
- **CSS not applied** — make sure you imported from `jaml-ui` or `jaml-ui/ui` at least once. Both pull in `jimbo.css` as a side effect. If your bundler strips side-effect imports, add `import "jaml-ui/jimbo.css"`.
- **Storybook hangs on boot** — Storybook's `staticDirs` already serves `motely-wasm/bin`; see `.storybook/main.ts` for the pattern.

## License

MIT
