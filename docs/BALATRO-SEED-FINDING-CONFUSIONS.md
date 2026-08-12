# Balatro seed finding — CONFUSIONS

**Curse of knowledge edition.** Things Nat knows in his bones that bots (and tired humans) fuck up for 18 months.

If you are a bot: **read this before writing a filter.**  
If you are Nat: use this when Claude “found nothing” and the package was never written down.

---

## 1. Showman — game law vs Motely raw search

| Confusion | Reality |
|-----------|---------|
| Showman only matters for Oops | Showman allows **dupes of any card** |
| Without Showman | A dupe is **force re-rolled** to **same type + same rarity pool** (not the same named card again) |
| Motely multi-Oops hit | Search is **raw / stateless** — stream is scored **as if Showman is on**. Engine does **not** model the force re-roll path |
| “I filtered four Oops, I’m done” | **Paper stack** unless the seed also has **Showman** (or you accept in-game re-rolls will eat the dupes) |
| “Search no-Showman universe” | **Not available** today — you search raw, then require Showman for playable dupe packages |

**Rule:** Motely shows **raw** multi-dupes. **Showman on the seed** is how you make that stack **real**. Bots that skip Showman hand you fantasy runs.

---

## 2. Negative Tag is not ante 1

| Confusion | Reality |
|-----------|---------|
| Default antes 1–8 on Neg | Wastes ante 1; Neg **does not roll ante 1** |
| `tag: NegativeTag` anywhere | **smallBlind** vs **bigBlind** are different clauses |
| “Tags: X, Y” in analyze | First is usually **small**, second **big** |
| Free Neg joker package | Need **NegativeTag** in the right blind + ante band **2–8** |

**Rule:** always `antes: [2, 3, 4, 5, 6, 7, 8]` for Neg unless you know a weird exception.

---

## 3. `and:` is not nesting cosplay

| Confusion | Reality |
|-----------|---------|
| Two must rows “Neg” and “Oops” | Same as and for must-all, but **intent** is “package” |
| Nested and “for structure” | Only if you mean **joint product**: *only* Neg-free Oops, not either alone |
| Bot essay about “product law” | Still forgets Showman |

**Rule:** if you would delete the seed when one half is missing, it is an **and package**. Write that in `description:`.

---

## 4. Empty antes ≠ “I thought about antes”

| Confusion | Reality |
|-----------|---------|
| Omit `antes` | Engine fills **1–8** after hoist |
| Parent `and`/`or` with `antes` | Passes into **bare** children; child override wins |
| Nested arms inside `and`/`or` | Must hoist + fill recursively or nested Oops score **zero** (fixed once; don’t rebreak) |

**Rule:** Neg and other “not ante 1” things must be **explicit**. Oops/Showman often want full 1–8 or a chosen band.

---

## 5. Shop slots 0–7 are not “the shop”

| Confusion | Reality |
|-----------|---------|
| Default sources shop 0–7 | Fine for early slots; **misses deep rerolls** |
| Analyzer `33) Oops` | Deep; filter must include that index band |
| “Hundreds of rerolls ante 8” | Model with **chunked** `shopItems` windows + `or mode max/sum` |
| One-slot probes | **Bad tests** — cutoff 0 + over-permissive paths lie |

**Rule:** if the player rerolls, the filter must see **deep** `shopItems` (or you will never find the god seed).

---

## 6. `mode: max` vs `mode: sum` (or arms)

| Confusion | Reality |
|-----------|---------|
| max | **Best arm only** (best chunk / best ante band) |
| sum | **Total** all arms that hit |
| Using max for “four Oops” | Wrong tool — use **`min: 4`** on a counting clause over the union of slots |

**Rule:** mode is **aggregate policy for or/and scoring**, not a free min-count.

---

## 7. Deck and stake are part of the seed identity

| Confusion | Reality |
|-----------|---------|
| Same 8 chars, Red vs Anaglyph | **Different** rolls |
| Filter Anaglyph, play Red | “Seed is broken” |
| Bot omits deck in writeup | Operator plays wrong universe |

**Rule:** every pinned seed line needs **deck + stake** in description or seeds notes.

---

## 8. NL → JAML is NOT surface-word matching (bot training law)

| Player says | Bot pattern-match crime | Reality |
|-------------|-------------------------|---------|
| “I want **60 negative jokers** in ante 8” | `joker: []` × 60, edition Negative, ante 8 | **Impossible** as a direct shop/pack package. Engine cannot “find 60 neg jokers.” |
| “60 Ankhs” | 60× `spectralCard: Ankh` | Not how Ankh economy works |
| “Negative jokers in buffoon pack” | Search Neg edition in packs | Neg edition from **tags / economy**, not “pack prints 60 negs” |

**Rule for bots:** translate NL into a **playable package of enablers**, then search **those**. Motely finds seeds that **contain the pieces**. Motely does **not** simulate full mid-run inventory chess. The human (or a separate planner) owns the **order of operations**.

### The “60 neg jokers” package (Diet Cola + Ankh + Perkeo + Ghost)

This is **confab + filter**, not a solver:

| Piece | Why it is in the filter |
|-------|-------------------------|
| **`deck: Ghost`** | **Only** deck that puts **Ankh in `shopItems`** so you can **hold** it. Buffoon-pack Ankh is **consume-on-open** — you eat it when the pack opens; that is a different, worse tool for the loop. |
| **Ankh** early, **`sources: shopItems`** | Pocket Ankh. Not “Ankh anywhere.” |
| **Perkeo** early (often ante **1–2** Soul/pack) | Copies **held consumables** → Ankh factory **before** the burn. |
| **Second Perkeo after the sacrifice** | **Ultimate wish.** Ankh loop often **kills** the first Perkeo (keep cola, eat the rest). You still want a **later** Perkeo so the press / economy doesn’t end as a one-shot. |
| **Diet Cola** banked | Sell → **Double Tag**. Stack of colas = stack of double tags. |
| **NegativeTag** | **Never ante 1.** Valid band for the skip-tag roll is **`antes: [2, 3, 4, 5, 6, 7, 8]`** — that is the **whole legal window**, not “late.” **Late cashout** = you *choose* e.g. ante **8** (or A3 mid-press) inside that band. |
| Doubles × Neg | Cashout: double tags × Neg → pile of **free negative jokers**. |

### Antes: 0 / 1–8 / deep (don’t mush these)

| Ante | Meaning |
|------|---------|
| **0** | Pre-run / special slot — **Hieroglyph / Petroglyph** universe (and jamlyzer pre-run shop). **Not** “early ante 1.” Not where Neg tags live. |
| **1** | First ante of the run. **No NegativeTag.** Soul/Perkeo A1 dream lives here. |
| **2–8** | Normal ante window for **Neg tags** and most “full run” filters. Engine default empty-antes fill is **1–8** (so **you** must strip 1 for Neg). |
| **>8 … ~39** | Deep / extended run space (voucher/hiero timelines, analyzer). **E22** = ante-39 hang note. Don’t put Neg on “1–39” by default. |

**Order of operations (think — do not invent a different loop):**

1. **Perkeo #1 early** (A1 dream). Hold **Ankh**(s) from shop (Ghost). Perkeo copies Ankhs while you bank them.  
2. Have **Diet Cola**(s). Ankh **duplicates one random joker** and **destroys all others**.  
3. Protect the loop: often **sacrifice Perkeo #1** (and junk) so the survivor is **Diet Cola**.  
4. **Sell Diet Cola** → **+Double Tag**.  
5. **Ankh → cola → sell → Ankh → cola…** boom double-tag stack.  
6. Hit **NegativeTag** on a chosen ante in **2–8** (with doubles) — **late** means **high ante in that band** (e.g. 8), not “write 2–8 and call it late.”  
7. **Ultimate:** land **Perkeo #2 after the sacrifice window** so you’re not permanently grand-master-less. Filter shape: early Perkeo + **second** Perkeo on a **later** band — see `PerkeoColaMidTrigger.jaml`.

**JAML therefore must/shoulds the enablers**, e.g. Ghost + shop Ankh + DietCola + Perkeo + NegTag — see:

- `JamlFilters/PerkeoColaEarly.jaml` / `PerkeoColaMidTrigger.jaml` / `PerkeoCola.jaml`
- `JamlFilters/DietCola_Ghost_Ankh.jaml` (pinned seeds include OPUS-ish hits)
- `JamlFilters/GhostColaDicetrick.jaml`

**Do not** author a filter whose must is “60 negative edition jokers.” That is NL surface matching. That is how bots waste months.

### Ankh source law (short)

| Source | Behavior for this package |
|--------|---------------------------|
| **Shop (Ghost)** | Can **pocket** Ankh → Perkeo can copy → loop fuel |
| **Buffoon / spectral pack open** | Often **consumed on open** — not your Ankh factory |

Bots that put Ankh only in packs for a Ghost cola press are wrong.

---

## 9. Package vs wish (one sticky)

| Wish language | Package language (searchable) |
|---------------|-------------------------------|
| “60 neg jokers A8” | Ghost + shop Ankh + DietCola + Perkeo + NegTag late |
| “lots of Oops” | Oops min N + **Showman** (raw vs real) |
| “perfect economy” | Not a clause — stop |

**Rule:** if the wish is **impossible as a shop stream**, ask “what **seeded pieces** make the **play line** work?” Filter those.

---

## 8. Analyze vs filter match

| Confusion | Reality |
|-----------|---------|
| Analyzer shows a card | Filter sources/antes/edition might still miss it |
| Filter matches | Always **re-analyze** before calling it a god seed |
| “Soul in pack” | Legendary identity still needs engine confirm (`legendaryJoker: Perkeo` etc.) |

**Rule:** brag only after **analyze + `--seeds` R1** on the same JAML.

---

## 9. Edition and stickers

| Confusion | Reality |
|-----------|---------|
| Foil Oops in analyze | May or may not match bare `OopsAll6s` depending on edition filters |
| “Negative free” | Free joker from **Negative Tag** (often Negative edition) ≠ filter `edition: Negative` unless you set it |

**Rule:** say which you mean: **tag path** vs **edition disc**.

---

## 9b. Black stake stickers (Eternal etc.)

**Black stake** introduces **stickers** on jokers (engine: `MotelyJokerSticker`, JAML `stickers: [Eternal, …]`).

| Sticker vibe | Reality |
|--------------|---------|
| **Eternal** | Nothing can **sell / destroy / harm** that joker. Usually **feels bad** — you get sick of the thing and cannot delete it. |
| **Eternal OP exception** | When the joker **wants** to stay forever and scale. Classic: **Eternal Madness** — eats other jokers, grows; you *want* it stuck. Rhymes with **Joker Stencil** “all stickers / locked identity” brain (different card, same “I am the build” energy). |
| White/Red stake hunt | No sticker stream — Eternal clauses won’t mean Black-stake reality |
| Bare `joker: Madness` | Matches Madness **with or without** sticker unless you set `stickers:` |

```yaml
stake: Black
must:
  - joker: Madness
    stickers: [Eternal]
```

Existing package shape in tree: `JamlFilters/01_MadnessMonday.jaml` (Eternal Madness + friends).  
**LSP:** use **vscode-jaml / Motely.Lsp** for disc keys (`stickers`, stake enum) — don’t freestyle sticker names from vibes.

**Rule:** Eternal is a **curse or a crown**. Default = curse. Madness (and similar “never sell me”) = crown. Pin **stake: Black** or the sticker is fiction.

---

## 10. Bot failure modes (expensive)

| Failure | Cost pattern |
|---------|----------------|
| Multi-Oops must, no Showman pin | Raw hits that **die in-game** without Showman (force re-roll) |
| Neg on default 1–8 or ante 1 only | False empties / wrong hits |
| Debug builds / wrong project | Slow, “engine is broken” |
| Agent API loop without Motely CLI | **$20 and nothing** — open claw style thrash |
| Poetry `description:` | RAG learns lies |
| Parallel matrices / cage hooks | Sessions brick; seeds still unfound |
| Kid-glove prose instead of UI priming law | Soft bots, hard zero seeds |

**Rule:** Motely **Release CLI** + one filter + proof. Agents mule tickets; they do not replace the door.

---

## 11. Curse of knowledge checklist (paste into tickets)

Before you say “find me a seed”:

- [ ] Package in one sentence  
- [ ] Deck + stake  
- [ ] Showman if multi-dupes must be **playable** (engine search is already raw/Showman-shaped)  
- [ ] Neg antes **2–8** if Neg matters  
- [ ] small vs big blind tags  
- [ ] Deep shop if rerolls matter  
- [ ] `min` for counts; `mode` for or aggregate  
- [ ] `description:` with INTENT / MUST / NOT / SEEDS  
- [ ] Prove with analyze + `--seeds`

---

## 12. Things that *look* smart and aren’t

- Single `shopItems: [n]` sweeps declaring “index mapping proven”  
- Matching at **score cutoff 0** as success  
- “Any joker” with no package  
- Searching Whimsy seeds on the wrong deck  
- Expecting Claude to remember: multi-dupe filter hits ≠ in-game without Showman (raw search assumes Showman-shaped stream)
- Treating Motely as if it simulates force re-roll when Showman is absent (it does not)

---

## 13. Related

- Guide: `docs/BALATRO-SEED-FINDING-GUIDE.md`  
- Filter grammar: `JAML.md`  
- Agent board: `HANDOFF-MATRIX.md`
- Example package filter: `JamlFilters/NegTag_FourOops.jaml`  
- Example god-shaped seed: **`1F5WEAYR`** Anaglyph/White (Perkeo, Showman, Neg, multi Oops) — re-prove after edits  

---

*Confusions doc. Add a row when the same footgun bites twice. Do not soft-wash. Do not kid-glove the operator.*
