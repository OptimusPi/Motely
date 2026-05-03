# jaml-ui

React components, UI tokens, sprites, and utilities for Balatro/JAML apps.

## Install

```bash
npm install jaml-ui react react-dom
```

## Package exports

| Entry | Contents |
|-------|----------|
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

## Asset handling

By default sprites resolve from the package `assets/` directory via `import.meta.url`.

Override at app startup:

```ts
import { setJamlAssetBaseUrl, clearJamlAssetBaseUrl } from "jaml-ui";

setJamlAssetBaseUrl("/vendor/jaml-ui/");
clearJamlAssetBaseUrl();                   // back to default
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

The library provides `useAnalyzer` to interact with `motely-wasm`'s search context natively.
Ensure `motely-wasm` is imported and booted at the module level in your application:

```tsx
import { boot } from "motely-wasm";
boot(); // Call early in your application lifecycle
```

## Peer dependencies

| Peer | Required for |
|------|-------------|
| `react`, `react-dom` | All components |
| `motely-wasm ^14.3.3` | `jaml-ui/motely`, `AnalyzerExplorer`, `useAnalyzer` data |
| `three`, `@react-three/fiber`, `@react-three/drei`, `@react-spring/three` | `jaml-ui/r3f` only |
