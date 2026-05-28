using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

// ── ErraticCard clause definition ──

public sealed class ErraticCardClause : JamlClause
{
    public MotelyStandardcardRank? Rank { get; set; }
    public MotelyStandardcardSuit? Suit { get; set; }

    public override int EstimatedCost => 5 + MaxAnte;

    public override string Describe()
    {
        var parts = new System.Collections.Generic.List<string>(2);
        if (Rank.HasValue)
            parts.Add(Rank.Value.ToString());
        if (Suit.HasValue)
            parts.Add(Suit.Value.ToString());
        return $"erraticCard {(parts.Count == 0 ? "Any" : string.Join(" ", parts))}";
    }
}

// ── Event clause definitions ──

/// <summary>Marker interface: clauses driven by a roll-index array (event probes), not antes.</summary>
public interface IRollClause : IJamlClause
{
    int[] Rolls { get; }
}

public sealed class LuckyMoneyClause : RollClause
{
    public override string Describe() => "event LuckyMoney";
}

public sealed class LuckyMultClause : RollClause
{
    public override string Describe() => "event LuckyMult";
}

public sealed class MisprintMultClause : RollClause
{
    /// <summary>
    /// Specific mult value to match (0-23). If null, matches any value (always succeeds).
    /// </summary>
    public int? Value { get; set; }

    public override string Describe() => "event MisprintMult";
}

public sealed class WheelOfFortuneClause : RollClause
{
    public override string Describe() => "event WheelOfFortune";
}

public sealed class CavendishExtinctClause : RollClause
{
    public override string Describe() => "event CavendishExtinct";
}

public sealed class GrosMichelExtinctClause : RollClause
{
    public override string Describe() => "event GrosMichelExtinct";
}

public sealed class SpaceLevelupClause : RollClause
{
    public override string Describe() => "event SpaceLevelup";
}

public sealed class BusinessPayoutClause : RollClause
{
    public override string Describe() => "event BusinessPayout";
}

public sealed class BloodstoneTriggerClause : RollClause
{
    public override string Describe() => "event BloodstoneTrigger";
}

public sealed class ParkingPayoutClause : RollClause
{
    public override string Describe() => "event ParkingPayout";
}

public sealed class GlassDestroyClause : RollClause
{
    public override string Describe() => "event GlassDestroy";
}

public sealed class WheelStaysFlippedClause : RollClause
{
    public override string Describe() => "event WheelStaysFlipped";
}

// ── 6 individual event filter descs (one per PRNG stream) ──

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
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextLuckyMoney(ref stream);
                    return sctx.GetNextLuckyMoney(ref stream);
                },
                ref stream
            );
        }
    }
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
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextLuckyMult(ref stream);
                    return sctx.GetNextLuckyMult(ref stream);
                },
                ref stream
            );
        }
    }
}

public struct MisprintMultFilterDesc(MisprintMultClause clause)
    : IMotelySeedFilterDesc<MisprintMultFilterDesc.MisprintMultFilter>
{
    private readonly MisprintMultClause _clause = clause;

    public MisprintMultFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Pre-compute at creation time - no branching in SIMD hot path
        var targetValue = _clause.Value.HasValue
            ? Vector256.Create(_clause.Value.Value)
            : Vector256<int>.Zero;
        return new MisprintMultFilter(_clause, _clause.Value.HasValue, targetValue);
    }

    public struct MisprintMultFilter(
        MisprintMultClause clause,
        bool hasValue,
        Vector256<int> targetValue
    ) : IMotelySeedFilter
    {
        private readonly MisprintMultClause _clause = clause;
        private readonly bool _hasValue = hasValue;
        private readonly Vector256<int> _targetValue = targetValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateMisprintPrngStream();

            if (_hasValue)
            {
                return EventFilterUtils.ProcessRollClause(
                    ref ctx,
                    _clause,
                    static (
                        ref MotelyVectorSearchContext sctx,
                        ref MotelyVectorPrngStream stream,
                        int rollIndex,
                        Vector256<int> target
                    ) =>
                    {
                        for (int i = 0; i < rollIndex; i++)
                            sctx.GetNextMisprintMult(ref stream);
                        var multValue = sctx.GetNextMisprintMult(ref stream);
                        return Vector256.Equals(multValue, target);
                    },
                    ref stream,
                    _targetValue
                );
            }

            // Original behavior: matches any value (always succeeds if roll exists)
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextMisprintMult(ref stream);
                    var multValue = sctx.GetNextMisprintMult(ref stream);
                    return Vector256.GreaterThanOrEqual(multValue, Vector256<int>.Zero);
                },
                ref stream
            );
        }
    }
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
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextWheelOfFortune(ref stream);
                    var edition = sctx.GetNextWheelOfFortune(ref stream);
                    return ~VectorEnum256.Equals(edition, MotelyItemEdition.None);
                },
                ref stream
            );
        }
    }
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
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextCavendishExtinct(ref stream);
                    return sctx.GetNextCavendishExtinct(ref stream);
                },
                ref stream
            );
        }
    }
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
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextGrosMichelExtinct(ref stream);
                    return sctx.GetNextGrosMichelExtinct(ref stream);
                },
                ref stream
            );
        }
    }
}

public struct SpaceLevelupFilterDesc(SpaceLevelupClause clause)
    : IMotelySeedFilterDesc<SpaceLevelupFilterDesc.SpaceLevelupFilter>
{
    private readonly SpaceLevelupClause _clause = clause;

    public SpaceLevelupFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(_clause);

    public struct SpaceLevelupFilter(SpaceLevelupClause clause) : IMotelySeedFilter
    {
        private readonly SpaceLevelupClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateSpacePrngStream();
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextSpaceLevelup(ref stream);
                    return sctx.GetNextSpaceLevelup(ref stream);
                },
                ref stream
            );
        }
    }
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
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextBusinessPayout(ref stream);
                    return sctx.GetNextBusinessPayout(ref stream);
                },
                ref stream
            );
        }
    }
}

public struct BloodstoneTriggerFilterDesc(BloodstoneTriggerClause clause)
    : IMotelySeedFilterDesc<BloodstoneTriggerFilterDesc.BloodstoneTriggerFilter>
{
    private readonly BloodstoneTriggerClause _clause = clause;

    public BloodstoneTriggerFilter CreateFilter(ref MotelyFilterCreationContext ctx) =>
        new(_clause);

    public struct BloodstoneTriggerFilter(BloodstoneTriggerClause clause) : IMotelySeedFilter
    {
        private readonly BloodstoneTriggerClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateBloodstonePrngStream();
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextBloodstoneTrigger(ref stream);
                    return sctx.GetNextBloodstoneTrigger(ref stream);
                },
                ref stream
            );
        }
    }
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
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextParkingPayout(ref stream);
                    return sctx.GetNextParkingPayout(ref stream);
                },
                ref stream
            );
        }
    }
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
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextGlassDestroy(ref stream);
                    return sctx.GetNextGlassDestroy(ref stream);
                },
                ref stream
            );
        }
    }
}

public struct WheelStaysFlippedFilterDesc(WheelStaysFlippedClause clause)
    : IMotelySeedFilterDesc<WheelStaysFlippedFilterDesc.WheelStaysFlippedFilter>
{
    private readonly WheelStaysFlippedClause _clause = clause;

    public WheelStaysFlippedFilter CreateFilter(ref MotelyFilterCreationContext ctx) =>
        new(_clause);

    public struct WheelStaysFlippedFilter(WheelStaysFlippedClause clause) : IMotelySeedFilter
    {
        private readonly WheelStaysFlippedClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateTheWheelPrngStream();
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextWheelStaysFlipped(ref stream);
                    return sctx.GetNextWheelStaysFlipped(ref stream);
                },
                ref stream
            );
        }
    }
}

// ── Shared utility for roll-based event processing ──

internal static class EventFilterUtils
{
    internal delegate VectorMask RollChecker(
        ref MotelyVectorSearchContext ctx,
        ref MotelyVectorPrngStream stream,
        int rollIndex
    );

    internal delegate VectorMask RollCheckerWithValue(
        ref MotelyVectorSearchContext ctx,
        ref MotelyVectorPrngStream stream,
        int rollIndex,
        Vector256<int> value
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static VectorMask ProcessRollClause<TClause>(
        ref MotelyVectorSearchContext ctx,
        TClause clause,
        RollChecker checker,
        ref MotelyVectorPrngStream stream
    )
        where TClause : IRollClause
    {
        Debug.Assert(
            clause.Rolls.Length > 0,
            "Event roll clause must provide at least one roll index."
        );

        var matchCounts = Vector256<int>.Zero;
        var minVector = Vector256.Create(clause.Min);
        var rolls = clause.Rolls;
        for (int i = 0; i < rolls.Length; i++)
        {
            var rollIndex = rolls[i];
            var rollMask = checker(ref ctx, ref stream, rollIndex);
            matchCounts = Vector256.Add(
                matchCounts,
                Vector256.Create(
                    rollMask[0] ? 1 : 0,
                    rollMask[1] ? 1 : 0,
                    rollMask[2] ? 1 : 0,
                    rollMask[3] ? 1 : 0,
                    rollMask[4] ? 1 : 0,
                    rollMask[5] ? 1 : 0,
                    rollMask[6] ? 1 : 0,
                    rollMask[7] ? 1 : 0
                )
            );

            // SIMD Optimization: We can stop only if EVERY lane is decided.
            // A lane is decided if it already hit 'min' (and max is null)
            // OR if it's impossible to reach 'min'.
            // Checking this every iteration in SIMD is often slower than just finishing the loop
            // UNLESS the loop is long. For events, indices are usually small, but
            // if rolls.Length is large (like your 0-99 example), it's worth it.

            if (rolls.Length > 8)
            {
                int rollsRemaining = rolls.Length - 1 - i;
                var possibleMax = Vector256.Add(matchCounts, Vector256.Create(rollsRemaining));

                // maskHit: matchCounts >= min
                var maskHit = Vector256.GreaterThanOrEqual(matchCounts, minVector);
                // maskFail: current + remaining < min
                var maskFail = Vector256.LessThan(possibleMax, minVector);

                // Combined: lane is finished
                var combined = Vector256.BitwiseOr(maskHit, maskFail);
                if (combined.ExtractMostSignificantBits() == 0xFF)
                    break;
            }
        }

        return new VectorMask(
            MotelyVectorUtils.VectorizedComparisonToMask(
                Vector256.GreaterThan(
                    matchCounts,
                    Vector256.Subtract(Vector256.Create(clause.Min), Vector256.Create(1))
                )
            )
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static VectorMask ProcessRollClause<TClause>(
        ref MotelyVectorSearchContext ctx,
        TClause clause,
        RollCheckerWithValue checker,
        ref MotelyVectorPrngStream stream,
        Vector256<int> value
    )
        where TClause : IRollClause
    {
        Debug.Assert(
            clause.Rolls.Length > 0,
            "Event roll clause must provide at least one roll index."
        );

        var matchCounts = Vector256<int>.Zero;
        var minVector = Vector256.Create(clause.Min);
        var rolls = clause.Rolls;
        for (int i = 0; i < rolls.Length; i++)
        {
            var rollIndex = rolls[i];
            var rollMask = checker(ref ctx, ref stream, rollIndex, value);
            matchCounts = Vector256.Add(
                matchCounts,
                Vector256.Create(
                    rollMask[0] ? 1 : 0,
                    rollMask[1] ? 1 : 0,
                    rollMask[2] ? 1 : 0,
                    rollMask[3] ? 1 : 0,
                    rollMask[4] ? 1 : 0,
                    rollMask[5] ? 1 : 0,
                    rollMask[6] ? 1 : 0,
                    rollMask[7] ? 1 : 0
                )
            );

            if (rolls.Length > 8)
            {
                int rollsRemaining = rolls.Length - 1 - i;
                var possibleMax = Vector256.Add(matchCounts, Vector256.Create(rollsRemaining));
                var maskHit = Vector256.GreaterThanOrEqual(matchCounts, minVector);
                var maskFail = Vector256.LessThan(possibleMax, minVector);
                var combined = Vector256.BitwiseOr(maskHit, maskFail);
                if (combined.ExtractMostSignificantBits() == 0xFF)
                    break;
            }
        }

        return new VectorMask(
            MotelyVectorUtils.VectorizedComparisonToMask(
                Vector256.GreaterThan(
                    matchCounts,
                    Vector256.Subtract(Vector256.Create(clause.Min), Vector256.Create(1))
                )
            )
        );
    }
}
