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
            double luck = _clause.Luck;
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextBusinessPayout(ref stream, luck);
                    return sctx.GetNextBusinessPayout(ref stream, luck);
                },
                ref stream
            );
        }
    }
}
