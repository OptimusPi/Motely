# motely-wasm

Balatro seed search + per-seed analysis, powered by **JAML** (Jimbo's Ante Markup
Language). A vectorized SIMD seed-search engine ([Motely](https://github.com/OptimusPi/MotelyJAML))
compiled to WebAssembly via [Bootsharp](https://github.com/elringus/bootsharp).

The whole engine runs **client-side** in the browser (or Node) — no server, no GPU.

## Install

```bash
npm install motely-wasm
```

## Boot

```js
import bootsharp, { Motely } from "motely-wasm";

await bootsharp.boot();
```

WASM has no pthreads, so every `run*` call **blocks** the calling thread until the
search finishes. For a responsive UI, run it inside a Web Worker.

## JAML in, results out

```js
import bootsharp, { Motely } from "motely-wasm";
await bootsharp.boot();

const config = Motely.fromYaml(`
name: perkeo
deck: Red
stake: White
must:
  - joker: Perkeo
    max: 1
    sources: [SoulCard]
`);

// Stream results as they're found.
Motely.onProgress(p => console.log("progress", p));
Motely.onScoredResult(r => console.log("hit", r.seed, r.score));

// Random sample of 1000 seeds; or runSequentialSearch for a bounded batch sweep.
Motely.runRandomSearch(config, 1000);
```

## API surface (module `motely/wasm`)

- **Parse / inspect:** `fromYaml`, `fromJson`, `jamlToJson`, `jsonToJaml`,
  `explainJaml`, `createPlan`
- **Search:** `runSequentialSearch`, `runRandomSearch`, `runSeedListSearch`,
  `runAestheticSearch`, `runNativeListSearch`, `runPassthroughListSearch`,
  `nativeFilterNames`
- **Events:** `onProgress`, `onSeedMatch`, `onScoredResult`, `onFileChanges`
- **Jimmolate** (per-scored-seed JS predicate, the OG Immolate `filter(seed) => keep?`
  model): set `jimmolatePredicate` then `jimmolateEnabled = true`
- **File System Access** (Bootsharp.FileSystem): `pickRoot`, `mountRoot`,
  `unmountRoot`, `readTextFile`, `writeTextFile`

The generated TypeScript declarations under `dist/generated/modules/` are the
authoritative shape of every type and member.

## License

MIT — see [LICENSE](./LICENSE).
