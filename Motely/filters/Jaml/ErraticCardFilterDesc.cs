using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Motely;
using static Motely.MotelyVectorUtils;

namespace Motely.Filters;

public struct ErraticCardFilterDesc(ErraticCardClause clause)
    : IMotelySeedFilterDesc<ErraticCardFilterDesc.ErraticCardFilter>
{
    private readonly ErraticCardClause _clause = clause;

    public ErraticCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        ctx.CacheErraticDeckPrngStream();
        return new ErraticCardFilter(_clause);
    }

    public struct ErraticCardFilter(ErraticCardClause clause) : IMotelySeedFilter
    {
        private readonly ErraticCardClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var clause = _clause;
            var min = Vector256.Create(clause.Min);

            var count = Vector256<int>.Zero;
            var stream = ctx.CreateErraticDeckPrngStream(true);
            for (int i = 0; i < 52; i++)
            {
                var card = ctx.GetNextErraticDeckCard(ref stream);
                
                VectorMask match = VectorMask.AllBitsSet;
                
                if (clause.Rank.HasValue)
                    match &= VectorEnum256.Equals(card.PlayingCardRank, clause.Rank.Value);
                
                if (clause.Suit.HasValue)
                    match &= VectorEnum256.Equals(card.PlayingCardSuit, clause.Suit.Value);

                count += Vector256.ConditionalSelect(
                    VectorMaskToConditionalSelectMask(match), 
                    Vector256<int>.One, 
                    Vector256<int>.Zero);
            }
            return new VectorMask(VectorizedComparisonToMask(Vector256.GreaterThanOrEqual(count, min)));
        }
    }
}
