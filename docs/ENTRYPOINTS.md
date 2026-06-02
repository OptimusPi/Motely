# MotelyJAML — entry points & how to run them

Every way into the engine, with copy-paste commands. Run from the repo root
(`X:\BalatroSeedOracle\src\MotelyJAML`). Shell is **PowerShell**.

> The engine is a *library* (`Motely`). You drive it through one of the heads
> below. `Motely.DataLake` is a support library (DuckDB persistence), not a head.

---

## 1. `Motely.CLI` — the headless searcher

```powershell
dotnet run -c Release --project Motely.CLI/Motely.CLI.csproj -- <options>
```

Pick **what to run** (one of):

| Flag | Runs | Example |
| --- | --- | --- |
| `--native <Name>` | a built-in C# filter (see registry below) | `--native TwoBlackHole` |
| `--jaml <file\|inline>` | a JAML filter | `--jaml JamlFilters/Canio.jaml` |
| `--analyze <SEED[,SEED…]>` | JAMLyzer per-seed analysis | `--analyze ALEEB` |
| `--jamlyzer` | curated-list analysis (`seeds:` in JAML or `--seeds`) | `--jamlyzer --seeds ALEEB,1IDAD111` |

Pick **which seeds to feed it** (default = sequential sweep of the whole space):

| Mode | Flags |
| --- | --- |
| Sequential (default) | `--startBatch N --endBatch N`, or `--startPercent PCT`, or `--startSeed S --stopSeed S` |
| Exact list | `--seeds 1IDAD111,FCTU2111` |
| Keyword | `--keyword OOB` / `--keywords OW,OH,BOOB` (+ `--padding 67Z`) |
| Random sample | `--random 1000000` (or `--native-random N` in `--native` mode) |
| Named source | `--source <name_or_path>` |
| Aesthetic | `--aesthetic <name>` |

Common knobs: `--deck Red`, `--stake White`, `--threads 16`, `--batchCharCount 4`,
`--cutoff <score>`, `-q`/`--quiet`.

Output: results go to stdout. `--output-json` (NDJSON), `--save-seeds` (write hits
back into the JAML's `seeds:`), `--drown` (persist to DuckDB), `--results-path <path>`.

**Quick correctness check** (run your filter against known seeds, no full sweep):
```powershell
dotnet run -c Release --project Motely.CLI/Motely.CLI.csproj -- --native TwoBlackHole --seeds 1IDAD111,FCTU2111
```

---

## 2. `Motely.TUI` — interactive terminal UI

```powershell
dotnet run -c Release --project Motely.TUI/Motely.TUI.csproj
```
Terminal.Gui front-end for building/running searches without remembering flags.

---

## 3. `Motely.Wasm` → `motely-wasm` (browser / JS)

Build the npm package (emits to `../motely-wasm/dist`, base64-embedded WASM):
```powershell
dotnet publish Motely.Wasm -c Release
```
Then from JS — boot, then search / analyze / Jimmolate:
```js
import bootsharp, { Motely } from "motely-wasm";
Motely.jimmolateProbe = (seed, deck, stake) => seed.startsWith("PI"); // bind BEFORE boot
await bootsharp.boot();
Motely.enableJimmolate();
const r = Motely.runPassthroughListSearch(["PIFREAK1", "XYZABCDE"]);
```
See `docs/JIMMOLATE.md` for the full JS surface and the publish gate
(`node Motely.Wasm/motely.test.mjs`).

---

## 4. `Motely.Tests` — the only test project

```powershell
dotnet test Motely.Tests/Motely.Tests.csproj
dotnet test Motely.Tests/Motely.Tests.csproj --filter "FullyQualifiedName~JamlyzerUnitTests"
```
`JamlyzerUnitTests` / `JimmolateFilterDescTests` hold **known-seed ground-truth**
assertions — the right place to prove a new filter is correct.

---

## Built-in native filters (the `--native <Name>` registry)

Defined in `Motely/MotelyNativeFilter.cs` (enum + parser + factory):

`PerkeoObservatory`, `Observatory`, `Trickeoglyph`, `NaturalNegatives`,
`NegativePerkeo`, `NegativeCopy`, `ShuffleFinder`, `ErraticFinder`, `FilledSoul`,
`LuckyCard`, `NanSeed`, `NegativeTag`, `TwoBlackHole`.

**To add one:** write a `…FilterDesc` in `Motely/Filters/Native/`, then register it in
all four spots in `MotelyNativeFilter.cs` — the `enum`, `DisplayNames`, `TryParse`,
and the `CreateSettings` switch. (No reflection — AOT-safe by design.) That's exactly
how `TwoBlackHole` was wired in.

---

## Build vs run

- **Compile-check everything:** `dotnet build Motely.slnx -c Release` (warnings are
  errors).
- **Desktop AOT publish:** `dotnet publish Motely.CLI/Motely.CLI.csproj -c Release -r win-x64 -p:EnableCliAot=true`
  — ⚠️ requires the VC++ build tools installed, and currently trips trim warnings from
  `DuckDB.NET.Data` / `McMaster.Extensions.CommandLineUtils`. Plain (JIT) self-contained
  publish works without that toolchain:
  `dotnet publish Motely.CLI/Motely.CLI.csproj -c Release -r win-x64 --self-contained`.
