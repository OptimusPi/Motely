# Handoff — Motely item decoding (`src/decode/`)

Read this before touching anything that turns a Motely item into a name,
category, or sprite.

## The mental model (this is the whole thing)

A Motely item is **a packed integer**. The item space is **finite and
deterministic**. An int maps to exactly **one** sprite — or a *sandwich* of
sprite layers when the item is modified (base art + enhancement + edition +
seal + sticker). There is no randomness and no I/O. Decoding is a **pure
function over a small finite domain.**

Consequences:
- It does **not** need a cache. (There used to be `warmMotelyItemCache()` /
  `motelyItemCacheSize()` no-op stubs that returned void / 0. They were
  cargo-cult and have been deleted. Do not add them back. If you ever genuinely
  need memoization, memoize for real — never ship a "cache" that does nothing.)
- It does **not** need hand-rolled bitmasks. See the rule below.

## The one rule: never hand-roll the bit layout

`src/decode/motelyItemDecoder.ts` is the **single source of truth**. It reads:
- `MOTELY_ITEM_FORMATS_BY_VALUE` (the typed format table, `motelyItemFormats.ts`)
  for `enumName` / `displayName` / `category`, and
- `Motely.decodeItemEdition / decodeItemSeal / decodeItemEnhancement /
  decodeStandardcardRank / decodeStandardcardSuit / isEternal / isPerishable /
  isRental` from `motely-wasm`.

Always go through these helpers. **Do not** write things like
`(value >> 12) & 0xf` with magic category constants. A previous version of
`components/GameCard.tsx#resolvePackedAnalyzerItem` did exactly that —
`catNibble === 5 /* Joker */`, `=== 3 /* Tarot */`, etc. Those constants did
**not** match the real Motely enum, so categories decoded as bogus values. It's
now routed through `decodeMotelyItem(value)`. Keep it that way.

## Public API (exported via `jaml-ui/motely`)

- `decodeMotelyItem(input)` → full `DecodedMotelyItem` (itemType, enumKey,
  displayName, category, edition, seal, enhancement, rank, suit, sticker flags).
- `decodeMotelyItemToJamlCard(input, scale?)` → `{ type, card }` ready for
  `<JamlGameCard>`.
- `resolveMotelyItemType`, `motelyItemTypeName`, `motelyItemDisplayName`,
  `motelyItemCategory`, `motelyItemRenderCategory`, and the per-field name
  helpers (`motelyItemEditionName`, `…SealName`, `…EnhancementName`,
  `motelyStandardcardRankName`, `…SuitName`).

`input` accepts a raw `number` (packed value) or a `MotelyRuntimeItem` object —
`resolvePackedValue` normalizes both.

## Categories → render type

`CATEGORY_MAP` maps the Motely category name to a renderable category:
`Standardcard→playing`, `Joker→joker`, `TarotCard→tarot`, `PlanetCard→planet`,
`SpectralCard→spectral`, `Invalid→unknown`. For the 3-way card renderer,
tarot/planet/spectral all collapse to `consumable`. **Vouchers are not a
decoder category** — they resolve by name in
`GameCard.resolveAnalyzerShopItem`, so an unknown decoder category falls
through to name-based matching on purpose. Don't "fix" that fall-through.

## If a name/category comes out wrong

It's a **data** problem, not a decode problem: check
`MOTELY_ITEM_FORMATS_BY_VALUE` and the `motely-wasm` version (peer `>=19.0.2`).
Don't paper over it with string heuristics in a component.
