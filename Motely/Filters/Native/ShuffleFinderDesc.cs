namespace Motely.Filters.Native;

/// <summary>
/// Native experimental filter — keeps using shared <see cref="MotelyPokerHandEval"/>.
/// Hardcoded royal-ish score target; prefer JAML <c>pokerHand:</c> for real searches.
/// </summary>
public struct ShuffleFinderFilterDesc()
    : IMotelySeedFilterDesc<ShuffleFinderFilterDesc.ShuffleFinderFilter>
{
    public readonly ShuffleFinderFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        return new ShuffleFinderFilter();
    }

    public struct ShuffleFinderFilter() : IMotelySeedFilter
    {
        // Back-compat aliases for any external callers that nested under ShuffleFinder.
        public enum HandType
        {
            HighCard = MotelyPokerHand.HighCard,
            Pair = MotelyPokerHand.Pair,
            TwoPair = MotelyPokerHand.TwoPair,
            ThreeOfAKind = MotelyPokerHand.ThreeOfAKind,
            Straight = MotelyPokerHand.Straight,
            Flush = MotelyPokerHand.Flush,
            FullHouse = MotelyPokerHand.FullHouse,
            FourOfAKind = MotelyPokerHand.FourOfAKind,
            StriaghtFlush = MotelyPokerHand.StraightFlush, // historical typo
            StraightFlush = MotelyPokerHand.StraightFlush,
        }

        public static MotelyPokerHandEval.HandInfo BestScore(Span<MotelyItem> hand) =>
            MotelyPokerHandEval.BestScore(hand);

        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            return searchContext.SearchIndividualSeeds(
                (MotelySingleSearchContext searchContext) =>
                {
                    MotelyItem[] deck = new MotelyItem[MotelyEnum<MotelyStandardCard>.ValueCount];

                    for (int i = 0; i < deck.Length; i++)
                        deck[i] = new(MotelyEnum<MotelyStandardCard>.Values[i]);

                    searchContext.Shuffle(MotelyPokerHandEval.ShuffleKeyForAnte(1), deck);

                    Span<MotelyItem> hand = deck.AsSpan().Slice(deck.Length - 13, 13);

                    int fiveCount = 0,
                        sevenCount = 0,
                        threeCount = 0;

                    foreach (MotelyItem item in hand)
                    {
                        switch (item.StandardcardRank)
                        {
                            case MotelyStandardcardRank.Seven:
                                ++sevenCount;
                                break;
                            case MotelyStandardcardRank.Five:
                                ++fiveCount;
                                break;
                            case MotelyStandardcardRank.Three:
                                ++threeCount;
                                break;
                        }
                    }

                    if (fiveCount < 2 || sevenCount < 2 || threeCount < 2)
                        return 0;

                    hand = deck.AsSpan().Slice(deck.Length - 21, 8);

                    return MotelyPokerHandEval.BestScore(hand).Score == 1208 ? 1 : 0; // Royal flush chips×mult
                }
            );
        }
    }
}
