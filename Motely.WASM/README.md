# Motely.WASM

MotelyJAML WebAssembly build.

## Dev

```bash
npm run dev
```
Opens http://localhost:3333

## API

```js
await MotelyWasm.AnalyzeSeed(seed, deck, stake, minAnte, maxAnte, optionsJson)
await MotelyWasm.SearchSeeds(jamlJson, seedList, threads)
await MotelyWasm.ValidateJaml(jamlString)
```

## Headers

Server needs for multi-threading:
```
Cross-Origin-Embedder-Policy: require-corp
Cross-Origin-Opener-Policy: same-origin
```
