using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class BusinessPayoutClause : RollClause
{
}

public struct BusinessPayoutFilterDesc(BusinessPayoutClause clause)
    : IMotelySeedFilterDesc<BusinessPayoutFilterDesc.BusinessPayoutFilter>
{
    private readonly BusinessPayoutClause _clause = clause;

    public BusinessPayoutFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(_clause);

    public struct BusinessPayoutFilter(BusinessPayoutClause clause) : IMotelySeedFilter
    {
        private readonly BusinessPayoutClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateBusinessPrngStream();
            // Business Card is a flat 50/50 (Chance = 2) — no luck/Oops multiplier.
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                (ref MotelyVectorSearchContext sctx, ref MotelyVectorPrngStream stream) =>
                    sctx.GetNextBusinessPayout(ref stream),
                ref stream
            );
        }
    }
}
