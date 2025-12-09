using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on Erratic Deck starting composition - RANK only.
/// Counts how many cards of specific rank(s) appear in the 52-card starting deck.
/// </summary>
public struct MotelyJsonErraticRankFilterDesc(MotelyPlayingCardRank rank, int minCount)
    : IMotelySeedFilterDesc<MotelyJsonErraticRankFilterDesc.MotelyJsonErraticRankFilter>
{
    private readonly MotelyPlayingCardRank _rank = rank;
    private readonly int _minCount = minCount;

    public MotelyJsonErraticRankFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        ctx.CacheErraticDeckPrngStream();
        return new MotelyJsonErraticRankFilter(_rank, _minCount);
    }

    public struct MotelyJsonErraticRankFilter : IMotelySeedFilter
    {
        private readonly MotelyPlayingCardRank _rank;
        private readonly int _minCount;

        public MotelyJsonErraticRankFilter(MotelyPlayingCardRank rank, int minCount)
        {
            _rank = rank;
            _minCount = minCount;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Vector256<int> count = Vector256<int>.Zero;

            // Loop through all 52 cards and count matching ranks
            var stream = ctx.CreateErraticDeckPrngStream(true);
            for (int cardIndex = 0; cardIndex < 52; cardIndex++)
            {
                var card = ctx.GetNextErraticDeckCard(ref stream);

                // Increment count for matching cards
                count += Vector256.ConditionalSelect(
                    VectorEnum256.Equals(card.PlayingCardRank, _rank),
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
public struct MotelyJsonErraticRankFilterClause
{
    public MotelyPlayingCardRank Rank { get; set; }
    public int MinCount { get; set; }
}
