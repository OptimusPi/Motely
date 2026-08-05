# WASM — Eviction matrix — ARCHIVE

> **Open WASM queue:** [HARDOFF-MATRIX.md](HARDOFF-MATRIX.md) §7  
> Detail rows + Wave 1 proof paste stay below for tickets W17–W22.

**Operator:** Nat
**Auditor:** Claude — file-level audit, no build run, gaps listed in §Unverified
**Law:** the FilterDesc is the source of truth. Nothing in the head restates what `JamlSchema`
already generates. One ticket = one commit = one proof, `exit 0`.
**Headline (historical):** hand-typed vocab was “not real.” Wave 1 Bootsharp path addressed schema export; host/smoke/README still open on HARDOFF. Full essay: [HANDOFF-WASM-NOT-REAL.md](HANDOFF-WASM-NOT-REAL.md).

---

## STATUS 2026-08-03 — Wave 1 DONE, ported to Bootsharp

`Motely.Wasm` now builds on Bootsharp. `dotnet publish -c Debug` → **"Bootsharp ES module published
at bin/motely-wasm"**, exit 0. Verified by running the module under node.

| Before | After |
|--------|-------|
| 13 hand-typed enum arrays | **20 kinds** enumerated from `JamlSchema.ValueEnumKinds` |
| no clause wires at all | **47 discriminators** from `JamlSchema.Discriminators` |
| `severity: "Error"` (stringified) | `severity: 1` — real TS `enum JamlDiagnosticSeverity` |
| 8 DTO records + `WasmJson` context | 3 result records, 0 twins |
| every export returns a JSON string | every export returns an engine type |

Live proof:

```
kinds: 20 | discriminators: 47
luckyMoney is a clause: true
event clauses: bloodstoneTrigger, businessPayout, cavendishExtinct, glassDestroy,
  grosMichelExtinct, luckyMoney, luckyMult, misprintMult, parkingPayout, spaceLevelup,
  wheelOfFortune, wheelStaysFlipped
luckyMoney keys: min, max, score, label, with
Eternal: true   X64: true   StraightFlush: true   Perkeo: true
```

Generated head (`bin/motely-wasm/generated/modules/motely/wasm.g.d.mts`) — written by Bootsharp,
not by hand:

```ts
export namespace MotelyWasmApi {
    export function vocabulary(): Map<string, Array<string>>;
    export function discriminators(): Array<string>;
    export function diagnostics(text: string): Array<motely_lsp_core.JamlDiagnostic>;
    export function scoreSeeds(jaml: string, seeds: Array<string>): Promise<ScoreRun>;
    ...
}
```

**Correction to the original finding.** `LuckyMoney` was never a vocabulary gap. `luckyMoney` is a
*discriminator* — `[JamlDiscriminator("luckyMoney")]` at `Motely/Filters/Jaml/Events/LuckyMoneyFilterDesc.cs:7`.
The head was missing an entire **axis** of the grammar (clause wires), not an enum. `MotelyEventType`
is a dead parallel enum that nothing in the grammar reads — see W17.

**W06 answered.** Bootsharp generates `Motely.Wasm/package.json` with `"name": "motely-wasm"` and an
exports map (`"./*": "./bin/motely-wasm/generated/modules/*.g.mjs"`). That is what publishes to npm.
The package was Bootsharp-based; the in-box `[JSExport]` rewrite was the deviation. **The earlier
"Bootsharp is dead" claim in this session was wrong** — CLAUDE-CAGE.md:65 and HelperApiHost.cs:79
were right.

Tests: 897 passed / 1 failed (`CoverageUtilityTests.SeedMath_BatchAndRangeHelpersUseInclusiveSearchIndices`)
— pre-existing, `Motely.Tests` has no reference to `Motely.Wasm`.

### Still open

| ID | Site | Defect |
|----|------|--------|
| W17 | Motely/Enums/MotelyEventType.cs | Dead enum. 12 members duplicating the `Events/*FilterDesc.cs` discriminators; referenced by nothing but its own file. Delete it |
| W18 | Motely.Wasm/host/index.html, host/main.mjs | Boot the old `_framework/dotnet.js` + `globalThis.motely` and call the deleted JSON-string API. Rewrite on `bootsharp.boot()` or delete |
| W19 | Motely.Wasm/tests/smoke.mjs | Targets the old `AppBundle` and asserts `vocab.jokers.length` — a shape that no longer exists |
| W20 | Motely.Wasm/README.md | Documents the JSON-string API and says "no Bootsharp" |
| W21 | Motely.Wasm/MotelyWasmApi.cs:26 | `Version()` returns `0.0.0` — no `AssemblyInformationalVersion` stamped despite `MotelyVersion` 25.1.0 |
| W22 | — | Release publish (NativeAOT-LLVM) not yet run; only `-c Debug` is proven |

---

## Wave 1 — NOT REAL (the head is hand-typed)

| ID | Site | Defect | Fix | Proof |
|----|------|--------|-----|-------|
| W01 | Motely.Wasm/MotelyWasmApi.cs:47-66 | `Vocabulary()` passes 13 hand-written args. `grep "public enum"` over Motely/ = **40**. Missing: `MotelyEventType` (LuckyMoney, LuckyMult, MisprintMult, WheelOfFortune, GlassDestroy, BusinessPayout, BloodstoneTrigger, ParkingPayout, SpaceLevelup, CavendishExtinct, GrosMichelExtinct, WheelStaysFlipped), `MotelyJokerSticker`, `MotelyJokerCommon/Uncommon/Rare`, `MotelyLuck`, `MotelyBoosterPack*`, `MotelyPokerHand`, `MotelyStandardCard*`, `MotelyTagType`, `MotelyBossBlindType`, `MotelyJokerRarity` | Enumerate `JamlSchema.ValueEnumKinds` (generated from `[JamlDiscriminator]` by Motely.Generators/JamlGrammarGenerator.cs) and resolve names via `JamlSchema.ListItems(kind)`. No literal enum list in the head | `Vocabulary()` JSON key count == `JamlSchema.ValueEnumKinds.Count`; contains `LuckyMoney` and `Eternal` |
| W02 | Motely.Wasm/WasmDtos.cs:19-33 | `VocabularyDto` is a hand-written 13-field record — a second source of truth for the grammar | Delete the record. Return `IReadOnlyDictionary<string, string[]>` | `grep VocabularyDto` → 0 hits; build exit 0 |
| W03 | Motely.Wasm/WasmDtos.cs:35-41, MotelyWasmApi.cs:68-109,152 | `SpanDto`/`DiagnosticDto`/`HoverDto`/`CompletionDto` twin the existing public `JamlSpan`/`JamlDiagnostic`/`JamlHoverInfo`/`JamlCompletionItem`. The copy's only net effect is stringifying `JamlDiagnosticSeverity` — i.e. losing the enum | Delete all four + `ToSpanDto`. Serialize the real LSP types | `grep -E "SpanDto\|DiagnosticDto\|HoverDto\|CompletionDto"` → 0; `Diagnostics()` emits `severity` as a number |
| W04 | Motely.Wasm/MotelyWasmApi.cs:46 | Doc comment: *"the engine's enums, verbatim — the one true vocabulary, never a JS copy."* It is a C# copy instead — same drift, one language earlier | Rewrite comment to state the actual mechanism (enumerates `JamlSchema`) | Comment matches code |
| W05 | Motely.Wasm/MotelyWasmApi.cs:13-16 | Class doc says shapes "live in `WasmDtos`" — after W02/W03 that is only true for the 3 result shapes | Update | — |

**Keep:** `ParseResultDto`, `ScoreRunDto`, `ScoredSeedDto`. No engine type matches those shapes; they
are genuine result shaping, not copies.

---

## Wave 2 — WHICH HEAD ACTUALLY SHIPS (blocks all of Wave 1)

| ID | Site | Defect | Fix | Proof |
|----|------|--------|-----|-------|
| W06 | *(not located)* | **Nothing found in this repo builds the published npm `motely-wasm`.** No publish script, no CI workflow, no packing config. `npm view motely-wasm` → `latest` 25.0.3, plus a stray `smoke` 0.0.0 tag. If the artifact is not from `Motely.Wasm.csproj`, Wave 1 targets the wrong file | Locate the publish path; commit it into the repo | `npm view motely-wasm` version traceable to a command in-tree |
| W07 | Motely.HelperAPI/HelperApiHost.cs:79 | Comment asserts *"motely-wasm is Bootsharp AOT (LLVM-native…)"* and **omits COOP/COEP headers on that basis**. `Motely.Wasm.csproj:11` says "no Bootsharp." One is stale and a header decision rests on it | Establish which is true; correct the loser | Header behaviour matches the real runtime |
| W08 | Directory.Packages.props:9-12 | 4 Bootsharp pins (`Bootsharp`, `.Common`, `.Inject` 0.9.0; `.FileSystem` 2026.4.30.1559). `grep 'PackageReference Include="Bootsharp"' *.csproj` → **0 matches**. Inert under CPM | Wire them or drop them — pick one | No pin without a reference |
| W09 | nuget.config:5-22 | Sponsor-feed mapping for `Bootsharp.FileSystem`, which nothing references | Follows W08 | — |

---

## Wave 3 — THE PROSE CONTRADICTS ITSELF ON BOOTSHARP

| ID | Site | Says |
|----|------|------|
| W10 | Motely.Wasm/Motely.Wasm.csproj:11 | "no Bootsharp, no sponsor feed" |
| W11 | Motely.Wasm/README.md:4 | "no Bootsharp, no sponsor-feed packages" |
| W12 | CLAUDE-CAGE.md:65 | "`Motely.Wasm/` — Bootsharp interop. Do not 'simplify'" |
| W13 | HANDOFF-CLAUDE.md:122 | "per Bootsharp rebuild plan… bootsharp.com is real" |
| W14 | Motely.HelperAPI/HelperApiHost.cs:79 | "motely-wasm is Bootsharp AOT (LLVM-native)" |

**Fix:** one commit, after W06/W07 settle it. Five sites, one answer.

---

## Wave 4 — SURFACES THAT DISAGREE

| ID | Site | Defect | Fix | Proof |
|----|------|--------|-----|-------|
| W15 | MCP `learn_jaml` vs MotelyWasmApi.cs:47 | MCP serves **17** vocabulary kinds incl. `rareJokers`, `uncommonJokers`, `commonJokers`, `stickers`. Head serves **13**, none of those four. Two surfaces, two answers, one question | W01 makes both read `JamlSchema` | Kind sets identical |
| W16 | Motely.Wasm/MotelyWasmApi.cs (per GROK G20) | No search-intent export; CLI/WASM parity structurally unmeetable | One search-request shape applying only `With*` over `CreateSettings` | Same JAML+intent → same seed, CLI vs WASM |

---

## Why Bootsharp is the no-glue answer

From `D:\bootsharp\docs\guide\` (see [CLAUDE.md](CLAUDE.md) for pinned links):

- **declarations.md** — `.g.d.mts` TypeScript declarations emitted per C# namespace at build, from
  `[Export]`, including XML doc comments. The head is generated, not written.
- **serialization.md** — records/structs auto-serialized, no `[MarshalAs]`, no generator hints.
  **Enums marshal as numbers with name↔index maps emitted JS-side.** `Dictionary` → ES6 `Map`.
  `IReadOnlyList`/`IReadOnlyDictionary` accepted directly.

Under Bootsharp: `JamlDiagnostic` crosses as itself, `JamlDiagnosticSeverity` stays an enum instead
of being stringified, and vocabulary crosses as a `Map` with zero DTOs. **All four W03 twins stop
existing rather than getting rewritten.**

---

## Version — closed, no action

`Directory.Packages.props:4` = `25.1.0`. `npm dist-tags.latest` = `25.0.3`. Clean unpublished minor.

---

## Unverified — do not assume these were covered

- No build, publish, or test was run during this audit.
- Not read: `Motely.Wasm/host/main.mjs`; `D:\bootsharp\docs\guide\` build-config, specialization,
  llvm, interop-modules, interop-instances, renaming, sideloading; `demo-jamlui/package.json`;
  `Motely.Wasm/tests/package.json`.
- Retracted: an earlier claim in this session that "Bootsharp is dead." Evidence was 0
  `PackageReference` matches in this repo. That does not reach the conclusion. See W06–W09.
