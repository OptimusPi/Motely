using System.ComponentModel;

namespace Motely.Analysis;

/// <summary>
/// Filter descriptor for seed analysis
/// </summary>
public sealed class MotelyAnalyzerFilterDesc()
    : IMotelySeedFilterDesc<MotelyAnalyzerFilterDesc.AnalyzerFilter>
{
    public MotelySeedAnalysis? LastAnalysis { get; private set; } = null;

    public AnalyzerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        return new AnalyzerFilter(this);
    }

    public readonly struct AnalyzerFilter(MotelyAnalyzerFilterDesc filterDesc) : IMotelySeedFilter
    {
        public MotelyAnalyzerFilterDesc FilterDesc { get; } = filterDesc;

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
        }

        public readonly bool CheckSeed(ref MotelySingleSearchContext ctx)
        {
            // Create voucher state to track activated vouchers across antes
            MotelyRunState voucherState = new();
            MotelySingleBossStream bossStream = ctx.CreateBossStream();

            // Get starting deck composition for Erratic Deck
            string? startingDeck = null;
            if (ctx.Deck == MotelyDeck.Erratic)
            {
                var deckStream = ctx.CreateErraticDeckPrngStream(isCached: false);
                var deckCards = new List<string>();
                for (int i = 0; i < 52; i++)
                {
                    var card = ctx.GetNextErraticDeckCard(ref deckStream);
                    var rank = card.PlayingCardRank;
                    var suit = card.PlayingCardSuit;
                    // Format as "2_H" (2 of Hearts) or "K_C" (King of Clubs)
                    var rankStr = rank switch
                    {
                        MotelyPlayingCardRank.Two => "2",
                        MotelyPlayingCardRank.Three => "3",
                        MotelyPlayingCardRank.Four => "4",
                        MotelyPlayingCardRank.Five => "5",
                        MotelyPlayingCardRank.Six => "6",
                        MotelyPlayingCardRank.Seven => "7",
                        MotelyPlayingCardRank.Eight => "8",
                        MotelyPlayingCardRank.Nine => "9",
                        MotelyPlayingCardRank.Ten => "10",
                        MotelyPlayingCardRank.Jack => "J",
                        MotelyPlayingCardRank.Queen => "Q",
                        MotelyPlayingCardRank.King => "K",
                        MotelyPlayingCardRank.Ace => "A",
                        _ => rank.ToString()
                    };
                    var suitStr = suit switch
                    {
                        MotelyPlayingCardSuit.Club => "C",
                        MotelyPlayingCardSuit.Diamond => "D",
                        MotelyPlayingCardSuit.Heart => "H",
                        MotelyPlayingCardSuit.Spade => "S",
                        _ => suit.ToString()
                    };
                    deckCards.Add($"{rankStr}_{suitStr}");
                }
                startingDeck = string.Join(",", deckCards);
            }

            List<MotelyAnteAnalysis> antes = [];

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

                // Shop Queue
                MotelySingleShopItemStream shopStream = ctx.CreateShopItemStream(ante);

                int maxSlots = ante == 1 ? 15 : 50;
                MotelyItem[] shopItems = new MotelyItem[maxSlots];

                for (int i = 0; i < maxSlots; i++)
                {
                    shopItems[i] = ctx.GetNextShopItem(ref shopStream);
                }

                // Packs - Get the actual shop packs (not tag-generated ones)
                var packStream = ctx.CreateBoosterPackStream(ante);
                int maxPacks = ante == 1 ? 4 : 6;
                MotelyBoosterPackAnalysis[] packs = new MotelyBoosterPackAnalysis[maxPacks];

                // Get all packs up to the maximum
                for (int i = 0; i < maxPacks; i++)
                {
                    MotelyBoosterPack pack = ctx.GetNextBoosterPack(ref packStream);
                    MotelySingleItemSet packContent = GetPackContents(
                        ref ctx,
                        ante,
                        pack,
                        ref state
                    );

                    packs[i] = new(pack, packContent.AsArray());
                }

                // Get draw order for this ante (need to recreate pack stream since we already consumed it)
                string? drawOrder = GetDrawOrderForAnte(ref ctx, ante, ref state);

                antes.Add(new(ante, boss, voucher, smallTag, bigTag, shopItems, packs, drawOrder));
            }

            FilterDesc.LastAnalysis = new(null, antes, ctx.Deck, startingDeck);

            return false; // Always return false since we're just analyzing
        }

        /// <summary>
        /// Gets the draw order of cards for a specific ante
        /// Format: "2_H,2_H,2_H,5_C" (comma-separated card strings)
        /// For Erratic Deck: order from starting deck
        /// For other decks: order from Standard Packs opened in this ante
        /// </summary>
        private static string? GetDrawOrderForAnte(ref MotelySingleSearchContext ctx, int ante, ref AnteAnalysisState state)
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
                    drawCards.Add(FormatCardString(card.PlayingCardRank, card.PlayingCardSuit));
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
                        var packContents = ctx.GetNextStandardPackContents(ref standardStream, packSize);
                        
                        foreach (var item in packContents.AsArray())
                        {
                            if (item.TypeCategory == MotelyItemTypeCategory.PlayingCard)
                            {
                                drawCards.Add(FormatCardString(item.PlayingCardRank, item.PlayingCardSuit));
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
        private static string FormatCardString(MotelyPlayingCardRank rank, MotelyPlayingCardSuit suit)
        {
            var rankStr = rank switch
            {
                MotelyPlayingCardRank.Two => "2",
                MotelyPlayingCardRank.Three => "3",
                MotelyPlayingCardRank.Four => "4",
                MotelyPlayingCardRank.Five => "5",
                MotelyPlayingCardRank.Six => "6",
                MotelyPlayingCardRank.Seven => "7",
                MotelyPlayingCardRank.Eight => "8",
                MotelyPlayingCardRank.Nine => "9",
                MotelyPlayingCardRank.Ten => "10",
                MotelyPlayingCardRank.Jack => "J",
                MotelyPlayingCardRank.Queen => "Q",
                MotelyPlayingCardRank.King => "K",
                MotelyPlayingCardRank.Ace => "A",
                _ => rank.ToString()
            };
            var suitStr = suit switch
            {
                MotelyPlayingCardSuit.Club => "C",
                MotelyPlayingCardSuit.Diamond => "D",
                MotelyPlayingCardSuit.Heart => "H",
                MotelyPlayingCardSuit.Spade => "S",
                _ => suit.ToString()
            };
            return $"{rankStr}_{suitStr}";
        }

        private static MotelySingleItemSet GetPackContents(
            ref MotelySingleSearchContext ctx,
            int ante,
            MotelyBoosterPack pack,
            ref AnteAnalysisState state
        )
        {
            var packType = pack.GetPackType();
            var packSize = pack.GetPackSize();

            switch (packType)
            {
                case MotelyBoosterPackType.Arcana:

                    if (!state.HasArcanaStream)
                        state.ArcanaStream = ctx.CreateArcanaPackTarotStream(ante);

                    return ctx.GetNextArcanaPackContents(ref state.ArcanaStream, packSize);

                case MotelyBoosterPackType.Celestial:

                    if (!state.HasCelestialStream)
                        state.CelestialStream = ctx.CreateCelestialPackPlanetStream(ante);

                    return ctx.GetNextCelestialPackContents(ref state.CelestialStream, packSize);

                case MotelyBoosterPackType.Spectral:

                    if (!state.HasSpectralStream)
                        state.SpectralStream = ctx.CreateSpectralPackSpectralStream(ante);

                    return ctx.GetNextSpectralPackContents(ref state.SpectralStream, packSize);

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
    }
}
