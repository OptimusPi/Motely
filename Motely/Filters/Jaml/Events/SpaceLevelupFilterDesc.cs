using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class SpaceLevelupClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Rolls { get; set; } = [];
    public int Luck { get; set; } = 1;
}

public struct SpaceLevelupFilterDesc(SpaceLevelupClause clause)
    : IMotelySeedFilterDesc<SpaceLevelupFilterDesc.SpaceLevelupFilter>
{
    private readonly SpaceLevelupClause _clause = clause;

    public SpaceLevelupFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(_clause);

    public struct SpaceLevelupFilter(SpaceLevelupClause clause) : IMotelySeedFilter
    {
        private readonly SpaceLevelupClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateSpacePrngStream();
            double luck = _clause.Luck;
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause.Rolls,
                _clause.Min,
                (ref MotelyVectorSearchContext sctx, ref MotelyVectorPrngStream stream) =>
                    sctx.GetNextSpaceLevelup(ref stream, luck),
                ref stream
            );
        }
    }
}
