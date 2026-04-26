# motely-wasm 14.0.1

Released 2026-04-26.

## TL;DR

v14.0.0 shipped four structural defects that v14.0.1 closes. If you write JAML
filters with `standardCard: { ... }` clauses, with strict-typing matters, or
with soul-joker / legendary-joker clauses, **upgrade**. No syntax breakage —
this is purely additive correctness.

## Fixes

### standardCard parser regression — FIXED

Object-form `standardCard:` clauses (the nested form with `rank`, `suit`,
`enhancement`, `seal`, `edition` properties) crashed at parse time with
`CONVERTER EXCEPTION: ... Exception during deserialization`. Root cause:
`StandardCardValueConverter` delegated nested-DTO deserialization to
`rootDeserializer(typeof(StandardCardConfigDto))`, which under YamlDotNet's
`StaticDeserializerBuilder` did not produce reader code for nested strict-typed
enum properties (`MotelyItemSeal`, `MotelyItemEnhancement`, `MotelyItemEdition`).

The converter is rewritten to walk YAML events manually for the strict-typed
enum properties — same pattern `EnumOrAnyConverter<T>` already uses. Strict
types stay strict; the converter just stops asking source-gen to do the thing
source-gen can't reliably do behind a discriminated-union wrapper.

Pinned by 5 new tests in `Motely.Tests/StandardcardMatchingTests.cs` covering
rank-only, suit-only, rank+suit, rank+enhancement+seal, and shorthand forms.

### standardCard matched zero seeds — FIXED

Even after the parser fix, a bare clause like `standardCard: { rank: K }`
returned zero matches at any seed count. Root cause:
`JamlConfig.NormalizeDefaultSources` had no case for `MotelyFilterItemType.Standardcard`,
so the source defaults stayed empty arrays and the matcher had nothing to
scan.

Added a `Standardcard` case that mirrors the joker-types default:
`boosterPacks = [0..5]`. A bare clause now finds matches in roughly 80%+ of
seeds (Kings appear in ~7.7% of standard cards × multiple packs per ante × 8
antes — easy to hit at least once).

### Strict YAML key validation — FIXED

The static-deserializer fragment parser had `.IgnoreUnmatchedProperties()`
silently dropping any unknown YAML key. Typos like `boses:` (instead of
`boss:`), `boosterPakcz:` (instead of `boosterPacks:`), or `mint:` on event
clauses (instead of `min:`) would silently parse as no-ops, producing
filters with missing constraints. The SIMD prefilter would then accept
seeds it shouldn't have — a silent false-positive source.

Strict mode is now ON: every typo gets rejected at parse time with
`Unknown property '<key>' in <context>` plus line+col coordinates. The
3 `Unknown*Key_IsRejected` regression tests in `JamlConfigTests` are
green for the first time since Phase 2.

**Migration note:** if your existing JAML had typos that were silently
accepted, you'll now get a parse error pointing at the line. Fix the
typo and you're done. If a key was deprecated and removed (e.g.
`type: StandardCard` flat syntax), migrate to the modern equivalent
(`standardCard: { ... }` nested object).

### Soul-joker structural validator — DEMOTED FROM THROW

`JamlSoulJokerStructuralValidation.ValidateLegendaryJokerClauseOrThrow`
used to `throw new InvalidOperationException` at plan-creation time when
a legendary-joker clause's only target was shop booster slot 0 (which is
forced Buffoon at ante 1, so soul/legendary cannot spawn). This blocked
users — including users who had ante 2+ where slot 0 IS a weighted pack
and CAN spawn soul cards.

Demoted to a no-op. Rule: never block users on inferred mistakes. If a
clause has a dead match path, the search returns zero matches at runtime
and the user adjusts. Future work: surface as structured warnings via
`TryLoad` return shape so consumers (CLI, WASM bridge, mobile) can render
them however they want without library code touching `Console`.

### Soul-joker default carve-out — DELETED

Removed the inconsistent `if SoulJoker && (arcanaBoosterPacks ||
spectralBoosterPacks)` carve-out that injected `boosterPacks = []`
specifically for that combination. Defaults are now consistent across all
clause types: if the user wants no default packs, they write
`boosterPacks: []` explicitly. No more "default sometimes, except when X
is set" magic.

## Migration

### `type: StandardCard` flat syntax — REMOVED

If your JAML uses the deprecated flat syntax:

```yaml
must:
  - type: StandardCard
    rank: K
    seal: Red
    edition: Polychrome
```

Migrate to the modern nested object syntax:

```yaml
must:
  - standardCard:
      rank: K
      seal: Red
      edition: Polychrome
```

Strict mode now rejects `type:` as an unknown property at parse time.

### Typo'd source / clause keys — NOW REJECTED

Filters that had silent typo'd keys will now fail to load. Examples:

- `boosterPakcz:` → `boosterPacks:`
- `boses:` → `boss:`
- `voucer:` → `voucher:`
- `mint:` (on event clauses) → `min:`
- `totallyFakeKey:` → remove

The error message includes line + column to make this fast to fix.

## Internal changes

- Build artifact: motely-wasm 14.0.1 npm tarball (17 files, 12.2MB AOT-LLVM
  bundle).
- `Motely.Tests` corpus regression test added (`JamlCorpusRegressionTests.cs`)
  — every committed `JamlFilters/*.jaml` must parse clean before any release
  ships. This is the missing guardrail that would have caught v13's
  standardCard regression at build time.
- `JamlFilters/loki.jaml` migrated from deprecated `type: StandardCard` flat
  syntax to modern nested object syntax (was the only corpus file still on
  the old form).
- `Motely/Filters/JamlYamlContext.cs` registrations unchanged — strict-typed
  enum properties (Seal, Enhancement, Edition) work correctly under
  source-gen now that the converter doesn't do the broken recursive-dispatch
  pattern.

## Test coverage

- 274/274 unit + integration tests passing (was 270 in v14.0.0 with 3 strict-key
  tests red and 1 soul-joker test that expected the old throw behavior).
- 13/13 release-smoke assertions passing against the live npm artifact.
- Corpus regression: 44/44 `JamlFilters/*.jaml` parse clean.

## Consumer cascade

| Consumer | motely-wasm | Status |
|---|---|---|
| seedfinder.app (mmm) | ^14.0.0 → ^14.0.1 | committed `d4ca999`, pushed, Vercel deploying |
| ErraticDeck.app | ^14.0.0 → ^14.0.1 | committed `b42bc25`, pushed |
| thelongblind6 | ^14.0.0 → ^14.0.1 | committed `27cabc7`, pushed |
| weejoker.app | ^14.0.0 → ^14.0.1 | bump in working tree, awaiting PR (build still has pre-existing `./Standardcard` import path issue, separate fix) |

## CDN

`https://cdn.seedfinder.app/motely-wasm/14.0.1/index.mjs` — 200, populated.
