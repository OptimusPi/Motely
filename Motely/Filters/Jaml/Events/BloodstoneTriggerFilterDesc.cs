using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class BloodstoneTriggerClause : RollClause
{
}

public struct BloodstoneTriggerFilterDesc(BloodstoneTriggerClause clause)
    : IMotelySeedFilterDesc<BloodstoneTriggerFilterDesc.BloodstoneTriggerFilter>
{
    private readonly BloodstoneTriggerClause _clause = clause;

    public BloodstoneTriggerFilter CreateFilter(ref MotelyFilterCreationContext ctx) =>
        new(_clause);

    public struct BloodstoneTriggerFilter(BloodstoneTriggerClause clause) : IMotelySeedFilter
    {
        private readonly BloodstoneTriggerClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateBloodstonePrngStream();
            // Bloodstone is a flat 50/50 (Chance = 2) — no luck/Oops multiplier.
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
                        sctx.GetNextBloodstoneTrigger(ref stream);
                    return sctx.GetNextBloodstoneTrigger(ref stream);
                },
                ref stream
            );
        }
    }
}
