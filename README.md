# jaml-ui

React components, UI tokens, sprites, and utilities for Balatro/JAML apps.

## Install

```bash
npm install jaml-ui react react-dom
```

## Package exports

| Entry | Contents |
| ----- | -------- |
| `jaml-ui` | Game card components, JAML IDE, Analyzer Explorer, hooks |
| `jaml-ui/ui` | Jimbo design system — JimboPanel, JimboButton, JimboModal, tokens |
| `jaml-ui/core` | Pure asset helpers, sprite metadata, decode utilities (no React) |
| `jaml-ui/motely` | motely-wasm decode helpers (requires `motely-wasm` peer) |
| `jaml-ui/r3f` | 3D card component via React Three Fiber (requires r3f peers) |

## Quick start

```tsx
import { JamlGameCard, AnalyzerExplorer, JamlIde } from "jaml-ui";
import { JimboPanel, JimboButton } from "jaml-ui/ui";
```

### Game card

```tsx
import { JamlGameCard } from "jaml-ui";

<JamlGameCard
  type="joker"
  card={{ name: "Blueprint", edition: "Foil", isEternal: true, scale: 1.5 }}
/>
```

### Jimbo UI (Balatro design system)

```tsx
import { JimboPanel, JimboButton, JimboModal } from "jaml-ui/ui";
import { JimboColorOption } from "jaml-ui/ui";

<JimboPanel sway onBack={() => setOpen(false)}>
  <JimboButton variant="primary" onClick={handleSearch}>Search</JimboButton>
</JimboPanel>
```

Available variants: `primary`, `secondary`, `danger`, `back`, `ghost`

### JAML IDE

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

### Analyzer Explorer

```tsx
import { AnalyzerExplorer } from "jaml-ui";

// antes: AnalyzerAnteView[] — stream from motely-wasm createSearchContext
<AnalyzerExplorer antes={antes} totalAntes={8} highlights={highlights} />
```

### JAML Map Preview

```tsx
import { JamlMapPreview } from "jaml-ui";

<JamlMapPreview jaml={jaml} />
```

## Core utilities

```ts
import { SPRITE_SHEETS, getSpriteData, resolveJamlAssetUrl } from "jaml-ui/core";
```

## Motely decode helpers

```ts
import { decodeMotelyItemName, motelyItemTypeName } from "jaml-ui/motely";
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

Import pure helpers from `jaml-ui/core` for server components. For local workspace installs add:

```ts
// next.config.ts
const nextConfig = { transpilePackages: ["jaml-ui"] };
```

## Search Worker Architecture

The library provides `useSearch` and `useAnalyzer` helpers that use `motely-wasm`'s Bootsharp-generated ES module directly. Bootsharp handles the runtime bridge: import the generated package, boot the package `bin/` directory, then call `Motely`.

```ts
import bootsharp, { Motely } from "motely-wasm";

await bootsharp.boot("/motely-wasm/bin");
const status = Motely.validateJaml(jaml);
```

```tsx
import { useSearch, useJamlLibrary } from "jaml-ui/motely";

// result: { seed: string, score: number, tallyColumns?: number[] }
```

`useJamlLibrary` wires Bootsharp.FileSystem when `@rewaffle/bootsharp-file-system` is available, letting browser users pick a local JAML folder and read/write files through Motely's generated WASM API:

```tsx
const library = useJamlLibrary();
await library.mount();

const source = await library.loadFile(library.files[0]);
await library.saveFile("filters/example.jaml", source);
```

## Peer dependencies

| Peer | Required for |
| ---- | ------------ |
| `react`, `react-dom` | All components |
| `react-icons` | Components that render icons (optional peer) |
| `@rewaffle/bootsharp-file-system` | Optional JAML library folder mount support |
| `three`, `@react-three/fiber`, `@react-three/drei`, `@react-spring/three` | `jaml-ui/r3f` only |

`motely-wasm` is a direct dependency (currently `^17.7.0`) — it ships with `jaml-ui` rather than as a peer, but is externalized from the bundle so consumers control how the Bootsharp runtime is served (see *Search Worker Architecture* above).
