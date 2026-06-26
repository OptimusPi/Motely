using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class ParkingPayoutClause : IJamlClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Rolls { get; set; } = [];
    // No Luck. Reserved Parking is flat 50/50 (Chance = 2) — one Oops saturates to
    // guaranteed, so luck is binary, not a dial. The field is gone by construction,
    // not inherited-then-forbidden.
}

public struct ParkingPayoutFilterDesc(ParkingPayoutClause clause)
    : IMotelySeedFilterDesc<ParkingPayoutFilterDesc.ParkingPayoutFilter>
{
    private readonly ParkingPayoutClause _clause = clause;

    public ParkingPayoutFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(_clause);

    public struct ParkingPayoutFilter(ParkingPayoutClause clause) : IMotelySeedFilter
    {
        private readonly ParkingPayoutClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateParkingPrngStream();
            // Reserved Parking is a flat 50/50 (Chance = 2) — no luck/Oops multiplier.
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause.Rolls,
                _clause.Min,
                (ref sctx, ref stream) => sctx.GetNextParkingPayout(ref stream),
                ref stream
            );
        }
    }
}
