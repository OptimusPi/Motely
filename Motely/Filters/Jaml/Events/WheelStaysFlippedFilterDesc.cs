using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class WheelStaysFlippedClause : IJamlClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Rolls { get; set; } = [];
    public int Luck { get; set; } = 1;
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
                _clause.Rolls,
                _clause.Min,
                (ref MotelyVectorSearchContext sctx, ref MotelyVectorPrngStream stream) =>
                    sctx.GetNextWheelStaysFlipped(ref stream, luck),
                ref stream
            );
        }
    }
}
