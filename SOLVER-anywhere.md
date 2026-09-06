# Item-path solver ("anywhere", for real)

Goal: given a seed, a target item, and an ante window, return EVERY path by which the
player can obtain that item, as a list of concrete actions with cost. Then expose the
depth-1 version of the same thing to the search engine as `sources: anywhere`.

Blueprint (the website) answers "which streams contain X" for one seed. This answers
"what do I DO to get X", including chains (get Judgement -> roll it -> get Showman), and
then runs the same logic across the 2.3T keyspace inside a JAML filter.

## The alphabet — every stream the engine already has

From MotelySingleSearchContext.*.cs (single-seed API; vector twins exist for search):

Jokers
  CreateShopJokerStream(ante)            shop slots, rerolls
  CreateBuffoonPackJokerStream(ante)     Buffoon / Jumbo / Mega Buffoon packs
  CreateJudgementJokerStream(ante)       Judgement tarot -> random joker
  CreateWraithJokerStream(ante)          Wraith spectral -> random rare
  CreateLegendaryJokerStream(ante)       The Soul -> legendary
  CreateRareTagJokerStream(ante)         Rare Tag -> free rare in next shop
  CreateUncommonTagJokerStream(ante)     Uncommon Tag -> free uncommon in next shop
  CreateRiffRaffJokerStream(ante)        Riff-Raff on blind select -> 2 commons
  CreateUncommonShopJokerStream / RareShopJokerStream / CommonShopJokerStream
                                          rarity-pinned shop views, same stream family
Spectrals
  CreateSpectralPackSpectralStream(ante) Spectral packs
  CreateArcanaOmenSpectralStream(ante)   Omen Globe: spectrals inside Arcana packs
  CreateShopSpectralStream(ante)         Ghost deck shop spectrals
  CreateSixthSenseSpectralStream(ante)   Sixth Sense: single 6 -> spectral
  CreateSeanceSpectralStream(ante)       Seance: straight flush -> spectral
Tarots
  CreateArcanaPackTarotStream(ante)
  CreateShopTarotStream(ante)
  CreateEmperorTarotStream(ante)         The Emperor -> 2 tarots
  CreatePurpleSealTarotStream(ante)      discard purple seal -> tarot
Structure
  CreateShopItemStream(ante)             the interleaved shop (jokers/tarots/planets/...)
  CreateBoosterPackStream(ante)          which packs appear
  CreateTagStream(ante)                  small/big blind tags
  Vouchers, Boss                         per ante

## Each stream is a path node with a precondition

A path is: precondition(s) -> stream -> target. Preconditions are themselves items, so
the solver is a bounded backward search on this table.

  stream                 precondition                              player action
  ShopJoker              money, reroll count                       reroll N times, buy
  BuffoonPack            pack appears in ante (BoosterPackStream)  open pack, pick
  JudgementJoker         own Judgement                             use Judgement
  WraithJoker            own Wraith                                use Wraith
  LegendaryJoker         own The Soul                              use The Soul
  RareTagJoker           skip a blind whose tag is RareTag         skip, enter shop
  UncommonTagJoker       skip a blind whose tag is UncommonTag     skip, enter shop
  RiffRaffJoker          own Riff-Raff                             select a blind
  SixthSenseSpectral     own Sixth Sense + a single 6 first hand   play the 6
  SeanceSpectral         own Seance + straight flush               play it
  ArcanaOmenSpectral     Omen Globe voucher + Arcana pack          open pack
  EmperorTarot           own The Emperor                           use Emperor
  PurpleSealTarot        purple-sealed card in hand                discard it

Where a precondition is itself an item (Judgement, Wraith, Soul, Emperor, Riff-Raff,
Sixth Sense, Seance), recurse: Judgement comes from ShopTarot / ArcanaPack / Emperor /
PurpleSeal. Depth cap 3. Tags come from TagStream + which blind you skip. Money is a
budget, not an item; rerolls cost money.

## Solver API (single seed)

    public sealed record PathStep(int Ante, string Action, string Detail, int MoneyCost, int Skips);
    public sealed record ItemPath(MotelyItem Target, IReadOnlyList<PathStep> Steps, int TotalCost);

    public static class MotelyItemSolver
    {
        // Every path to `target` within [firstAnte, lastAnte], cheapest first.
        public static IReadOnlyList<ItemPath> Solve(
            MotelySingleSearchContext ctx,
            MotelyItem target,
            int firstAnte, int lastAnte,
            SolverBudget budget);   // maxRerollsPerShop, maxDepth (default 3), maxSkips
    }

Implementation shape: for each ante in window, for each stream in the table, ask the
stream whether `target` appears within the budget (rerolls / pack slots / tag pulls).
Each hit becomes a candidate path; resolve its precondition recursively into earlier-or-
same-ante paths. Return the union, sorted by TotalCost. Dedupe by (ante, stream, index).

Cost model, v1: reroll = escalating shop reroll price; skip = 1 skip; pack = pack price;
precondition item = its own path cost. Good enough to rank; refine later.

## Search-side: `sources: anywhere` is Solve() at depth 1

In the vector search, a clause with `sources: anywhere` expands to the OR of every
depth-1 stream in the table for that item kind — shop, packs, tag pulls, Judgement,
Wraith, Soul, Riff-Raff, Sixth Sense, Seance, Emperor. No recursion in the hot loop;
the vector contexts already exist for these streams so the cost is N stream checks per
seed, all SIMD. Depth>1 (chains) is single-seed-only, run on hits after the search.

    - joker: Showman
      antes: [1, 2, 3]
      sources: anywhere            # depth-1 union
    - joker: Showman
      antes: [1, 2, 3]
      sources:
        solve: { depth: 3 }        # post-filter on hits: full solver, keep seeds with a path

## CLI

    dotnet run --project Motely.CLI -- --solve Showman --seed KGBY8NE2 --antes 1-4
    -> prints each path as steps with cost, cheapest first, or "no path within budget".

    --anywhere    (already in BRAINSTORM-anywhere.md) makes sourceless clauses use depth-1.

## MCP

    solve_item(seed, item, firstAnte, lastAnte, budget) -> ItemPath[]
    Small output by construction: paths, not streams.

## Tests (verifiable today against jamlyze)

    OMOANV53  Showman   antes 1-2  -> ShopJoker ante 2 reroll 0 slot 0, cost $0 rerolls
    3Q3OGZ82  Showman   antes 0-2  -> three paths: ante 0 shop r3, ante 1 shop r1, ante 2 shop r0
    KGBY8NE2  Showman   antes 1-6  -> (whatever Blueprint says; if Blueprint finds one via a
                                      tag or Judgement and we don't, that stream is the bug)
    KGBY8NE2  OopsAll6s ante 4     -> ShopJoker ante 4: r1 s0, r1 s1, r2 s0

## Order of work

1. MotelyItemSolver, single-seed, jokers only, depth 1 (just the table, no recursion).
   Test against the four cases above.
2. Depth 2-3 recursion for Judgement / Wraith / Soul / tags.
3. `sources: anywhere` in JamlConfig -> vector expansion. This is the payoff.
4. --solve on the CLI, solve_item on the MCP.
5. Spectrals and tarots through the same table.
