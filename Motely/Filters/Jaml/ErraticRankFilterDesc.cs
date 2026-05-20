using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class ErraticRankClause : JamlClause
{
    public required MotelyStandardcardRank Rank { get; init; }

    public override int EstimatedCost => 4 + MaxAnte;
    public override string Describe() => $"erraticRank {Rank}";
    public override IMotelySeedFilterDesc CreateDesc() => new ErraticRankFilterDesc(this);
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
            MethodImplOptions.AggressiveInlining
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
                    VectorEnum256.Equals(card.StandardcardRank, clause.Rank),
                    Vector256<int>.One,
                    Vector256<int>.Zero
                );
            }
            return Vector256.GreaterThanOrEqual(count, Vector256.Create(clause.Min));
        }
    }
}
