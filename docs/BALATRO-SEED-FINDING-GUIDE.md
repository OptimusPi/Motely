# Balatro seed finding — GUIDE

**Who this is for:** humans + bots hunting seeds with Motely / JAML / Search Party.  
**Not:** lore essays, “have fun exploring,” or API money pits.  
**Engine home:** `MotelyJAML` (this repo). **CLI is the daily door.**

---

## 1. What “finding a seed” actually is

You are not wishing at the game. You are:

1. **Naming a package** (what must be true on a seed).
2. **Writing a filter** (JAML) that the engine can score.
3. **Running search** (Release CLI, sequential when possible).
4. **Proving hits** (`--analyze`, or re-score the seed list).
5. **Playing** the seed in-game with the **same deck/stake** the filter used.

If deck/stake drift, the seed is a different universe. **Always pin deck + stake.**

---

## 2. The three lists (JAML)

| List | Job |
|------|-----|
| **must** | All true or seed dies. Use for the package you actually want. |
| **should** | Score / rank. Tallies, cutoffs, “how juicy.” |
| **mustNot** | Any true → reject. |

**Category any** = empty disc list, e.g. `joker: []`. Token `Any` is rejected.  
**Empty antes** on a bare arm → engine fills **1–8** (default). That is *not* always what you want (see Confusions).

Clause shape: **what** + **where** (antes, sources) + optional **how** (`with`).  
Grammar lives on **FilterDescs**, not YAML cosplay.

---

## 3. Packages beat shopping lists

Bad filter: “any cool joker somewhere.”  
Good filter: a **named package** you would actually open a run for.

Examples of real packages:

| Package | Core and: |
|---------|-----------|
| **Neg free Oops** | smallBlind **NegativeTag** (antes **2–8**) + **OopsAll6s** (min ≥ 2–4 raw) + **Showman** if those dupes must be real in-game |
| **Whimsy / dicetrick** | Perkeo + Showman + Neg skip tags + Oops stack (see `JamlFilters/Whimsy_Dicetricks.jaml`) |
| **Soul pipeline** | Sixth Sense / Soul pack path → legendary (Perkeo etc.) |
| **“60 neg jokers” (NOT 60 shop negs)** | **Ghost** + **Ankh in shopItems** + **Diet Cola** + **Perkeo early** + **NegativeTag** in **2–8** (pick cashout ante; 2–8 = legal band, not “late”) — see CONFUSIONS §8. Filters: `PerkeoCola*`, `DietCola_Ghost_Ankh`, `GhostColaDicetrick` |
| **Ultimate cola press** | Same as above **plus second Perkeo after you sacrifice the first** (Ankh keeps cola, eats Perkeo #1). Search **two Perkeo windows**, not one pet forever. |

**Bot law:** NL “I want 60 negative jokers” → search the **enabler package**, never a 60× negative-joker clause (impossible as a direct find).

**`and:` is product law**, not nesting sugar:  
“I only want **Negative-tag free Oops**” = Neg **and** Oops, not either alone.

---

## 4. Showman (write this on a sticky)

**In-game law:** Showman allows **duplicates of any card**.  
Without Showman, a rolled dupe is **force re-rolled** into the **same type + same rarity pool** (you do not keep seeing the same named joker over and over).

**Engine law (Motely today):** search is **raw / stateless**. The shop stream is scored **as if Showman is already on** — Motely does **not** simulate “no Showman → force re-roll out of this exact card.”  
So:

| Layer | Truth |
|-------|--------|
| **Filter hits multi-Oops** | Raw stream *can* print that many Oops (Showman-shaped / unconstrained) |
| **Actual run without Showman** | Those later Oops may **never appear** (force re-roll) |
| **Playable multi-dupe package** | You still want **Showman on the seed** (must/should) so the raw hits are **real in-game** |

You cannot yet “search the no-Showman universe.” You search raw, then **pin Showman** when the package needs dupes to survive contact with the real game.

Bots forget this and report multi-Oops gods that are **paper only**.

---

## 5. Negative Tag (write this too)

- **Negative Tag does not appear on ante 1.**  
- Author **`antes: [2, 3, 4, 5, 6, 7, 8]`** — do **not** ride default 1–8 for Neg.
- **smallBlindTag** vs **bigBlindTag** matter. Analyzer line is usually `Tags: small, big`.
- “Free Neg joker” path = you care about **NegativeTag**, not vibes.

Parent scope trick:

```yaml
and:
  antes: [2, 3, 4, 5, 6, 7, 8]   # once — passes into bare nested arms
  clauses:
    - smallBlindTag: NegativeTag
    - joker: Showman
    - uncommonJoker: OopsAll6s
      min: 4
      sources: { shopItems: [0, 1, 2, ...] }  # deep if you reroll hard
```

Child with its own `antes:` **overrides**. Nested `and:`/`or:` inherit parent antes when bare.

---

## 6. Shop depth / reroll chunks

Default joker sources are often **shop slots 0–7** only.  
Good players **reroll deep** (ante 8 can go *far*). If your filter stops at slot 7, you miss god seeds.

Pattern:

```yaml
or:
  mode: max          # best window only
  # mode: sum        # total all windows
  antes: [1, 2, 3, 4, 5, 6, 7, 8]
  clauses:
    - uncommonJoker: OopsAll6s
      sources: { shopItems: [0, 1, 2, 3, 4, 5, 6, 7] }
    - uncommonJoker: OopsAll6s
      sources: { shopItems: [8, 9, 10, 11, 12, 13, 14, 15] }
    # …more 8-wide chunks
```

| mode | Meaning |
|------|---------|
| **sum** | Total juice across every chunk that hits |
| **max** | Score only the **best** chunk (“land on the best reroll band”) |

For **must count** (e.g. four Oops total), use `min: 4` on a clause whose `shopItems` **union** covers the deep stream — `or mode` alone is not “min 4 occurrences.”

---

## 7. CLI daily door (use Release)

```bash
dotnet run -c Release --project Motely.CLI -- \
  --jaml JamlFilters/YourFilter.jaml \
  --startSeed <LEFT_OFF> \
  --threads 7 \
  2>&1 | tee Seeds/run.log

# top hits
grep -E '^[A-Z0-9]+,' Seeds/run.log | sort -t, -k2 -nr | head -20
```

| Mode | Truth |
|------|--------|
| **Sequential** | Fast path (shared seed prefix cache) |
| Keywords / random / list | Different providers; more threads can **slow** list locks |
| **Debug** `dotnet run` | Slow. Always **`-c Release`** for real hunts |
| **Analyze** | `--analyze SEED --deck X --stake Y` — human text of antes/shops/tags |

Prove a seed:

```bash
dotnet run -c Release --project Motely.CLI -- \
  --jaml JamlFilters/YourFilter.jaml \
  --seeds 1F5WEAYR -q
```

---

## 8. Description blocks (for humans + RAG)

`description:` is not marketing. It is **load-bearing** for future you and for bots.

```yaml
description: >
  INTENT: Neg free multi-Oops with Showman (dupe path).
  MUST: smallBlind NegativeTag antes 2-8; Showman; OopsAll6s min 4 deep shop.
  SHOULD: or mode max shop chunks; score best window.
  DECK/STAKE: Anaglyph / White.
  SEEDS: 1F5WEAYR (R1: Perkeo Soul A1, Showman A4, Neg A6, multi Oops).
  NOT: Oops without Showman; Neg on ante 1; Red deck unless re-proven.
```

If description lies, RAG lies, Claude lies, you rage.

---

## 9. Workflow that doesn’t waste money

1. **Name the package** in one sentence.  
2. **Write must** (and: for joint packages).  
3. **Sanity on 1–20 known seeds** (`--seeds a,b,c`) before a long sequential.  
4. **Analyze** 1–2 hits — confirm Showman / Neg blind / shop slots.  
5. **Pin seeds:** in the `.jaml` under `seeds:`.  
6. **Play** same deck/stake.  
7. **Stop** when proof is green. One ticket. No parallel matrix theater.

Long random API agent loops with no filter and no Release CLI = **lighting money on fire**. Motely CLI is the door; agents are optional mules.

---

## 10. Example god-shaped hit (proven this tree)

**Seed `1F5WEAYR` · Anaglyph · White**

| Piece | Note |
|-------|------|
| Soul / Perkeo | Ante 1 Jumbo Arcana includes The Soul; engine matches Perkeo |
| Showman | Ante 4 shop ~5 — makes multi-Oops **playable** (engine already assumed raw/Showman-shaped) |
| Negative Tag | Small blind ante 6 |
| Oops! All 6s | Multiple shop hits (deep slots included) |

Style: dicetrick / Neg free Oops / Showman stack. Re-score after any filter edit.

---

## 11. Stickers (Black stake) — short

**Black stake** puts **stickers** on jokers. Wire: `stickers: [Eternal]` (see `MotelyJokerSticker`).

| | |
|--|--|
| **Eternal** | Cannot sell / destroy / harm that joker. Usually annoying. |
| **OP when** | The joker is the build forever — e.g. **Eternal Madness** (keeps scaling, never leave). Similar *feel* to a hard **Joker Stencil** identity build. |
| **Search** | `stake: Black` + `joker: Madness` + `stickers: [Eternal]`. Example filter: `JamlFilters/01_MadnessMonday.jaml`. |

Use **LSP (vscode-jaml)** for clause keys/enums — sticker names and stake are easy to typo.

---

## 12. Related docs

| Doc | Use |
|-----|-----|
| `docs/BALATRO-SEED-FINDING-CONFUSIONS.md` | Curse of knowledge + bot fails |
| `JAML.md` | How to write filters |
| `HANDOFF-MATRIX.md` | Agent law, open tickets, CLI door |
| `vscode-jaml` / Motely.Lsp | Completions + diagnostics — **use it** when editing `.jaml` |
| `~/Documents/GitHub/seedfinder-app/corpus/` | Game-item RAG chunks (not Motely grammar) |

---

*Honest guide. Update when a package law changes (e.g. ante rules), not when a bot feels poetic.*
