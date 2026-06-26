using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class BusinessPayoutClause : IJamlClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Rolls { get; set; } = [];
    // No Luck. Business Card is flat 50/50 (Chance = 2) — one Oops saturates to
    // guaranteed, so luck is binary, not a dial. The field is gone by construction,
    // not inherited-then-forbidden.
}

public struct BusinessPayoutFilterDesc(BusinessPayoutClause clause)
    : IMotelySeedFilterDesc<BusinessPayoutFilterDesc.BusinessPayoutFilter>
{
    private readonly BusinessPayoutClause _clause = clause;

    public BusinessPayoutFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(_clause);

    public struct BusinessPayoutFilter(BusinessPayoutClause clause) : IMotelySeedFilter
    {
        private readonly BusinessPayoutClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateBusinessPrngStream();
            // Business Card is a flat 50/50 (Chance = 2) — no luck/Oops multiplier.
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause.Rolls,
                _clause.Min,
                (ref sctx, ref stream) => sctx.GetNextBusinessPayout(ref stream),
                ref stream
            );
        }
    }
}
