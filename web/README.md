# web · soft ui 🫥

A dumb, soft (neumorphic) front-end for Motely. Author a JAML filter, pick a
deck + stake, squish the big button, watch scored seeds pillow in.

It's a single self-contained `index.html` — no build, no bundler, no deps.

## Run it

Any static server works (ES modules need `http://`, not `file://`):

```sh
cd web
python3 -m http.server 8080
# → http://localhost:8080
```

## Live vs demo

On load it tries to import the real SIMD engine from `../motely-wasm/dist/`:

- **engine: live** — the dist is built ([`motely-wasm`](../motely-wasm/README.md)),
  so searches run the actual vectorized Motely engine in WASM. Searches block
  the main thread (no Web Worker here — it's a toy UI), so keep the seed count
  modest.
- **demo mode** — the dist isn't built, so a tiny deterministic stand-in emits
  fake scored seeds. The buttons still squish; nothing real is searched.

Build the engine to go live:

```sh
cd motely-wasm && npm run build
```

Bounded by design — it only ever runs a random sample, never the full sweep.
