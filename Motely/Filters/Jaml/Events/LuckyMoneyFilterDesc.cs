using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class LuckyMoneyClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Rolls { get; set; } = [];
    public int Luck { get; set; } = 1;
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
                _clause.Rolls,
                _clause.Min,
                (ref MotelyVectorSearchContext sctx, ref MotelyVectorPrngStream stream) =>
                    sctx.GetNextLuckyMoney(ref stream, luck),
                ref stream
            );
        }
    }
}
