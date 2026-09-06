# check_shop — the reroll checker (design draft)

Drafted by Claude (Cowork) after a live OMOANV53 run with Nat & Lola, 2026-09-05.
Status: proposal. Nothing implemented.

## The problem this solves

`analyze_seed` answers "what does this seed contain" and returns a flat 50-item stream per ante.
The question at the table is smaller and sharper, and it came up three times tonight:

- "I'm about to skip for the Negative tag — which joker is going to eat it?"
- "How many rerolls until Oops! All 6s shows up in this ante, and with my slot count?"
- "I have $643 in ante 8. What is actually reachable?"

Answering those from the flat stream means the *model* counts items by hand, guesses the slot
width, and pastes 8 KB of JSON into the chat to do it. The engine already knows the answer;
the tool should say it in ~200 bytes.

## Shape

One tool, `check_shop`. Inputs are the run state a player can read off their screen.
Output is folded into rerolls, not a flat stream.

```csharp
[McpServerTool(Name = "check_shop")]
[Description("Fold one ante's shop stream into rerolls for a given seed and run state. "
  + "Answers: what is in each reroll, which reroll first shows a wanted item, and which "
  + "joker will consume a pending edition tag. Small output by design.")]
public static string CheckShop(
    string seed,
    int ante,
    string deck = "Red",
    string stake = "White",
    // Run state the player can see:
    int shopSlots = 2,                 // 2 base, 3 with Overstock, 4 with Overstock Plus
    int maxRerolls = 12,               // how deep to fold; bounded so output stays small
    string[]? pendingTags = null,      // e.g. ["NegativeTag"] — tags held when entering this shop
    // Question:
    string[]? find = null,             // item names to locate, engine PascalCase (OopsAll6s)
    bool listAll = false               // dump every reroll (still bounded by maxRerolls)
)
```

### Output (listAll=false, find=["OopsAll6s"], pendingTags=["NegativeTag"])

```json
{
  "seed": "OMOANV53", "ante": 6, "shopSlots": 2,
  "opening": ["Earth", "TheDevil"],
  "tagLandsOn": [{ "tag": "NegativeTag", "item": "Matador", "reroll": 1, "slot": 0 }],
  "found": [
    { "item": "OopsAll6s", "reroll": 5, "slot": 1 },
    { "item": "OopsAll6s", "reroll": 9, "slot": 0 }
  ],
  "rerollsFolded": 12
}
```

That's the whole ante-6 conversation from tonight in one object.

## Folding rules (where the correctness lives)

1. **Slot width.** Stream index → (reroll, slot) is `divmod(index, shopSlots)`. Reroll 0 is the
   opening shop. `shopSlots` is an input, not inferred: the engine can't know what vouchers
   the player bought, and a wrong width shifts every answer. The description must say this.
2. **Edition tags fire on the next base-edition joker generated, across rerolls.** Walk the
   stream in order; the first item with `type == joker && edition == None` consumes the first
   pending tag, the next such joker consumes the second, and so on. Non-joker items and jokers
   that already carry an edition are skipped, not consumed. This is exactly why the ante-6
   Negative tag went to Matador (reroll 1) and not to the Oops at reroll 5.
3. **Tags don't perturb the stream.** Applying an edition tag doesn't consume PRNG state in the
   engine, so folding is a pure view over the stream `analyze_seed` already produces. No new
   RNG, no new search. Bounded compute like `jamlyze_seeds`.
4. **`find` returns every occurrence up to `maxRerolls`**, not just the first — "there's another
   one at reroll 9" was the useful half of tonight's answer.
5. **Packs are out of scope** for v1. They ride a different stream and the question "which pack
   has The Soul" is `analyze_seed` with `include=["packs"]`. Don't bloat this tool.

## What it deliberately does not do

- No cost/money simulation. Reroll price escalates and vouchers change it; modelling that
  means modelling the whole run. Report reroll *index*; the player knows their wallet.
- No "should I skip" advice. Tool returns facts; the model at the other end does the judgement.
- No multi-ante. One ante per call keeps the output small and the arguments honest.

## Follow-ups worth a second tool, not this one

- `check_tag_path`: given pendingTags and a *range* of antes, report where each tag lands if
  the player skips at each blind. Needs the tag-acquisition order rules; separate design.
- `--check-shop` on the CLI, same folding, for people who live in the terminal.

## Test cases (from tonight, all verifiable against jamlyze output)

- OMOANV53 ante 2, slots=2, find=["Showman"] → reroll 0, slot 0.
- OMOANV53 ante 6, slots=2, pendingTags=["NegativeTag"] → lands on Matador, reroll 1.
- OMOANV53 ante 6, slots=2, find=["OopsAll6s"] → rerolls 5 and 9.
- OMOANV53 ante 6, slots=3, find=["OopsAll6s"] → rerolls 3 and 6 (index 11 → 3r2, index 18 → 6r0).
- Two pending Negative tags, ante 6 → Matador (r1 s0) and RiffRaff (r2 s0).
