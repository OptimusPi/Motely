using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class WheelStaysFlippedClause : RollClause
{
}

public struct WheelStaysFlippedFilterDesc(WheelStaysFlippedClause clause)
    : IMotelySeedFilterDesc<WheelStaysFlippedFilterDesc.WheelStaysFlippedFilter>
{
    private readonly WheelStaysFlippedClause _clause = clause;

    public WheelStaysFlippedFilter CreateFilter(ref MotelyFilterCreationContext ctx) =>
        new(_clause);

    public struct WheelStaysFlippedFilter(WheelStaysFlippedClause clause) : IMotelySeedFilter
    {
        private readonly WheelStaysFlippedClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateTheWheelPrngStream();
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
                        sctx.GetNextWheelStaysFlipped(ref stream, luck);
                    return sctx.GetNextWheelStaysFlipped(ref stream, luck);
                },
                ref stream
            );
        }
    }
}
