using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class CavendishExtinctClause : RollClause
{
}

public struct CavendishExtinctFilterDesc(CavendishExtinctClause clause)
    : IMotelySeedFilterDesc<CavendishExtinctFilterDesc.CavendishExtinctFilter>
{
    private readonly CavendishExtinctClause _clause = clause;

    public CavendishExtinctFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(_clause);

    public struct CavendishExtinctFilter(CavendishExtinctClause clause) : IMotelySeedFilter
    {
        private readonly CavendishExtinctClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateCavendishPrngStream(false);
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
                        sctx.GetNextCavendishExtinct(ref stream, luck);
                    return sctx.GetNextCavendishExtinct(ref stream, luck);
                },
                ref stream
            );
        }
    }
}
