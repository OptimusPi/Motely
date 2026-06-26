using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class BloodstoneTriggerClause : IJamlClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Rolls { get; set; } = [];
    // No Luck. Bloodstone is flat 50/50 (Chance = 2) — one Oops saturates to
    // guaranteed, so luck is binary, not a dial. The field is gone by construction,
    // not inherited-then-forbidden.
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
                _clause.Rolls,
                _clause.Min,
                (ref sctx, ref stream) => sctx.GetNextBloodstoneTrigger(ref stream),
                ref stream
            );
        }
    }
}
