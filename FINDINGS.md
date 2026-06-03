# FINDINGS — the cringe, and what it actually was

One sin wearing three hats: **schema drift** — the UI re-deriving, re-typing, or
renaming data that `motely-wasm` already owns, in a library whose `CLAUDE.md`
explicitly says *don't hand-roll Balatro item names / pull enums from the engine*.
This isn't a new diagnosis either — the term is already in this repo's own commit
log (`ff64157 "Audit pass: … partial schema drift fix"`). It was partial. Here's
the rest.

All three are **fixed** on `claude/blissful-mendel-Nzt84` — see `HANDOFF.md`.

---

## 1. The phantom `Echo` enum member  *(was breaking the build)*

`src/components/JamlAestheticSelector.tsx` referenced `JamlAesthetic.Echo`.
The installed engine (`motely-wasm@19.1.1`) has no such member — its
`JamlAesthetic` is `Palindrome(0), Psychosis(1), Gross(2), Funny(3), Balatro(4)`.
Commit `3472e5b "Rename Psychosis aesthetic option to Echo"` renamed the member
to mirror an engine rename **that never shipped**, so `pnpm typecheck` failed on
a property that doesn't exist. Drift made literal — and authored by `Claude`, not
a foreign bot, which is the honest version of the story.

The label "Echo" was never the problem (a display string is yours to choose). The
bug was binding the *id* to a non-existent engine member. **Fixed:** the id now
resolves to whatever aesthetic the engine actually exposes and adopts a native
`Echo` automatically once it ships upstream (Track A in `HANDOFF.md`).

## 2. Hand-typed joker rarities — *and they were already wrong*

`src/components/jamlMap/JokerPicker.tsx` hard-coded ~90 joker names into
`LEGENDARY_JOKERS / RARE_JOKERS / UNCOMMON_JOKERS` Sets, while the peer dep ships
the authoritative lists right there: `MotelyJokerCommon (61) / MotelyJokerUncommon
(64) / MotelyJokerRare (20)`, with Legendary being the 5 left over
(`Canio, Triboulet, Yorick, Chicot, Perkeo`). 61+64+20+5 = 150 = `MotelyJoker`, a
clean partition.

This is the punchline: **the hand-typed list had already rotted.** Seven jokers
the engine ships as **Rare** were filed under **Uncommon** in the UI:

> `DNA`, `Vagabond`, `Baron`, `Obelisk`, `Baseball Card`, `Ancient Joker`, `Campfire`

So the picker was handing the search engine the wrong rarity clause for these. Not
a hypothetical maintenance risk — a live mislabel. **Fixed:** rarity is now read
directly from the engine enums (name-normalized, with one explicit alias because
the engine spells "8 Ball" as `EightBall`). Verified: all seven now read Rare;
legendaries, commons, and anchors (Blueprint→Rare, Perkeo→Legendary, Greedy
Joker→Common, 8 Ball→Common) all match the engine.

## 3. The ghost `jaml.schema.json`

`package.json` `files` promised to publish `jaml.schema.json` — a file that isn't
in the repo, in a library whose stated philosophy is *delegate validation to the
engine, ship no schema of our own* (`CLAUDE.md`). The manifest advertised a thing
the project deliberately doesn't do; every `npm pack` would warn or silently omit
it. **Fixed:** dropped from `files`.

---

## Meta-beat: even the audit drifted

The first review pass indicted `jokerRarity.ts`'s local `MotelyJokerRarity` enum
as "invented drift." Reading the file showed it's a *defensible* opaque UI tier
tag — the engine has no rarity-tier enum to mirror. The real drift was next door
in the hand-typed Sets. The lesson is the same one the fixes encode: **bind to
the source before you trust the claim.** (The tier enum was kept, but renamed
`JokerRarityTier` so it stops masquerading as a `Motely*` engine type.)
