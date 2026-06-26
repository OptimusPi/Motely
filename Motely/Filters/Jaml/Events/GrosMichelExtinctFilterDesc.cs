using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class GrosMichelExtinctClause : RollClause
{
}

public struct GrosMichelExtinctFilterDesc(GrosMichelExtinctClause clause)
    : IMotelySeedFilterDesc<GrosMichelExtinctFilterDesc.GrosMichelExtinctFilter>
{
    private readonly GrosMichelExtinctClause _clause = clause;

    public GrosMichelExtinctFilter CreateFilter(ref MotelyFilterCreationContext ctx) =>
        new(_clause);

    public struct GrosMichelExtinctFilter(GrosMichelExtinctClause clause) : IMotelySeedFilter
    {
        private readonly GrosMichelExtinctClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateGrosMichelPrngStream(false);
            double luck = _clause.Luck;
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                (ref MotelyVectorSearchContext sctx, ref MotelyVectorPrngStream stream) =>
                    sctx.GetNextGrosMichelExtinct(ref stream, luck),
                ref stream
            );
        }
    }
}
