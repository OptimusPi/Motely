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

Dev: `_framework` is served at `/_framework`, headers set. Build: `_framework` and `_framework_nt` are copied into `dist/`.

### Next.js

```js
// next.config.mjs (or .js)
import withMotelyWasm from "motely-wasm/next-plugin";

export default withMotelyWasm({
  // your existing next config
});
```

On first run the plugin copies `_framework` and `_framework_nt` into `public/` and sets COOP/COEP. No manual copy, no recurring setup.

**Turbopack:** The way `loadMotely()` loads the WASM runtime does not work with Next.js when Turbopack is enabled. Use the default webpack bundler (do not enable `--turbo` / `turbo: true`) when using motely-wasm.

### Other frameworks (SvelteKit, Astro, Remix, static host, etc.)

No plugin needed. Do the same one-time setup in your stack:

1. **Serve `_framework`**: copy `node_modules/motely-wasm/_framework` into your static/public folder, or configure your dev/server to serve that folder at `/_framework`.
2. **Set COOP/COEP headers** on **all** responses. Without these, the browser silently disables SharedArrayBuffer and multi-threading — your search will run single-threaded without any error:
   ```
   Cross-Origin-Opener-Policy: same-origin
   Cross-Origin-Embedder-Policy: require-corp
   ```

#### Server config examples

**Netlify / Cloudflare Pages** — copy `node_modules/motely-wasm/_headers` to your site root, or add to your own `_headers`:
```
/*
  Cross-Origin-Opener-Policy: same-origin
  Cross-Origin-Embedder-Policy: require-corp
```

**Nginx:**
```nginx
add_header Cross-Origin-Opener-Policy "same-origin" always;
add_header Cross-Origin-Embedder-Policy "require-corp" always;
```

**Apache (.htaccess):**
```apache
Header set Cross-Origin-Opener-Policy "same-origin"
Header set Cross-Origin-Embedder-Policy "require-corp"
```

**IIS (web.config):**
```xml
<system.webServer>
  <httpProtocol>
    <customHeaders>
      <add name="Cross-Origin-Opener-Policy" value="same-origin" />
      <add name="Cross-Origin-Embedder-Policy" value="require-corp" />
    </customHeaders>
  </httpProtocol>
</system.webServer>
```

**Node/Express:**
```js
app.use((req, res, next) => {
  res.setHeader('Cross-Origin-Opener-Policy', 'same-origin');
  res.setHeader('Cross-Origin-Embedder-Policy', 'require-corp');
  next();
});
```

**Verify:** open DevTools Console and run `self.crossOriginIsolated` — must return `true`.

Then call `loadMotely()` (auto-detects threads) or `loadMotely({ baseUrl: "/your/path" })` if you used a different path.

## Usage

```typescript
import { loadMotely } from "motely-wasm";

const api = await loadMotely();
const version = api.getVersion();
const result = api.analyzeSeed("TACO1111", "Red", "White");
```

Optional custom base URL (e.g. CDN): `loadMotely({ baseUrl: "https://cdn.example/assets" })`.
Optional threading mode: `loadMotely({ threads: "auto" | "on" | "off" })` (default: auto).

## JAML schema

The package includes the JAML JSON schema for validation and editor IntelliSense. Use the file at `node_modules/motely-wasm/jaml.schema.json` or resolve `motely-wasm/jaml.schema.json` (e.g. copy to your public dir or point your editor at it).

## License

MIT. See the [MotelyJAML repository](https://github.com/OptimusPi/Motely) for details.
