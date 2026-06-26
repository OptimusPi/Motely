using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class LuckyMultClause : IJamlClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Rolls { get; set; } = [];
    public int Luck { get; set; } = 1;
}

public struct LuckyMultFilterDesc(LuckyMultClause clause)
    : IMotelySeedFilterDesc<LuckyMultFilterDesc.LuckyMultFilter>
{
    private readonly LuckyMultClause _clause = clause;

    public LuckyMultFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(_clause);

    public struct LuckyMultFilter(LuckyMultClause clause) : IMotelySeedFilter
    {
        private readonly LuckyMultClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateLuckyCardMultStream(isCached: false);
            double luck = _clause.Luck;
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause.Rolls,
                _clause.Min,
                (ref MotelyVectorSearchContext sctx, ref MotelyVectorPrngStream stream) =>
                    sctx.GetNextLuckyMult(ref stream, luck),
                ref stream
            );
        }
    }
}
