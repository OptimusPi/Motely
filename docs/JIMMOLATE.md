# Jimmolate — build & run guide

> The single-seed mental model from **Immolate**, bridged into **Motely**'s SIMD
> search. Write plain scalar code that runs *per seed* — but only on the seeds the
> vectorized base filter already let through.

## What it is

Motely's base filters are SIMD: they score many seeds per lane at once. That's fast,
but awkward when you just want to *think about one seed at a time* the way Immolate
let you — "give me the seed, let me write normal code against it."

**Jimmolate is that bridge.** It runs a scalar single-seed predicate
(`MotelyIndividualSeedSearcher`) as an *additional* filter, but only on the survivors
of the SIMD base filter. The base filter narrows the field with vectors; Jimmolate
runs your hand-written logic once per surviving seed — not once per lane.

- `Motely/Filters/Native/JimmolateFilterDesc.cs` — the additional filter. Its
  `Filter` calls `ctx.SearchIndividualSeeds(searcher)`.
- `MotelySearchSettings.WithJimmolate()` (`Motely/MotelySearch.cs`) — attaches it.
  Throws if no searcher is registered (`MotelyWasmInterop.JimmolateSearcher`).
- `Motely.Tests/JimmolateFilterDescTests.cs` — proves it runs once per base
  survivor and matches a control filter.

Two ways to supply the predicate:

| Surface | Predicate | Entry point |
| --- | --- | --- |
| **Native C#** | `MotelyIndividualSeedSearcher` delegate | `WithJimmolate()` |
| **WASM / JS** | `Motely.jimmolateProbe = (seed, deck, stake) => bool` | `Motely.enableJimmolate()` |

The JS surface is the Immolate experience: just write a function against the seed.

## Prerequisites (Windows)

- **.NET SDK `10.0.202`** — pinned exactly in `global.json` (`rollForward: disable`).
  `dotnet --version` must print `10.0.202`.
- **Node.js 18+** — runs the `.mjs` publish-gate / demo scripts and
  `finalize-package.mjs`.
- **WASM path only:** the `wasm-tools` workload — `dotnet workload install wasm-tools`
  (NativeAOT-LLVM for the `browser-wasm` RID).
- **WASM path only:** Bootsharp packages. As of **0.8.0 (stable)** the three core
  packages — `Bootsharp`, `Bootsharp.Common`, `Bootsharp.Inject` — restore straight
  from nuget.org. No more alpha-local-feed dance. **`Bootsharp.FileSystem` is the
  sponsor extension** and is *not* on nuget.org — add the Rewaffle GitHub Packages
  NuGet source authenticated with your sponsor PAT (already configured on your box;
  see `AGENTS.md`). Versions are pinned in `Directory.Packages.props`.

> **Note on the pins.** `Directory.Packages.props` may still pin `0.8.0-alpha.415`
> for the core packages. Bumping those to `0.8.0` is a real change that must clear
> the publish gate below — do it on your machine where the sponsor feed and
> NativeAOT-LLVM toolchain exist, then re-run the gate. Don't take an unverified bump.

## Build & run

### Native path (no Bootsharp, no WASM)

This is the fastest way to exercise Jimmolate — the C# delegate surface and the test
that proves the bridge.

```sh
dotnet build Motely.slnx -c Release
dotnet test Motely.Tests/Motely.Tests.csproj --filter JimmolateFilterDescTests
```

`TreatWarningsAsErrors` is on across the repo — warnings fail the build. Fix, don't
suppress.

### WASM path (the JS "code against the seed" surface)

```sh
# 1. Build the npm package (outputs to ../motely-wasm/dist, embedded base64 WASM)
dotnet publish Motely.Wasm -c Release

# 2. Publish gate — both must pass
node Motely.Wasm/motely.test.mjs          # expect: RESULT: PASS
node Motely.Wasm/pack-consumer-smoke.mjs  # publish -> npm pack -> fresh install -> boot

# 3. Live Jimmolate demo (hand-written JS predicate over a seed list)
node Motely.Wasm/tests/jimmolate-demo.mjs
```

## Using Jimmolate from JS

The predicate must be bound **before boot** — it's a Bootsharp `[Import]`, so the
runtime needs it present when the module initializes. Then enable, then search.

```js
import bootsharp, { Motely } from "motely-wasm"; // or ./motely-wasm/dist/index.mjs

// 1. Bind your scalar predicate BEFORE boot. Plain code against the seed.
Motely.jimmolateProbe = (seed, deck, stake) => seed.startsWith("PI");

// 2. Boot, then enable Jimmolate (registers the probe as the searcher).
await bootsharp.boot();
Motely.enableJimmolate();

// 3. Search. When a probe is registered, WithJimmolate() is applied automatically:
//    the base filter passes seeds through, your probe does the culling.
const matches = [];
const onMatch = (s) => matches.push(s);
Motely.onSeedMatch.subscribe(onMatch);
try {
  const r = Motely.runPassthroughListSearch(["PIFREAK1", "PIAAAAAA", "XYZABCDE"]);
  console.log("matched:", matches, "of", Number(r.totalSeedsSearched));
} finally {
  Motely.onSeedMatch.unsubscribe(onMatch);
}
```

See `Motely.Wasm/tests/jimmolate-demo.mjs` for a runnable version and
`Motely.Wasm/tests/jimmolate.test.mjs` for the asserted behavior (probe runs once per
base survivor; matches are exactly the seeds the probe returned `true` for).

## Troubleshooting

- **`Jimmolate searcher is not registered.`** — you called a search path that uses
  `WithJimmolate()` without binding a probe. Native: pass a `MotelyIndividualSeedSearcher`.
  JS: set `Motely.jimmolateProbe` *before* `boot()`, then call `Motely.enableJimmolate()`.
- **`dotnet --version` isn't `10.0.202`** — `rollForward: disable` means it won't fall
  forward. Install the exact SDK.
- **WASM restore fails on `Bootsharp.FileSystem`** — the sponsor GitHub Packages source
  isn't configured / PAT expired. The three core `Bootsharp` packages come from
  nuget.org; only `Bootsharp.FileSystem` needs the sponsor feed.
- **`motely.test.mjs` can't find the entry** — it defaults to `../motely-wasm/dist/index.mjs`.
  Override with `MOTELY_WASM_ENTRY=/abs/path/to/dist/index.mjs`.
