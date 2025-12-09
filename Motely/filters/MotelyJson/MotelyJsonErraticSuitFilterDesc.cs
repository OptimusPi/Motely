using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on Erratic Deck starting composition - SUIT only.
/// Counts how many cards of specific suit(s) appear in the 52-card starting deck.
/// </summary>
public struct MotelyJsonErraticSuitFilterDesc(MotelyPlayingCardSuit suit, int minCount)
    : IMotelySeedFilterDesc<MotelyJsonErraticSuitFilterDesc.MotelyJsonErraticSuitFilter>
{
    private readonly MotelyPlayingCardSuit _suit = suit;
    private readonly int _minCount = minCount;

    public MotelyJsonErraticSuitFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        ctx.CacheErraticDeckPrngStream();
        return new MotelyJsonErraticSuitFilter(_suit, _minCount);
    }

    public struct MotelyJsonErraticSuitFilter : IMotelySeedFilter
    {
        private readonly MotelyPlayingCardSuit _suit;
        private readonly int _minCount;

        public MotelyJsonErraticSuitFilter(MotelyPlayingCardSuit suit, int minCount)
        {
            _suit = suit;
            _minCount = minCount;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Vector256<int> count = Vector256<int>.Zero;

            // Loop through all 52 cards and count matching suits
            var stream = ctx.CreateErraticDeckPrngStream(true);
            for (int cardIndex = 0; cardIndex < 52; cardIndex++)
            {
                var card = ctx.GetNextErraticDeckCard(ref stream);

                // Increment count for matching cards
                count += Vector256.ConditionalSelect(
                    VectorEnum256.Equals(card.PlayingCardSuit, _suit),
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
/// Simple clause struct for combined ErraticRankAndSuit filter
/// </summary>
public struct MotelyJsonErraticSuitFilterClause
{
    public MotelyPlayingCardSuit Suit { get; set; }
    public int MinCount { get; set; }
}
