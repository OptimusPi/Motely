# motely-wasm

Browser-only WebAssembly package for Motely Seed Oracle. Use in the browser, not in Node.

## Installation

```bash
npm install motely-wasm
```

## One-time setup (no recurring chores)

Use the plugin for your bundler. It serves/copies `_framework` and sets the required COOP/COEP headers. After this, you never copy files or touch headers again.

### Vite

```js
// vite.config.js
import { defineConfig } from "vite";
import motelyWasm from "motely-wasm/vite-plugin";

export default defineConfig({
  plugins: [motelyWasm()],
});
```

Dev: `_framework` is served at `/_framework`, headers set. Build: `_framework` is copied into `dist/_framework`.

### Next.js

```js
// next.config.mjs (or .js)
import withMotelyWasm from "motely-wasm/next-plugin";

export default withMotelyWasm({
  // your existing next config
});
```

On first run the plugin copies `_framework` into `public/_framework` and sets COOP/COEP. No manual copy, no recurring setup.

### Other frameworks (SvelteKit, Astro, Remix, static host, etc.)

No plugin needed. Do the same one-time setup in your stack:

1. **Serve `_framework`** at `/_framework` (or any path): copy `node_modules/motely-wasm/_framework` into your static/public folder, or configure your dev/server to serve that folder at `/_framework`.
2. **Set headers** on the page (and optionally on `/_framework/*`): `Cross-Origin-Opener-Policy: same-origin`, `Cross-Origin-Embedder-Policy: require-corp`.

Then call `loadMotely()` (defaults to `/_framework`) or `loadMotely({ baseUrl: "/your/path" })` if you used a different path.

## Usage

```typescript
import { loadMotely } from "motely-wasm";

const api = await loadMotely();
const version = api.GetVersion();
const result = api.AnalyzeSeed("TACO1111", "Red", "White", 1, 8, "{}");
```

Optional custom base URL (e.g. CDN): `loadMotely({ baseUrl: "https://cdn.example/assets" })`. Default is `/_framework`.

## JAML schema

The package includes the JAML JSON schema for validation and editor IntelliSense. Use the file at `node_modules/motely-wasm/jaml.schema.json` or resolve `motely-wasm/jaml.schema.json` (e.g. copy to your public dir or point your editor at it).

## License

MIT. See the [Motely repository](https://github.com/OptimusPi/Motely) for details.
