using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class ParkingPayoutClause : RollClause
{
}

public struct ParkingPayoutFilterDesc(ParkingPayoutClause clause)
    : IMotelySeedFilterDesc<ParkingPayoutFilterDesc.ParkingPayoutFilter>
{
    private readonly ParkingPayoutClause _clause = clause;

    public ParkingPayoutFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(_clause);

    public struct ParkingPayoutFilter(ParkingPayoutClause clause) : IMotelySeedFilter
    {
        private readonly ParkingPayoutClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateParkingPrngStream();
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
                        sctx.GetNextParkingPayout(ref stream, luck);
                    return sctx.GetNextParkingPayout(ref stream, luck);
                },
                ref stream
            );
        }
    }
}
