# motely-wasm

Browser-only WebAssembly package for Motely Seed Oracle. Use in the browser, not in Node.

## Installation

```bash
npm install motely-wasm
```

## Runtime asset setup

This package ships two runtime folders:

1. **`_framework`** for the threaded runtime
2. **`_framework_st`** for the bundled single-thread runtime

Your app must serve those folders somewhere the browser can fetch them.

If you call `loadMotely()` without a `baseUrl`, the package loads its bundled runtime assets package-relatively.

If you copy the assets into your own public/static folder, pass an explicit `baseUrl` that points to the correct runtime folder.

### Threaded runtime requirements

For multi-threading, serve **`_framework`** and set COOP/COEP headers on **all** responses. Without these, the browser disables `SharedArrayBuffer` and threaded WebAssembly.

```
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Embedder-Policy: require-corp
```

### Single-thread runtime

If you want to force the single-thread runtime, serve **`_framework_st`** and call `loadMotely({ threads: "off", baseUrl: "/your-runtime-path" })`.

### Hosting examples

If you are manually hosting the runtime assets, common setups look like this:

- copy `node_modules/motely-wasm/_framework` to `public/_framework`
- copy `node_modules/motely-wasm/_framework_st` to `public/_framework_st`
- use `loadMotely({ threads: "auto", baseUrl: "/_framework" })` for threaded/prod hosting
- use `loadMotely({ threads: "off", baseUrl: "/_framework_st" })` for forced single-thread hosting

Set COOP/COEP headers on **all** responses when using the threaded runtime:
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

Then call `loadMotely()` (auto-detects threads and falls back to the bundled single-thread runtime when isolation is unavailable), `loadMotely({ threads: "auto", baseUrl: "/your-threaded-runtime" })`, or `loadMotely({ threads: "off", baseUrl: "/your-single-thread-runtime" })`.

## Usage

```typescript
import { loadMotely } from "motely-wasm";

const api = await loadMotely();
const version = api.getVersion();
const result = api.analyzeSeed("TACO1111", "Red", "White");

// Analyze seed with ante-by-ante breakdown
result.antes.forEach(ante => {
  console.log(`Ante ${ante.ante}: Boss=${ante.boss}, Draw order=${ante.drawOrder}`);
  ante.shopQueue.forEach(item => console.log(`  Shop: ${item.name}`));
});

// Search with JAML filter
const searchResult = await api.startJamlSearch(jamlContent, {
  threadCount: 4,       // auto-detected if omitted; defaults to processorCount
  batchCharCount: 4,    // default: 4 (1.5M seeds per batch, range 1-7)
  onProgress: (searched, matches, elapsed, count) => {
    console.log(`Searched: ${searched}, Matches: ${matches}`);
  },
  onResult: (seed, score) => {
    console.log(`Found: ${seed}`);
  }
});
```

**Batch size tuning:**
- `batchCharCount=4` (default): 1.5M seeds/batch, good balance between responsiveness and JS interop overhead
- `batchCharCount=3`: 175K seeds/batch, more responsive UI updates
- `batchCharCount=5`: 52M seeds/batch, fewer JS calls, less responsive

Optional custom base URL (e.g. CDN): `loadMotely({ baseUrl: "https://cdn.example/assets" })`.
Optional threading mode: `loadMotely({ threads: "auto" | "on" | "off" })` (default: auto).
- `auto`: use the threaded browser runtime when `crossOriginIsolated` is available, otherwise fall back to the bundled single-thread runtime
- `on`: require the threaded browser runtime
- `off`: force the bundled single-thread runtime

## JAML schema

The package includes the JAML JSON schema for validation and editor IntelliSense. Use the file at `node_modules/motely-wasm/jaml.schema.json` or resolve `motely-wasm/jaml.schema.json` (e.g. copy to your public dir or point your editor at it).

## License

MIT. See the [MotelyJAML repository](https://github.com/OptimusPi/Motely) for details.
