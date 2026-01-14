using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on Erratic Deck starting composition - specific CARD (rank + suit).
/// Counts how many times a specific card (e.g., "K_C" for King of Clubs, "2_H" for 2 of Hearts) appears in the 52-card starting deck.
/// </summary>
public struct MotelyJsonErraticCardFilterDesc(MotelyPlayingCardRank rank, MotelyPlayingCardSuit suit, int minCount)
    : IMotelySeedFilterDesc<MotelyJsonErraticCardFilterDesc.MotelyJsonErraticCardFilter>
{
    private readonly MotelyPlayingCardRank _rank = rank;
    private readonly MotelyPlayingCardSuit _suit = suit;
    private readonly int _minCount = minCount;

    public MotelyJsonErraticCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        ctx.CacheErraticDeckPrngStream();
        return new MotelyJsonErraticCardFilter(_rank, _suit, _minCount);
    }

    public struct MotelyJsonErraticCardFilter : IMotelySeedFilter
    {
        private readonly MotelyPlayingCardRank _rank;
        private readonly MotelyPlayingCardSuit _suit;
        private readonly int _minCount;

        public MotelyJsonErraticCardFilter(MotelyPlayingCardRank rank, MotelyPlayingCardSuit suit, int minCount)
        {
            _rank = rank;
            _suit = suit;
            _minCount = minCount;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Vector256<int> count = Vector256<int>.Zero;

            // Loop through all 52 cards and count matching cards (both rank AND suit)
            var stream = ctx.CreateErraticDeckPrngStream(true);
            for (int cardIndex = 0; cardIndex < 52; cardIndex++)
            {
                var card = ctx.GetNextErraticDeckCard(ref stream);

                // Increment count for matching cards (both rank AND suit must match)
                var rankMatch = VectorEnum256.Equals(card.PlayingCardRank, _rank);
                var suitMatch = VectorEnum256.Equals(card.PlayingCardSuit, _suit);
                var bothMatch = Vector256.BitwiseAnd(rankMatch, suitMatch);
                
                count += Vector256.ConditionalSelect(
                    bothMatch,
                    Vector256<int>.One,
                    Vector256<int>.Zero
                );
            }

            // Return mask where count >= minCount
            return Vector256.GreaterThanOrEqual(count, Vector256.Create(_minCount));
        }
    }
}

/// <summary>
/// Simple clause struct for ErraticCard filter
/// </summary>
public struct MotelyJsonErraticCardFilterClause
{
    public MotelyPlayingCardRank Rank { get; set; }
    public MotelyPlayingCardSuit Suit { get; set; }
    public int MinCount { get; set; }
}
