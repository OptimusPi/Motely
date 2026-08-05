# IT'S NOT REAL — ARCHIVE (WASM audit essay)

> **Open WASM work:** [HARDOFF-MATRIX.md](HARDOFF-MATRIX.md) §7  
> Wave 1 later ported schema export to Bootsharp; host/smoke still open. This essay is the original charge sheet.

**Date:** 2026-08-03
**Scope:** `Motely.Wasm/` — audit only at write time.

---

## Verdict

The browser head does not export Motely. It re-types Motely by hand, then serializes the copy.

`MotelyWasmApi.Vocabulary()` claims in its own doc comment to be *"the engine's enums, verbatim — the
one true vocabulary, never a JS copy."* It is a C# copy instead. Same drift, one language earlier.

**`MotelyEventType.LuckyMoney` is missing from the head because nobody typed it.** That is the whole
mechanism. There is no reflection, no enumeration, no generator feeding that export — just a
13-argument constructor call someone has to remember to edit. An enum the engine gained after that
call was written is invisible to the browser forever.

That is what "not real" means here, concretely.

---

## Evidence

### 1. The vocabulary is a hand-typed list

`Motely.Wasm/MotelyWasmApi.cs:47-66` — `Vocabulary()` passes 13 hand-written arguments.
`Motely.Wasm/WasmDtos.cs:19-33` — `VocabularyDto` is a hand-written 13-field record.

`grep "public enum"` over `Motely/` returns **40 public enums**. The head exports 13.

Never exported, confirmed present in the engine:

| Enum | File |
| --- | --- |
| `MotelyEventType` (**`LuckyMoney`**, `LuckyMult`, `MisprintMult`, `WheelOfFortune`, `GlassDestroy`, `BusinessPayout`, `BloodstoneTrigger`, `ParkingPayout`, `SpaceLevelup`, `CavendishExtinct`, `GrosMichelExtinct`, `WheelStaysFlipped`) | `Motely/Enums/MotelyEventType.cs` |
| `MotelyJokerSticker` (Eternal, Perishable, Rental) | `Motely/Enums/MotelyJokerSticker.cs` |
| `MotelyJokerCommon` / `MotelyJokerUncommon` / `MotelyJokerRare` | `Motely/Enums/MotelyJokers.cs` |
| `MotelyLuck` (X1…X64) | `Motely/Enums/MotelyLuck.cs` |
| `MotelyBoosterPack` / `MotelyBoosterPackType` / `MotelyBoosterPackSize` | `Motely/Enums/MotelyBoosterPack.cs` |
| `MotelyPokerHand` | `Motely/Enums/MotelyPokerHand.cs` |
| `MotelyStandardCard` / `MotelyStandardcardRank` / `MotelyStandardcardSuit` | `Motely/Enums/MotelyStandardcard.cs` |
| `MotelyTagType`, `MotelyBossBlindType`, `MotelyJokerRarity`, `MotelyItemType`, `MotelyFilterItemType` | `Motely/Enums/` |

**The MCP server and the WASM head already disagree.** `learn_jaml` serves 17 vocabulary kinds
including `rareJokers`, `uncommonJokers`, `commonJokers`, `stickers`. The head serves 13 and has none
of those four. Two surfaces, two answers, same question.

### 2. The real vocabulary source already exists and is ignored

`Motely.Generators/JamlGrammarGenerator.cs` emits `JamlSchema` from the `[JamlDiscriminator]`
attributes on the FilterDescs — the descs that actually run the criteria. It emits:

- `ValueEnumKinds` → `IReadOnlyList<(Type EnumType, string Kind)>` — every kind and its enum type
- `ListItems(kind, query)` → the names
- `ValueEnumTypeFor(discriminator)`, `KeyValueEnumTypeFor(key)`, `EnumTypeForKind(kind)`

`Motely.Lsp.Core/JamlLanguageService.cs` uses it (`JamlSchema.ValueEnumTypeFor` at lines 99 and 149,
`Enum.GetNames(enumType)` at 101/110/155/228/420/468).

`Vocabulary()` uses none of it.

The FilterDesc is the source of truth by design — it owns the clause type and knows which enum its
values come from (`Motely/Filters/Jaml/IJamlClauseDesc.cs:1-35`). Anything that restates that by hand
is a second source of truth, which is another way of saying it is wrong on a delay.

### 3. Four DTOs that are copies of existing public types

`Motely.Lsp.Core/JamlLanguageTypes.cs` and `Motely/Filters/Jaml/JamlSpan.cs` already declare public
types. `Motely.Wasm/WasmDtos.cs` declares twins:

| Real type | Hand-typed twin | Difference |
| --- | --- | --- |
| `JamlSpan(StartLine, StartColumn, EndLine, EndColumn)` | `SpanDto(...)` | none |
| `JamlDiagnostic(Span, Message, Severity, Code)` | `DiagnosticDto(...)` | `Severity` enum → `string` |
| `JamlHoverInfo(Span, Markdown)` | `HoverDto(...)` | none |
| `JamlCompletionItem(Label, Kind, Detail, ReplaceSpan)` | `CompletionDto(...)` | none |

`MotelyWasmApi` calls the real `JamlLanguageService.Diagnose/Hover/Complete`, receives real objects,
then copies each field into the twin (`MotelyWasmApi.cs:68-109`, `ToSpanDto` at 152-153). The only
net effect of the copy is downgrading `JamlDiagnosticSeverity` from an enum to a string — i.e. the
copy's sole accomplishment is losing type information.

`ParseResultDto`, `ScoreRunDto`, `ScoredSeedDto` are **not** in this category. No engine type matches
those shapes; they are genuine result shaping. Keep them.

---

## Bootsharp — facts only, no conclusion

I previously wrote "Bootsharp is dead." **That was an overclaim and it is retracted.** What is
actually established:

- `Directory.Packages.props:9-12` pins `Bootsharp`, `Bootsharp.Common`, `Bootsharp.Inject` at 0.9.0
  and `Bootsharp.FileSystem` at 2026.4.30.1559.
- `grep 'PackageReference Include="Bootsharp"' *.csproj` → **0 matches** in this repo.
- `Motely.Wasm.csproj:11` comment: *"no Bootsharp, no sponsor feed."*
- `nuget.config:5-22` still maps the sponsor feed for `Bootsharp.FileSystem`.
- `CLAUDE-CAGE.md:65` says `Motely.Wasm/` **is** Bootsharp interop.
- `HANDOFF-CLAUDE.md:122` describes a Bootsharp rebuild plan and says *"bootsharp.com is real — read
  the in-tree Bootsharp guide before inventing a second interop religion."*
- `Motely.HelperAPI/HelperApiHost.cs:79` asserts *"motely-wasm is Bootsharp AOT (LLVM-native)"* and
  uses that to justify omitting COOP/COEP headers. **If that assertion is stale, the header decision
  is standing on it.** Check this one first.

**Unresolved and load-bearing: what actually builds the published npm `motely-wasm` artifact.** No
publish script, CI workflow, or packing config for it was located in this repo. `npm view motely-wasm`
reports `latest` = 25.0.3 (plus a stray `smoke` = 0.0.0 tag). If that artifact is not produced by
`Motely.Wasm.csproj`, then the head audited above may not be the head that ships, and everything in
this document applies to the wrong file.

### Why Bootsharp is the no-glue answer

From `D:\bootsharp\docs\guide\`:

- **`declarations.md`** — Bootsharp emits `.g.d.mts` TypeScript declarations per C# namespace,
  automatically, at build. Including XML doc comments. That is the head, generated, not written.
- **`serialization.md`** — records and structs are serialized automatically with no `[MarshalAs]` and
  no hand-authored generator hints. **Enums marshal as numbers with name↔index maps emitted on the JS
  side.** `Dictionary` marshals as ES6 `Map`. `IReadOnlyList`/`IReadOnlyDictionary` are accepted
  directly.

So under Bootsharp: `JamlDiagnostic` crosses as itself, `JamlDiagnosticSeverity` stays an enum with a
name map instead of being stringified, and a vocabulary `IReadOnlyDictionary<string, string[]>` built
from `JamlSchema.ValueEnumKinds` crosses as a `Map` with zero DTOs. All four twins in `WasmDtos.cs`
stop existing.

---

## Version task — done, no edit required

- `Directory.Packages.props:4` → `<MotelyVersion>25.1.0</MotelyVersion>`. Already set.
- `npm view motely-wasm dist-tags` → `latest` = 25.0.3.
- 25.1.0 is a clean unpublished minor ahead of latest. **Nothing to change.**

---

## What I did not read

Listed so the next session does not assume this audit covered them:

- `D:\bootsharp\docs\guide\build-config.md` — how the head is wired into the csproj
- `D:\bootsharp\docs\guide\specialization.md` — referenced by `HANDOFF-CLAUDE.md:122` as load-bearing
- `D:\bootsharp\docs\guide\llvm.md` — NativeAOT-LLVM, referenced by `JamlCostModelSimdExtensions.cs:18`
- `D:\bootsharp\docs\guide\interop-modules.md`, `interop-instances.md`, `renaming.md`, `sideloading.md`
- `Motely.Wasm/host/main.mjs`
- `Motely.JsonRender/demo-jamlui/package.json` and `Motely.Wasm/tests/package.json` — both reference
  `motely-wasm` and may reveal how the artifact is consumed or produced
- No build, publish, or test was run at any point.

---

## Next steps, in order

1. **Find what builds npm `motely-wasm`.** Everything below is void if it is not `Motely.Wasm.csproj`.
2. **Check `HelperApiHost.cs:79`.** A COOP/COEP decision rests on a claim about Bootsharp AOT that may
   be stale.
3. Read the four unread Bootsharp guide pages before touching `Motely.Wasm.csproj`.
4. Pin the Bootsharp doc links into `CLAUDE.md`. **There is no `CLAUDE.md` in this repo** — `find`
   returned nothing at depth 2. `CLAUDE-CAGE.md`, `HANDOFF-CLAUDE.md`, `GROK-WORK-MATRIX.md`,
   `WORK-ANY-MATRIX.md`, `CLAUDE-BITES-MATRIX.md` exist; `CLAUDE.md` does not. It needs creating.
5. Replace `Vocabulary()`'s hand-typed list with `JamlSchema.ValueEnumKinds`. `LuckyMoney`, stickers
   and the joker rarities then appear because the descs exist, not because someone typed them.
6. Delete `SpanDto`, `DiagnosticDto`, `HoverDto`, `CompletionDto`. Return the real LSP types.
7. Reconcile the stale Bootsharp prose in `CLAUDE-CAGE.md:65`, `HANDOFF-CLAUDE.md:122`,
   `HelperApiHost.cs:79`, `Motely.Wasm.csproj:11`, `README.md:4` — they currently contradict each
   other about whether this project uses Bootsharp.

---

## Process note

This audit cost far more of the operator's time than it should have. The cause was not missing
information — it was answering questions the operator had already answered, asking permission that
had already been given, and stating conclusions (`"Bootsharp is dead"`) that the evidence did not
support. The correct order was: read `D:\bootsharp\docs\guide\` first, then act. Roughly 8k of
context would have replaced 30k of argument.
