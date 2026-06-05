# Balatro Mechanics & Synergies — Reference for the Seed/MCP Layer

Purpose: give an MCP server (or any tool translating natural language → JAML) enough
*real* game knowledge to build correct filters instead of keyword-mapping. Every
mechanic below is tagged with a confidence/source marker:

- **[SRC]** verified directly in the Balatro LUA source (`external/Balatro/`) — ground truth.
- **[WIKI]** from balatrowiki.org / community guides — high confidence, version-sensitive.
- **[?]** inferred or uncertain — do not assert to users without checking.

> Source paths are relative to the vendored game at `d:\BalatroSeedOracle\external\Balatro`.

---

## 1. The probability system (this is the whole "luck" model)

**[SRC]** All random procs read a single global table `G.GAME.probabilities`, whose key
field is `.normal` (default `1`). A roll succeeds when
`pseudorandom(<key>) < G.GAME.probabilities.normal / odds`.

`Oops! All 6s` (`card.lua:609`) does exactly this on acquire:

```lua
for k, v in pairs(G.GAME.probabilities) do G.GAME.probabilities[k] = v*2 end
```

…and the inverse (`/2`) when removed (`card.lua:666`). So:

> **`G.GAME.probabilities.normal` = 2 ^ (number of Oops! All 6s in play).**

This is the JAML `sources.luck` value. Mapping:

| `luck:` | # Oops! All 6s | How to reach it |
|--------:|---------------:|-----------------|
| 1 | 0 | base game |
| 2 | 1 | one Oops! |
| **4** | **2** | **Oops! + Ankh copy** (the canonical combo) |
| 8 | 3 | Oops! + Ankh + Showman/Invisible Joker dupe |
| 16 | 4 | the `Fragile` challenge starts you with 2 negative Oops! |

**[SRC]** Oops! is NOT copyable by Blueprint/Brainstorm (see §3). Extra copies come only
from **Ankh**, **Invisible Joker**, **Showman** (re-buy), or the Fragile challenge
(`card.lua` confirms the `*2` stack; multiple copies each apply independently).

### JAML event selectors ↔ source rolls  **[SRC]**

The engine's numeric event selectors each correspond to a specific `pseudorandom` key.
Every one of these scales with `luck` (= `probabilities.normal`):

| JAML selector | Source call | Base odds | At `luck:4` |
|---|---|---|---|
| `luckyMoney` | `pseudorandom('lucky_money') < normal/15` (`card.lua:1076`) | 1 in 15 | 4/15 ≈ 27% |
| `luckyMult` | `pseudorandom('lucky_mult') < normal/5` (`card.lua:988`) | 1 in 5 | 4/5 = 80% |
| `glassDestroy` | `pseudorandom('glass') < normal/odds` (`state_events.lua:961`, odds 4) | 1 in 4 | always breaks |
| `wheelOfFortune` | `pseudorandom('wheel_of_fortune') < normal/odds` (`card.lua:1470`) | 1 in 4 | always |
| `businessPayout` | Business Card face-scored roll (`card.lua:794` loc) | 1 in 2 | guaranteed |
| `parkingPayout` | Reserved Parking held-face roll (`card.lua:879`) | 1 in 2 | guaranteed |
| `bloodstoneTrigger` | Bloodstone flush xMult roll (`card.lua:832`) | 1 in 2 | guaranteed |
| `grosMichelExtinct` | `pseudorandom('gros_michel') < normal/odds` (`card.lua:3020`) | 1 in 6 | 4/6 (faster!) |
| `cavendishExtinct` | `pseudorandom('cavendish')` (`card.lua:3020`) | 1 in 1000 | 4/1000 |

> **Footgun the MCP must respect:** raising `luck` speeds up *negative* rolls too —
> Glass cards shatter, Gros Michel/Cavendish go extinct faster, The Wheel boss flips
> more cards. "More luck" is not strictly good. **[SRC]**

### PRNG internals  **[SRC]**

`pseudohash` (`misc_functions.lua:279`), `pseudoseed` (`:298`), `pseudorandom`
(`:315`). This is the math the Motely engine re-implements; the FMA/`2^52` magic-number
rounding in `SeedMath.cs` exists to match LuaJIT's float behavior here. Read these before
touching seed math.

---

## 2. Scoring: `Score = Chips × Mult`  **[SRC/WIKI]**

Order of operations (Activation Sequence, `state_events.lua` + wiki):

1. **Hand base** chips/mult from hand type & level (planet cards level these). The Arm
   lowers level (min 1); The Flint halves base chips & mult.
2. **Scored cards** left→right: base chips, then enhancements, then seals, then retriggers.
   Only *scored* cards count (Splash makes all *played* cards score).
3. **Jokers** left→right: each reads/modifies current Chips/Mult. Editions apply after the
   joker's own effect (Foil +50 chips, Holo +10 mult, Poly ×1.5 mult).
4. Final `Chips × Mult`.

**The one ordering rule that matters: `+Mult` before `×Mult`.** `(10+20)×2 = 60` beats
`(10×2)+20 = 40`. So additive effects go left, multiplicative go right. Otherwise joker
order is commutative and irrelevant. **[SRC/WIKI]**

Scaling tiers **[WIKI]**: additive (linear, good early) → multiplicative xMult (quadratic)
→ retrigger / stacked xMult (exponential — Baseball Card `^uncommons`, retriggered Mime+Baron).

---

## 3. Copy jokers: Blueprint & Brainstorm  **[SRC/WIKI]**

- **Blueprint** copies the joker to its *immediate right*.
- **Brainstorm** copies the *leftmost* joker (position-independent on itself).
- Stacking adds one more copy each. Brainstorm copying Blueprint copies *Blueprint's target*.

**They cannot copy "passive modifier" effects.** The 29 fully-incompatible jokers **[WIKI]**:

> Astronomer · Chaos the Clown · Chicot · Cloud 9 · Credit Card · Delayed Gratification ·
> Drunkard · Egg · Four Fingers · Gift Card · Golden Joker · Invisible Joker · Juggler ·
> Merry Andy · Midas Mask · Mr. Bones · **Oops! All 6s** · Pareidolia · Rocket · Satellite ·
> Shortcut · Showman · Sixth Sense · Smeared Joker · Splash · To the Moon · Trading Card ·
> Troubadour · Turtle Bean

> **MCP rule:** never reward/suggest Blueprint or Brainstorm as a way to multiply
> Oops! All 6s, Pareidolia, Splash, or any economy/passive joker on this list. The game
> flags them incompatible in-tooltip. (This is the exact bug in the original
> `OopsAnkh_4xLuck` filter — Blueprint/Brainstorm `should` clauses claimed "8× luck"; they
> can't copy Oops!, so 4× via Ankh is the real ceiling.)

---

## 4. Synergy archetypes (for intent → JAML)

- **Lucky build** — Lucky-enhanced cards + `Oops! All 6s` (doubles trigger odds) +
  `Lucky Cat` (x0.25 mult per successful Lucky trigger, scales) + `Hanging Chad`
  (retriggers first scored card → put a Lucky card first for extra rolls). **[WIKI]**
- **Glass build** — Glass cards (×2 mult, 1-in-4 break) + `Glass Joker` (gains x0.75 per
  glass destroyed). Oops! makes them *always* break — accelerates Glass Joker but eats
  your deck. **[WIKI]**
- **Steel build** — Steel cards (×1.5 while *held*, not played) + `Mime`/`Baron` retrigger
  held effects. **[WIKI]**
- **Economy/interest** — interest is `$1 per $5 held`, cap `$25→$5` (`state_events.lua:1191`).
  `To the Moon` raises the rate, `Money Tree` voucher raises the cap. Egg/Bull/Gift Card/
  Golden Joker fund mid-game. **[SRC]**
- **xMult scaling** — Cavendish/Card Sharp/Glass Joker/The Trio etc.; retrigger via
  Blueprint+Brainstorm on the *best* target (not the rarest). **[WIKI]**

---

## 5. Verified cheese for **seeded casual** (no leaderboard, just fun)  **[SRC]**

These are mechanically sound per source — pure single-player fun:

1. **Guaranteed money printer: Pareidolia + Business Card + one Oops!**
   - Pareidolia: *all* cards are face cards (passive). **[WIKI]**
   - Business Card: scored face card has 1-in-2 chance of `$2`. **[SRC]** roll uses `normal/odds`.
   - One Oops! → `normal=2` → `2/2` = **guaranteed $2 per scored card**.
   - Add **Splash** (every played card scores) → all 5 played cards print `$2` each, every hand.
   - Note: Pareidolia, Splash, and Oops! are all Blueprint-incompatible — but you don't
     need copies; the engine runs raw.
2. **Quad-roll Lucky money: Lucky card (first) + Hanging Chad + Oops!+Ankh.**
   Hanging Chad retriggers the first scored card; at `luck:4` each `lucky_money` roll is
   4/15, and you get multiple rolls per hand off the one card. **[WIKI + SRC odds]**
3. **Fast x3: Gros Michel + Oops!** — extinction goes 1-in-6 → faster, so Cavendish (×3)
   shows up sooner. (Downside is the point.) **[SRC]**

---

## 6. Open items / to verify against source later

- **[?]** Exact Business Card / Reserved Parking base odds field (`ability.extra` numeric) —
  confirm the `odds` value in `card.lua` rather than trusting the 1-in-2 wiki figure.
- **[?]** Whether `forced_all_6s` / challenge modifiers alter `probabilities` beyond the
  `*2` stack.
- **[?]** Retrigger interaction count for Hanging Chad + Seals + Oops! (rolls per card).

_Last grounded against source on 2026-06-05. When the game patches, re-verify §1 odds and
§3 incompatible list — both are version-sensitive._
