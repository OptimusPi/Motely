# Make JAML Real — Implementation Plan

**Goal:** JAML's vocabulary and name-validation are *derived from the C# enums at compile
time*. No `--vocab` runtime flag. No WASM needed to define the language. One source of truth
(`Motely/Enums`), zero schema drift — structurally impossible to forget to sync.

**The core idea:** "what names are valid in JAML" is currently produced by *running the app*
(`--vocab` → `vocabulary.json`). That makes the language a runtime printout. We invert it: a
Roslyn **source generator** reads the enum declarations while `Motely` compiles and emits the
vocabulary as a build artifact. Add a joker to the enum → the next build already knows it.

---

## Source of truth

The enums already are the truth; we just make the relationship explicit and machine-read:

- `Motely/Enums/MotelyJokers.cs`, `MotelyVoucher.cs`, `MotelyTarotCard.cs`,
  `MotelySpectralCard.cs`, `MotelyPlanetCard.cs`, `MotelyTag.cs`, `MotelyBossBlind.cs`,
  `MotelyStandardcard.cs` (ranks/suits), `MotelyDeck.cs`, `MotelyStake.cs`,
  `MotelyItemEdition.cs`, `MotelyItemEnhancement.cs`, `MotelyItemSeal.cs`,
  `MotelyJokerSticker.cs`
- Discriminator set: the clause types in `JamlClause.cs` → `CreateDesc` switch.

---

## Phase 1 — Annotate the truth
Add a tiny marker attribute so "this enum is part of the JAML language" is declared in code,
not guessed by the generator.

- New: `Motely/Filters/Jaml/JamlVocabAttribute.cs`
  `[AttributeUsage(AttributeTargets.Enum)] public sealed class JamlVocabAttribute(string category)`
- Annotate each vocab enum: `[JamlVocab("joker")]`, `[JamlVocab("voucher")]`, …

**Acceptance:** every enum that feeds JAML carries exactly one `[JamlVocab]`; build still green.

## Phase 2 — The source generator
- New project: `Motely.Jaml.Generator/` (`netstandard2.0`, `IsRoslynComponent`,
  references `Microsoft.CodeAnalysis.CSharp`). An **incremental** generator.
- Finds all `[JamlVocab]` enums via the semantic model; reads their members.
- Emits `JamlVocabulary.g.cs` into the `Motely` compilation:
  - `public static class JamlVocabulary` with `IReadOnlyList<string>` per category,
  - a `public const string Json` — the whole vocabulary serialized at generation time,
  - the discriminator list (sourced from the `CreateDesc` clause types).

**Acceptance:** `dotnet build` produces `JamlVocabulary.g.cs`; `JamlVocabulary.Json` is
non-empty and contains a known sentinel (e.g. `Blueprint`, `Perkeo`). No app run involved.

## Phase 3 — Delete the kludge
- Remove the `--vocab` CLI entry point. The vocabulary is now a compile-time constant, not a
  debug-flag dump. Nothing in the engine reaches it by running the app.

**Acceptance:** `--vocab` is gone; build + existing tests green.

## Phase 4 — Bridge to the TS tooling (no app run, no WASM)
- MSBuild target `EmitJamlVocabularyJson` (after `Compile`) in `Motely.csproj` writes the
  generated vocabulary to `jaml-lang/src/vocabulary.generated.json`, committed.
- This is a *build step over compiled source*, never the application runtime, never WASM.

**Acceptance:** editing an enum + `dotnet build` updates `vocabulary.generated.json`
automatically; the file is byte-stable across rebuilds with no enum change.

## Phase 5 — Rebuild jaml-lang as a thin consumer
- jaml-lang (rebuilt clean) imports `vocabulary.generated.json` directly. `validateNames()`
  validates against the generated set. The TS package maintains *no* hand-written name lists.

**Acceptance:** a JAML file with `joker: Blueprint` validates clean; `joker: Blueprnt`
fuzzy-suggests `Blueprint`; a name added to the C# enum is accepted with no TS edit.

## Phase 6 — Make drift fail CI
- Test in `Motely.Tests`: assert every discriminator returned by `CreateDesc` has a
  `JamlVocabulary` category, and `JamlVocabulary.Json` round-trips. A new clause with no
  vocabulary entry fails the build.

**Acceptance:** deliberately adding a clause type with no vocab category turns the test red.

---

## Done = all true
1. No `--vocab`; no WASM in the language-definition path.
2. Add an enum member → rebuild → both the C# engine and `vocabulary.generated.json` have it,
   with zero manual steps.
3. jaml-lang holds no hand-maintained name lists.
4. A clause without a vocabulary entry fails CI.
5. `dotnet build Motely.slnx` and `dotnet test` green throughout.
