using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class WheelOfFortuneClause : RollClause
{
}

public struct WheelOfFortuneFilterDesc(WheelOfFortuneClause clause)
    : IMotelySeedFilterDesc<WheelOfFortuneFilterDesc.WheelOfFortuneFilter>
{
    private readonly WheelOfFortuneClause _clause = clause;

    public WheelOfFortuneFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(_clause);

    public struct WheelOfFortuneFilter(WheelOfFortuneClause clause) : IMotelySeedFilter
    {
        private readonly WheelOfFortuneClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateWheelOfFortuneStream();
            double luck = _clause.Luck;
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                (ref MotelyVectorSearchContext sctx, ref MotelyVectorPrngStream stream) =>
                {
                    var edition = sctx.GetNextWheelOfFortune(ref stream, luck);
                    return ~VectorEnum256.Equals(edition, MotelyItemEdition.None);
                },
                ref stream
            );
        }
    }
}
