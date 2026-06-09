# Jamlyzer — design

Status: design only. The old implementation was ripped (it was stacked on the legacy
string analyzer — wrong). This is the rebuild spec. Read `analyzer-design.md` first for
the two-analyzer split; this fleshes out the UI half.

## What it is

Jamlyzer takes ONE seed and a `JamlConfig` filter, walks everything the seed generates per
ante (shop queue, packs, tags, vouchers, boss, soul jokers — the "Card Sources"), and
returns a structured snapshot where every item the filter's clauses match is flagged
`IsHighlighted` + `MatchedBy`. The UI renders that content and glows the matches. That glow
— "this seed has Perkeo HERE, in ante 1 arcana pack slot 5" — is the product. It's for UX.
Rule #1.

Jamlyzer runs the filter in **highlight mode**: the clauses annotate the generated content
instead of gating it. Same filter the search runs, different job — search keeps/rejects
whole seeds, Jamlyzer marks where each clause landed on one seed.

It is NOT:
- a gate (highlight mode never rejects a seed; it annotates what the seed generates),
- the test oracle (that is `MotelyLegacyTextAnalyzer`, strings, untouched),
- implemented on top of the legacy analyzer (independent walk; see below).

## API surface

```
Jamlyzer.Analyze(JamlyzerOptions options) -> JamlyzerSnapshot

record JamlyzerOptions(string Seed, MotelyDeck Deck, MotelyStake Stake, JamlConfig Filter)
```

One entry point. `Filter` is the same compiled `JamlConfig` the search uses — so highlight
semantics CANNOT drift from search semantics (the whole point; see Highlighting).

## Snapshot shape (marshallable DTO — records, immutable, simple types only)

Crosses the Bootsharp WASM boundary to jaml-ui, so: records/readonly collections =
serialized by value (`serialization.md`). No ref structs, no engine handles, no
`MotelySingle*` types leak across.

```
record JamlyzerSnapshot(
    string Seed,
    string? Error,                       // non-null => walk failed; Antes empty
    IReadOnlyList<JamlyzerAnte> Antes)

record JamlyzerAnte(
    int Ante,
    JamlyzerItem? Boss,
    JamlyzerItem? Voucher,
    IReadOnlyList<JamlyzerItem> Tags,            // small + big blind tags
    IReadOnlyList<JamlyzerItem> ShopQueue,
    IReadOnlyList<JamlyzerPack> Packs,
    JamlyzerItem? SmallBlindTagGrantedJoker,     // hidden board state (Rare/Uncommon tag)
    JamlyzerItem? BigBlindTagGrantedJoker)

record JamlyzerPack(
    string Type,                          // Arcana/Spectral/Buffoon/Standard/Celestial...
    MotelyBoosterPackSize Size,
    IReadOnlyList<JamlyzerItem> Items,
    JamlyzerItem? GrantedLegendaryJoker)  // The Soul -> legendary

record JamlyzerItem(
    string Name,
    string Kind,                          // Joker/Tarot/Spectral/Planet/Voucher/Tag/Boss/Card
    bool IsHighlighted,
    string? MatchedBy)                    // clause label/Describe() that lit it; null if dark
```

`MatchedBy` is the matching clause's `Label ?? Describe()` — the human reason for the glow.

## The walk (independent, exhaustive)

One single-seed pass via `MotelySingleSearchContext` — the SAME primitive the legacy
analyzer and the filters use, but Jamlyzer drives its OWN walk and builds the DTO directly.
It does not call `MotelyLegacyTextAnalyzer` and the legacy analyzer does not call it.

Per ante it must materialize EVERY source the engine exposes — parity with the game's
"Card Sources", no holes:
- boss, voucher, small/big blind tags
- shop queue (every slot, every item type)
- every booster pack (Arcana/Spectral/Buffoon/Standard/Celestial), each pack's contents
- The Soul -> legendary joker inside arcana/spectral packs
- tag-granted jokers (Rare/Uncommon tag hidden grants)
- soul/edition rolls where relevant

A source missing from the walk = a generated item that can never glow even when a clause
matches it. Completeness is the contract.

## Highlighting (no drift, no re-implementation)

Highlighting reuses each clause's OWN match logic — the same per-clause matcher the search
path uses (`JamlScoring` / `JamlScoop` matching primitives). Jamlyzer does NOT re-derive
"does this item satisfy this clause"; if it did, glow would drift from search results and
lie to the user.

Flow: for each materialized board item, ask each `must` + `should` clause "do you match
this item, at this source/ante/slot?" via the shared matcher. First match sets
`IsHighlighted = true`, `MatchedBy = clause.Label ?? clause.Describe()`. `mustNot` is not a
highlight (negation has no positive board location).

Highlight mode is annotate, not gate: a seed always produces a full snapshot regardless of
whether `must` clauses are satisfied. Nothing highlighted = the filter found nothing in
what this seed generates; still a valid snapshot.

## WASM / Bootsharp

Re-add as a single `[Export]` in `Motely.Wasm/Program.cs`:
`JamlyzerSnapshot Jamlyzer(string seed, JamlConfig filter)`. That export pulls the snapshot
records onto the interop surface and regenerates the `motely/analysis` module jaml-ui
imports. All snapshot types are by-value records, so no ref-struct erasure dance needed.

## Build order

1. Define the snapshot records (no behavior) — compiles, marshals, jaml-ui can type against it.
2. Implement the independent single-seed walk producing a fully-dark snapshot (every source,
   no highlighting yet). Verify board completeness against a known seed.
3. Wire highlighting through the shared per-clause matcher; light up `IsHighlighted`/`MatchedBy`.
4. Re-add the WASM export.
5. (Optional) CLI `--analyze --jaml <filter>` convenience that prints the highlighted items.

## Tests

UI DTO — assert structure/highlighting on known seed+filter pairs (e.g. KHTW99TC + a Perkeo
filter lights ante-1 arcana slot 5). Do NOT pin a `.ToString()` text block here — that oracle
belongs to `MotelyLegacyTextAnalyzer`. Keep the two test surfaces separate.
