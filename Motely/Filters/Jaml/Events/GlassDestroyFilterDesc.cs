using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class GlassDestroyClause : RollClause
{
}

public struct GlassDestroyFilterDesc(GlassDestroyClause clause)
    : IMotelySeedFilterDesc<GlassDestroyFilterDesc.GlassDestroyFilter>
{
    private readonly GlassDestroyClause _clause = clause;

    public GlassDestroyFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(_clause);

    public struct GlassDestroyFilter(GlassDestroyClause clause) : IMotelySeedFilter
    {
        private readonly GlassDestroyClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateGlassPrngStream();
            double luck = _clause.Luck;
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                (ref MotelyVectorSearchContext sctx, ref MotelyVectorPrngStream stream) =>
                    sctx.GetNextGlassDestroy(ref stream, luck),
                ref stream
            );
        }
    }
}
