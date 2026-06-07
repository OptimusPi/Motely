using System.ComponentModel;
using Motely.Filters.Jaml;

namespace Motely.Analysis;

/// <summary>
/// Filter descriptor for seed analysis
/// </summary>
public sealed class JamlyzerFilterDesc()
    : IMotelySeedFilterDesc<JamlyzerFilterDesc.JamlyzerFilter>
{
    public JamlyzerSnapshot? LastAnalysis { get; private set; } = null;

    /// <summary>
    /// Optional JAML lens. Items matching its <c>should</c> clauses get IsHighlighted/MatchedBy
    /// (the glow); null = a plain, dark board dump.
    /// </summary>
    public JamlConfig? Lens { get; init; }

    public JamlyzerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        return new JamlyzerFilter(this);
    }

    public readonly struct JamlyzerFilter(JamlyzerFilterDesc filterDesc) : IMotelySeedFilter
    {
        public JamlyzerFilterDesc FilterDesc { get; } = filterDesc;

        public readonly VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            return ctx.SearchIndividualSeeds(CheckSeed);
        }

        private ref struct AnteAnalysisState
        {
            public MotelySingleTarotStream ArcanaStream;
            public readonly bool HasArcanaStream => !ArcanaStream.IsNull;
            public MotelySinglePlanetStream CelestialStream;
            public readonly bool HasCelestialStream => !CelestialStream.IsNull;
            public MotelySingleSpectralStream SpectralStream;
            public readonly bool HasSpectralStream => !SpectralStream.IsNull;
            public MotelySingleStandardCardStream StandardStream;
            public readonly bool HasStandardStream => !StandardStream.IsInvalid;
            public MotelySingleJokerStream BuffoonStream;
            public readonly bool HasBuffoonStream => !BuffoonStream.IsNull;
            // Streams for tag-granted and soul-granted jokers
            public MotelySingleJokerFixedRarityStream RareTagJokerStream;
            public bool RareTagJokerStreamInitialized;
            public MotelySingleJokerFixedRarityStream UncommonTagJokerStream;
            public bool UncommonTagJokerStreamInitialized;
            public MotelySingleJokerFixedRarityStream LegendaryJokerStream;
            public bool LegendaryJokerStreamInitialized;
        }

        public readonly bool CheckSeed(ref MotelySingleSearchContext ctx)
        {
            JamlConfig? lens = FilterDesc.Lens;

            // Glow + the focused match list both come from the real scorer: run it once with a scoop
            // attached. scoopMatches = the flat "what the JAML matched" card; scoopLookup = the
            // shop/pack glow index for the ante-map. No lens = a plain dark board, no matches.
            IReadOnlyList<ScoopedMatch> scoopMatches = [];
            Dictionary<long, string>? scoopLookup = null;
            if (lens is not null)
                scoopMatches = RunScoop(ref ctx, lens, out scoopLookup);

            // Create voucher state to track activated vouchers across antes
            MotelyRunState voucherState = new();
            MotelySingleBossStream bossStream = ctx.CreateBossStream();

            // Get starting deck composition for all decks using high-level API
            var deckStream = ctx.CreateErraticDeckPrngStream(isCached: false);
            var deckCards = new List<string>();

            for (int i = 0; i < 52; i++)
            {
                var card = ctx.GetNextErraticDeckCard(ref deckStream);
                deckCards.Add(FormatCardString(card.StandardcardRank, card.StandardcardSuit));
            }

            string startingDeck = string.Join(",", deckCards);

            List<AnteSnapshot> antes = [];

            // Analyze each ante
            for (int ante = 1; ante <= 8; ante++)
            {
                AnteAnalysisState state = new()
                {
                    ArcanaStream = default,
                    CelestialStream = default,
                    SpectralStream = default,
                    StandardStream = MotelySingleStandardCardStream.Invalid,
                    BuffoonStream = default,
                };

                // Boss
                MotelyBossBlind boss = ctx.GetBossForAnte(ref bossStream, ante, ref voucherState);

                // Voucher - get with state for proper progression
                MotelyVoucher voucher = ctx.GetAnteFirstVoucher(ante, voucherState);
                voucherState.ActivateVoucher(voucher);

                // Tags
                MotelySingleTagStream tagStream = ctx.CreateTagStream(ante);

                MotelyTag smallTag = ctx.GetNextTag(ref tagStream);
                MotelyTag bigTag = ctx.GetNextTag(ref tagStream);

                // Materialize jokers granted by rare/uncommon tags
                SnapshotItem? smallTagGrantedJoker = null;
                SnapshotItem? bigTagGrantedJoker = null;

                // Check if tags grant jokers (tags can grant jokers at certain rarities)
                if (IsRareTag(smallTag))
                {
                    if (!state.RareTagJokerStreamInitialized)
                    {
                        state.RareTagJokerStream = ctx.CreateRareTagJokerStream(ante);
                        state.RareTagJokerStreamInitialized = true;
                    }
                    var jokerItem = ctx.GetNextJoker(ref state.RareTagJokerStream);
                    smallTagGrantedJoker = new SnapshotItem(jokerItem);
                }
                else if (IsUncommonTag(smallTag))
                {
                    if (!state.UncommonTagJokerStreamInitialized)
                    {
                        state.UncommonTagJokerStream = ctx.CreateUncommonTagJokerStream(ante);
                        state.UncommonTagJokerStreamInitialized = true;
                    }
                    var jokerItem = ctx.GetNextJoker(ref state.UncommonTagJokerStream);
                    smallTagGrantedJoker = new SnapshotItem(jokerItem);
                }

                if (IsRareTag(bigTag))
                {
                    if (!state.RareTagJokerStreamInitialized)
                    {
                        state.RareTagJokerStream = ctx.CreateRareTagJokerStream(ante);
                        state.RareTagJokerStreamInitialized = true;
                    }
                    var jokerItem = ctx.GetNextJoker(ref state.RareTagJokerStream);
                    bigTagGrantedJoker = new SnapshotItem(jokerItem);
                }
                else if (IsUncommonTag(bigTag))
                {
                    if (!state.UncommonTagJokerStreamInitialized)
                    {
                        state.UncommonTagJokerStream = ctx.CreateUncommonTagJokerStream(ante);
                        state.UncommonTagJokerStreamInitialized = true;
                    }
                    var jokerItem = ctx.GetNextJoker(ref state.UncommonTagJokerStream);
                    bigTagGrantedJoker = new SnapshotItem(jokerItem);
                }

                // Shop Queue
                MotelySingleShopItemStream shopStream = ctx.CreateShopItemStream(ante);

                int maxSlots = ante == 1 ? 15 : 50;
                SnapshotItem[] shopItems = new SnapshotItem[maxSlots];

                for (int i = 0; i < maxSlots; i++)
                {
                    shopItems[i] = Glow(
                        ctx.GetNextShopItem(ref shopStream),
                        ante,
                        MotelyMatchSource.Shop,
                        i,
                        -1,
                        scoopLookup
                    );
                }

                // Packs - Get the actual shop packs (not tag-generated ones)
                var packStream = ctx.CreateBoosterPackStream(ante);
                int maxPacks = ante == 1 ? 4 : 6;
                PackSnapshot[] packs = new PackSnapshot[maxPacks];

                // Get all packs up to the maximum
                for (int i = 0; i < maxPacks; i++)
                {
                    MotelyBoosterPack pack = ctx.GetNextBoosterPack(ref packStream);
                    MotelySingleItemSet packContent = GetPackContents(
                        ref ctx,
                        ante,
                        pack,
                        ref state,
                        out var grantedLegendaryJoker
                    );

                    var packCards = packContent.AsArray();
                    var glowedCards = new SnapshotItem[packCards.Length];
                    for (int c = 0; c < packCards.Length; c++)
                        glowedCards[c] = Glow(
                            packCards[c],
                            ante,
                            MotelyMatchSource.BoosterPack,
                            i,
                            c,
                            scoopLookup
                        );

                    packs[i] = new(pack, glowedCards, grantedLegendaryJoker);
                }

                // NOTE: Per-round hand draw not yet implemented - requires shuffle PRNG per round
                // For now, omitting DrawOrder as the previous implementation was incorrect
                // (it showed standard pack cards, not the actual hand draw)

                antes.Add(
                    new(
                        ante,
                        boss,
                        voucher,
                        smallTag,
                        bigTag,
                        shopItems,
                        packs,
                        null,
                        BossMatched: false,
                        VoucherMatched: false,
                        SmallBlindTagMatched: false,
                        BigBlindTagMatched: false,
                        SmallBlindTagGrantedJoker: smallTagGrantedJoker,
                        BigBlindTagGrantedJoker: bigTagGrantedJoker
                    )
                );
            }

            // For Erratic deck, include the full deck composition with breakdown
            // For other decks, the starting deck is always the same 52 standard cards
            string? deckComposition = ctx.Deck == MotelyDeck.Erratic ? startingDeck : null;
            string? deckBreakdown =
                ctx.Deck == MotelyDeck.Erratic ? GetErraticDeckBreakdown(deckCards) : null;

            FilterDesc.LastAnalysis = new(
                null,
                antes,
                ctx.Deck,
                deckComposition,
                deckBreakdown,
                scoopMatches
            );

            return false; // Always return false since we're just analyzing
        }

        /// <summary>
        /// Wraps a materialized board <paramref name="item"/> as a snapshot item, lighting it up
        /// (IsHighlighted + MatchedBy) when the real scorer matched a clause at this exact board
        /// location (<paramref name="ante"/>/<paramref name="source"/>/<paramref name="slot"/>/
        /// <paramref name="cardIndex"/>). The glow comes from the scoring path itself
        /// (<see cref="RunScoop"/>) — one source of truth, no parallel matcher. No scoop = dark.
        /// </summary>
        private static SnapshotItem Glow(
            MotelyItem item,
            int ante,
            MotelyMatchSource source,
            int slot,
            int cardIndex,
            Dictionary<long, string>? scoop
        )
        {
            if (
                scoop is not null
                && scoop.TryGetValue(ScoopKey(ante, source, slot, cardIndex), out string? label)
            )
                return new SnapshotItem(item, true, label);

            return new SnapshotItem(item);
        }

        /// <summary>Packs a board location into a dictionary key: ante | source | slot | cardIndex+1.</summary>
        private static long ScoopKey(int ante, MotelyMatchSource source, int slot, int cardIndex) =>
            ((long)ante << 40)
            | ((long)(int)source << 32)
            | ((long)(ushort)slot << 16)
            | (ushort)(cardIndex + 1);

        /// <summary>
        /// Runs the JAML scorer once over the seed with a <see cref="JamlScoop"/> attached and
        /// returns the flat match list (the focused "swipe card" payload) — every concrete thing the
        /// JAML's <c>must</c> + <c>should</c> clauses matched, scoped exactly as written, straight
        /// from the code that filters seeds. Also fills <paramref name="overlay"/>: shop/pack matches
        /// indexed by board location → clause label, for the ante-map glow. One source of truth.
        /// </summary>
        private static IReadOnlyList<ScoopedMatch> RunScoop(
            ref MotelySingleSearchContext ctx,
            JamlConfig lens,
            out Dictionary<long, string> overlay
        )
        {
            overlay = new Dictionary<long, string>();

            int mustCount = lens.Must.Count;
            int total = mustCount + lens.Should.Count;
            if (total == 0)
                return [];

            // Scoop must + should: the card shows every condition the JAML names (e.g. the must
            // `smallBlindTag: Negative Tag`), not only the scored shoulds. Index 0..mustCount-1 are
            // must clauses, the rest are should — the frontend maps the index back to its clause.
            var clauses = new IJamlClause[total];
            for (int i = 0; i < mustCount; i++)
                clauses[i] = lens.Must[i];
            for (int i = 0; i < lens.Should.Count; i++)
                clauses[mustCount + i] = lens.Should[i];

            var scoop = new JamlScoop();
            var runState = new MotelyRunState { ScoopSink = scoop };
            JamlScoring.PrepareRunState(ref ctx, clauses, ref runState);

            for (int i = 0; i < clauses.Length; i++)
            {
                scoop.CurrentClauseIndex = i;
                JamlScoring.CountRawOccurrences(ref ctx, clauses[i], ref runState);
            }

            IReadOnlyList<ScoopedMatch> matches = scoop.Matches;
            for (int m = 0; m < matches.Count; m++)
            {
                ScoopedMatch sm = matches[m];
                if (
                    sm.Source != MotelyMatchSource.Shop
                    && sm.Source != MotelyMatchSource.BoosterPack
                )
                    continue;

                long key = ScoopKey(sm.Ante, sm.Source, sm.Slot, sm.CardIndex);
                if (!overlay.ContainsKey(key))
                {
                    IJamlClause clause = clauses[sm.ClauseIndex];
                    overlay[key] = clause.Label ?? clause.Describe();
                }
            }

            return matches;
        }

        /// <summary>
        /// Gets the draw order of cards for a specific ante
        /// Format: "2_H,2_H,2_H,5_C" (comma-separated card strings)
        /// For Erratic Deck: order from starting deck
        /// For other decks: order from Standard Packs opened in this ante
        /// </summary>
        private static string? GetDrawOrderForAnte(
            ref MotelySingleSearchContext ctx,
            int ante,
            ref AnteAnalysisState state
        )
        {
            var drawCards = new List<string>();

            if (ctx.Deck == MotelyDeck.Erratic)
            {
                // For Erratic Deck, cards are drawn from the starting deck in order
                // We need to track cumulative draws across all antes up to this point
                var deckStream = ctx.CreateErraticDeckPrngStream(isCached: false);

                // Calculate cumulative cards drawn by this ante
                // Each ante typically draws 5 cards per hand, but we'll show the deck order
                // For simplicity, show first 52 cards (full deck) - the actual draw order
                for (int i = 0; i < 52; i++)
                {
                    var card = ctx.GetNextErraticDeckCard(ref deckStream);
                    drawCards.Add(FormatCardString(card.StandardcardRank, card.StandardcardSuit));
                }
            }
            else
            {
                // For standard decks, cards come from Standard Packs opened in this ante
                // Recreate the pack stream to get cards in order
                var packStream = ctx.CreateBoosterPackStream(ante);
                int maxPacks = ante == 1 ? 4 : 6;

                var standardStream = ctx.CreateStandardPackCardStream(ante);

                for (int i = 0; i < maxPacks; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (pack.GetPackType() == MotelyBoosterPackType.Standard)
                    {
                        var packSize = pack.GetPackSize();
                        var packContents = ctx.GetNextStandardPackContents(
                            ref standardStream,
                            packSize
                        );

                        foreach (var item in packContents.AsArray())
                        {
                            if (item.TypeCategory == MotelyItemTypeCategory.Standardcard)
                            {
                                drawCards.Add(
                                    FormatCardString(item.StandardcardRank, item.StandardcardSuit)
                                );
                            }
                        }
                    }
                }
            }

            return drawCards.Count > 0 ? string.Join(",", drawCards) : null;
        }

        /// <summary>
        /// Formats a card as "2_H" or "K_C" format
        /// </summary>
        private static string FormatCardString(
            MotelyStandardcardRank rank,
            MotelyStandardcardSuit suit
        )
        {
            var rankStr = rank switch
            {
                MotelyStandardcardRank.Two => "2",
                MotelyStandardcardRank.Three => "3",
                MotelyStandardcardRank.Four => "4",
                MotelyStandardcardRank.Five => "5",
                MotelyStandardcardRank.Six => "6",
                MotelyStandardcardRank.Seven => "7",
                MotelyStandardcardRank.Eight => "8",
                MotelyStandardcardRank.Nine => "9",
                MotelyStandardcardRank.Ten => "10",
                MotelyStandardcardRank.Jack => "J",
                MotelyStandardcardRank.Queen => "Q",
                MotelyStandardcardRank.King => "K",
                MotelyStandardcardRank.Ace => "A",
                _ => rank.ToString(),
            };
            var suitStr = suit switch
            {
                MotelyStandardcardSuit.Clubs => "C",
                MotelyStandardcardSuit.Diamonds => "D",
                MotelyStandardcardSuit.Hearts => "H",
                MotelyStandardcardSuit.Spades => "S",
                _ => suit.ToString(),
            };
            return $"{rankStr}_{suitStr}";
        }

        /// <summary>
        /// Gets a breakdown of ranks and suits for Erratic deck with asterisks marking the most common
        /// Uses ASCII suit symbols: ♣ ♦ ♥ ♠
        /// </summary>
        private static string GetErraticDeckBreakdown(List<string> deckCards)
        {
            // Count ranks and suits
            var rankCounts = new Dictionary<string, int>();
            var suitCounts = new Dictionary<char, int>
            {
                ['C'] = 0,
                ['D'] = 0,
                ['H'] = 0,
                ['S'] = 0,
            };

            foreach (var card in deckCards)
            {
                var parts = card.Split('_');
                if (parts.Length == 2)
                {
                    var rank = parts[0];
                    var suit = parts[1][0];

                    rankCounts[rank] = rankCounts.GetValueOrDefault(rank, 0) + 1;
                    if (suitCounts.ContainsKey(suit))
                        suitCounts[suit]++;
                }
            }

            // Find max counts for asterisks
            int maxRankCount = rankCounts.Values.Max();
            int maxSuitCount = suitCounts.Values.Max();

            var sb = new System.Text.StringBuilder();

            // Ranks breakdown (ordered: 2-10, J, Q, K, A)
            string[] rankOrder = ["2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A"];
            sb.AppendLine("Ranks:");
            foreach (var rank in rankOrder)
            {
                int count = rankCounts.GetValueOrDefault(rank, 0);
                string marker = count == maxRankCount && count > 0 ? "*" : "";
                sb.AppendLine($"  {rank, 2}: {count}{marker}");
            }

            // Suits breakdown with ASCII symbols
            sb.AppendLine("Suits:");
            var suitSymbols = new Dictionary<char, string>
            {
                ['C'] = "♣",
                ['D'] = "♦",
                ['H'] = "♥",
                ['S'] = "♠",
            };
            foreach (var (suit, symbol) in suitSymbols)
            {
                int count = suitCounts[suit];
                string marker = count == maxSuitCount && count > 0 ? "*" : "";
                sb.AppendLine($"  {symbol}: {count}{marker}");
            }

            return sb.ToString().TrimEnd();
        }

        private static MotelySingleItemSet GetPackContents(
            ref MotelySingleSearchContext ctx,
            int ante,
            MotelyBoosterPack pack,
            ref AnteAnalysisState state,
            out SnapshotItem? grantedLegendaryJoker
        )
        {
            grantedLegendaryJoker = null;
            var packType = pack.GetPackType();
            var packSize = pack.GetPackSize();

            switch (packType)
            {
                case MotelyBoosterPackType.Arcana:

                    if (!state.HasArcanaStream)
                        state.ArcanaStream = ctx.CreateArcanaPackTarotStream(ante);

                    var arcanaContents = ctx.GetNextArcanaPackContents(ref state.ArcanaStream, packSize);

                    // Check if this pack has The Soul
                    if (arcanaContents.Contains(MotelyItemType.TheSoul))
                    {
                        if (!state.LegendaryJokerStreamInitialized)
                        {
                            state.LegendaryJokerStream = ctx.CreateLegendaryJokerStream(ante);
                            state.LegendaryJokerStreamInitialized = true;
                        }

                        var legendaryJoker = ctx.GetNextJoker(ref state.LegendaryJokerStream);
                        grantedLegendaryJoker = new SnapshotItem(legendaryJoker);
                    }

                    return arcanaContents;

                case MotelyBoosterPackType.Celestial:

                    if (!state.HasCelestialStream)
                        state.CelestialStream = ctx.CreateCelestialPackPlanetStream(ante);

                    return ctx.GetNextCelestialPackContents(ref state.CelestialStream, packSize);

                case MotelyBoosterPackType.Spectral:

                    if (!state.HasSpectralStream)
                        state.SpectralStream = ctx.CreateSpectralPackSpectralStream(ante);

                    var spectralContents = ctx.GetNextSpectralPackContents(ref state.SpectralStream, packSize);

                    // Check if this pack has The Soul
                    if (spectralContents.Contains(MotelyItemType.TheSoul))
                    {
                        if (!state.LegendaryJokerStreamInitialized)
                        {
                            state.LegendaryJokerStream = ctx.CreateLegendaryJokerStream(ante);
                            state.LegendaryJokerStreamInitialized = true;
                        }

                        var legendaryJoker = ctx.GetNextJoker(ref state.LegendaryJokerStream);
                        grantedLegendaryJoker = new SnapshotItem(legendaryJoker);
                    }

                    return spectralContents;

                case MotelyBoosterPackType.Buffoon:

                    if (!state.HasBuffoonStream)
                        state.BuffoonStream = ctx.CreateBuffoonPackJokerStream(ante);

                    return ctx.GetNextBuffoonPackContents(ref state.BuffoonStream, packSize);

                case MotelyBoosterPackType.Standard:

                    if (!state.HasStandardStream)
                        state.StandardStream = ctx.CreateStandardPackCardStream(ante);

                    return ctx.GetNextStandardPackContents(ref state.StandardStream, packSize);

                default:
                    throw new InvalidEnumArgumentException();
            }
        }

        /// <summary>
        /// Determines if a tag is a rare tag that grants rare jokers
        /// </summary>
        private static bool IsRareTag(MotelyTag tag)
        {
            // Rare tags in Balatro: Rare Tag
            return tag == MotelyTag.RareTag;
        }

        /// <summary>
        /// Determines if a tag is an uncommon tag that grants uncommon jokers
        /// </summary>
        private static bool IsUncommonTag(MotelyTag tag)
        {
            // Uncommon tags in Balatro: Uncommon Tag
            return tag == MotelyTag.UncommonTag;
        }
    }
}
