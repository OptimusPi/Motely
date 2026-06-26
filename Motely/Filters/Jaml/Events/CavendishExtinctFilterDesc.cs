using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class CavendishExtinctClause : IJamlClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Rolls { get; set; } = [];
    public int Luck { get; set; } = 1;
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
                _clause.Rolls,
                _clause.Min,
                (ref MotelyVectorSearchContext sctx, ref MotelyVectorPrngStream stream) =>
                    sctx.GetNextCavendishExtinct(ref stream, luck),
                ref stream
            );
        }
    }
}
