using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters;

public sealed class ErraticRankClause : IJamlClause
{
    public string Label { get; init; } = "";
    public int Score { get; init; }
    public required MotelyPlayingCardRank Rank { get; init; }
    public int[] Antes { get; init; } = [];
    public int Min { get; init; } = 1;
}

public struct ErraticRankFilterDesc(ErraticRankClause clause)
    : IMotelySeedFilterDesc<ErraticRankFilterDesc.ErraticRankFilter>
{
    private readonly ErraticRankClause _clause = clause;

    public ErraticRankFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        ctx.CacheErraticDeckPrngStream();
        return new ErraticRankFilter(_clause);
    }

    public struct ErraticRankFilter(ErraticRankClause clause) : IMotelySeedFilter
    {
        private readonly ErraticRankClause _clause = clause;

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var clause = _clause;
            Vector256<int> count = Vector256<int>.Zero;
            var stream = ctx.CreateErraticDeckPrngStream(true);
            for (int cardIndex = 0; cardIndex < 52; cardIndex++)
            {
                var card = ctx.GetNextErraticDeckCard(ref stream);
                count += Vector256.ConditionalSelect(
                    VectorEnum256.Equals(card.PlayingCardRank, clause.Rank),
                    Vector256<int>.One,
                    Vector256<int>.Zero
                );
            }
            return Vector256.GreaterThanOrEqual(count, Vector256.Create(clause.Min));
        }
    }
}
