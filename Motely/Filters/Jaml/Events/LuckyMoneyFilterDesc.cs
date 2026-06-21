using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class LuckyMoneyClause : RollClause
{
}

public struct LuckyMoneyFilterDesc(LuckyMoneyClause clause)
    : IMotelySeedFilterDesc<LuckyMoneyFilterDesc.LuckyMoneyFilter>
{
    private readonly LuckyMoneyClause _clause = clause;

    public LuckyMoneyFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(_clause);

    public struct LuckyMoneyFilter(LuckyMoneyClause clause) : IMotelySeedFilter
    {
        private readonly LuckyMoneyClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateLuckyCardMoneyStream(isCached: false);
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
                        sctx.GetNextLuckyMoney(ref stream, luck);
                    return sctx.GetNextLuckyMoney(ref stream, luck);
                },
                ref stream
            );
        }
    }
}
