# pifreak-forgot.md

The "I'll get to it" pile. Things pifreak meant to do, half-did, or discovered
mid-flight. If you're an agent landing here: this is the backlog, not a spec —
confirm with pifreak before building anything big.

## Deferred features

### Per-round starting draw (the deck's opening hand) as a JAML source
pifreak promised JAML support for the **per-round starting draw** — what cards
you actually draw at the start of each round/hand, not just what's in the deck.
- This needs the per-round shuffle PRNG (`MotelySingleSearchContext.Shuffle()`),
  simulated round-by-round. The analyzer already has a NOTE where `DrawOrder`
  used to be (it was removed because the old impl showed pack cards, not the
  real draw). See `JamlyzerFilterDesc.CheckSeed`.
- This is a **bigger feature** (round simulation, hand size, draw count). Scope
  it properly before starting.

### Jamlyzer glow — phase 2 sources
The analyzer glow now comes from the real scorer (`JamlScoop`), but only
**shop + booster-pack** matches are overlaid onto the board today. Still to
instrument + overlay (the scorer covers them; the analyzer just doesn't paint
them yet):
- Boss, Voucher, small/big Tag (one cell per ante)
- The Soul -> legendary joker (pack `GrantedLegendaryJoker` cell)
- Tag-granted jokers (Rare/Uncommon tag -> `*TagGrantedJoker` cells)
- Consumable-triggered sources: Emperor, Purple Seal / 8-Ball, Sixth Sense,
  Seance, Judgement, Wraith, Riff-Raff (these have no board cell — render as a
  separate "consumable would give…" list).
- Gameplay rolls (misprint/lucky/space/business/glass/…): bool/int outcomes,
  no item. Could wrap as pseudo-items via new `MotelyItemType` entries (pifreak
  okayed extending the enum at the end: NotAvailInStream/Invalid live there).

## Known warts / tuning decisions

### Shop default depth
Bare clauses (no source) default to shop slots `[0,1,2,3]` + packs `[0..5]`
(`JamlConfigLoader.NormalizeDefaultSources`). pifreak wondered about searching
"a lil bit deeper" in the shop. NOT done — deepening changes scoring for every
bare-clause filter, so it's a deliberate decision, not a freebie. Pick a depth
and change the two `[0,1,2,3]` defaults together if you want it.

### Enum.Parse in the joker scorer (AOT smell)
`JamlScoring.CountJokerOccurrencesGeneric` builds target types via
`Enum.Parse<MotelyItemType>(jokers[i].ToString(), true)` — string parsing in the
AOT/WASM path. The packed-int idiom (`(MotelyItemType)((int)Category.Joker |
(int)joker)`, used elsewhere in the engine) is the typo-proof, allocation-free
replacement. Left alone for now because the generic `TJoker` needs a no-box
conversion (Unsafe.As or per-type handling) — tangential to the scoop work.

### Charm/Ethereal slot alignment (analyzer glow)
The analyzer joins two independent single-seed walks (board layout + scoop) by
`(ante, source, slot, cardIndex)`. The tarot/spectral scorers consume an EXTRA
arcana/spectral pack for the Charm/Ethereal-tag closure
(`weightedShopDrawNumber < 2`) that the layout walk does not. On a Charm/Ethereal
seed that can advance the pack content stream further in the scorer than in the
layout walk -> a pack `cardIndex` glow could land on the wrong card. Plain
clauses (no CharmTag/EtherealTag) are fine; verified shop+pack alignment on
UNITTEST. Fix when phase-2 hits pack sources.
