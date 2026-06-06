using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class ErraticSuitClause : JamlClause
{
    public required MotelyStandardcardSuit Suit { get; set; }

    public override int EstimatedCost => 4 + MaxAnte;

    public override string Describe() => $"erraticSuit {Suit}";
}

public struct ErraticSuitFilterDesc(ErraticSuitClause clause)
    : IMotelySeedFilterDesc<ErraticSuitFilterDesc.ErraticSuitFilter>
{
    private readonly ErraticSuitClause _clause = clause;

    public ErraticSuitFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        ctx.CacheErraticDeckPrngStream();
        return new ErraticSuitFilter(_clause);
    }

    public struct ErraticSuitFilter(ErraticSuitClause clause) : IMotelySeedFilter
    {
        private readonly ErraticSuitClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var clause = _clause;
            Vector256<int> count = Vector256<int>.Zero;
            var stream = ctx.CreateErraticDeckPrngStream(true);
            for (int cardIndex = 0; cardIndex < 52; cardIndex++)
            {
                var card = ctx.GetNextErraticDeckCard(ref stream);
                count += Vector256.ConditionalSelect(
                    VectorEnum256.Equals(card.StandardcardSuit, clause.Suit),
                    Vector256<int>.One,
                    Vector256<int>.Zero
                );
            }
            return Vector256.GreaterThanOrEqual(count, Vector256.Create(clause.Min));
        }
    }
}
