using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters;

// ── ErraticCard clause definition ──

public sealed class ErraticCardClause : IJamlClause
{
    public string Label { get; init; } = "";
    public MotelyPlayingCardRank? Rank { get; init; }
    public MotelyPlayingCardSuit? Suit { get; init; }
    public int[] Antes { get; init; } = [];
    public int Min { get; init; } = 1;
}

// ── Event clause definitions ──

public interface IRollClause : IJamlClause { int[] Rolls { get; } }

public sealed class LuckyMoneyClause : IRollClause 
{ 
    public string Label { get; init; } = "";
    public required int[] Rolls { get; init; } 
    public int[] Antes { get; init; } = []; 
    public int Min { get; init; } = 1; 
}

public sealed class LuckyMultClause : IRollClause 
{ 
    public string Label { get; init; } = "";
    public required int[] Rolls { get; init; } 
    public int[] Antes { get; init; } = []; 
    public int Min { get; init; } = 1; 
}

public sealed class MisprintMultClause : IRollClause 
{ 
    public string Label { get; init; } = "";
    public required int[] Rolls { get; init; } 
    public int[] Antes { get; init; } = []; 
    public int Min { get; init; } = 1; 
}

public sealed class WheelOfFortuneClause : IRollClause 
{ 
    public string Label { get; init; } = "";
    public required int[] Rolls { get; init; } 
    public int[] Antes { get; init; } = []; 
    public int Min { get; init; } = 1; 
}

public sealed class CavendishExtinctClause : IRollClause 
{ 
    public string Label { get; init; } = "";
    public required int[] Rolls { get; init; } 
    public int[] Antes { get; init; } = []; 
    public int Min { get; init; } = 1; 
}

public sealed class GrosMichelExtinctClause : IRollClause 
{ 
    public string Label { get; init; } = "";
    public required int[] Rolls { get; init; } 
    public int[] Antes { get; init; } = []; 
    public int Min { get; init; } = 1; 
}

// ── 6 individual event filter descs (one per PRNG stream) ──

public struct LuckyMoneyFilterDesc(LuckyMoneyClause clause)
    : IMotelySeedFilterDesc<LuckyMoneyFilterDesc.LuckyMoneyFilter>
{
    private readonly LuckyMoneyClause _clause = clause;

    public LuckyMoneyFilter CreateFilter(ref MotelyFilterCreationContext ctx)
        => new(_clause);

    public struct LuckyMoneyFilter(LuckyMoneyClause clause) : IMotelySeedFilter
    {
        private readonly LuckyMoneyClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            return EventFilterUtils.ProcessRollClause(ref ctx, _clause, static (ref MotelyVectorSearchContext sctx, int rollIndex) =>
            {
                var stream = sctx.CreateLuckyCardMoneyStream(true);
                for (int i = 0; i < rollIndex; i++)
                    sctx.GetNextLuckyMoney(ref stream);
                return sctx.GetNextLuckyMoney(ref stream);
            });
        }
    }
}

public struct LuckyMultFilterDesc(LuckyMultClause clause)
    : IMotelySeedFilterDesc<LuckyMultFilterDesc.LuckyMultFilter>
{
    private readonly LuckyMultClause _clause = clause;

    public LuckyMultFilter CreateFilter(ref MotelyFilterCreationContext ctx)
        => new(_clause);

    public struct LuckyMultFilter(LuckyMultClause clause) : IMotelySeedFilter
    {
        private readonly LuckyMultClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            return EventFilterUtils.ProcessRollClause(ref ctx, _clause, static (ref MotelyVectorSearchContext sctx, int rollIndex) =>
            {
                var stream = sctx.CreateLuckyCardMultStream(true);
                for (int i = 0; i < rollIndex; i++)
                    sctx.GetNextLuckyMult(ref stream);
                return sctx.GetNextLuckyMult(ref stream);
            });
        }
    }
}

public struct MisprintMultFilterDesc(MisprintMultClause clause)
    : IMotelySeedFilterDesc<MisprintMultFilterDesc.MisprintMultFilter>
{
    private readonly MisprintMultClause _clause = clause;

    public MisprintMultFilter CreateFilter(ref MotelyFilterCreationContext ctx)
        => new(_clause);

    public struct MisprintMultFilter(MisprintMultClause clause) : IMotelySeedFilter
    {
        private readonly MisprintMultClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            return EventFilterUtils.ProcessRollClause(ref ctx, _clause, static (ref MotelyVectorSearchContext sctx, int rollIndex) =>
            {
                var stream = sctx.CreateMisprintPrngStream(true);
                for (int i = 0; i < rollIndex; i++)
                    sctx.GetNextMisprintMult(ref stream);
                var multValue = sctx.GetNextMisprintMult(ref stream);
                return Vector256.GreaterThanOrEqual(multValue, Vector256<int>.Zero);
            });
        }
    }
}

public struct WheelOfFortuneFilterDesc(WheelOfFortuneClause clause)
    : IMotelySeedFilterDesc<WheelOfFortuneFilterDesc.WheelOfFortuneFilter>
{
    private readonly WheelOfFortuneClause _clause = clause;

    public WheelOfFortuneFilter CreateFilter(ref MotelyFilterCreationContext ctx)
        => new(_clause);

    public struct WheelOfFortuneFilter(WheelOfFortuneClause clause) : IMotelySeedFilter
    {
        private readonly WheelOfFortuneClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            return EventFilterUtils.ProcessRollClause(ref ctx, _clause, static (ref MotelyVectorSearchContext sctx, int rollIndex) =>
            {
                var stream = sctx.CreateWheelOfFortuneStream(true);
                for (int i = 0; i < rollIndex; i++)
                    sctx.GetNextWheelOfFortune(ref stream);
                var edition = sctx.GetNextWheelOfFortune(ref stream);
                return ~VectorEnum256.Equals(edition, MotelyItemEdition.None);
            });
        }
    }
}

public struct CavendishExtinctFilterDesc(CavendishExtinctClause clause)
    : IMotelySeedFilterDesc<CavendishExtinctFilterDesc.CavendishExtinctFilter>
{
    private readonly CavendishExtinctClause _clause = clause;

    public CavendishExtinctFilter CreateFilter(ref MotelyFilterCreationContext ctx)
        => new(_clause);

    public struct CavendishExtinctFilter(CavendishExtinctClause clause) : IMotelySeedFilter
    {
        private readonly CavendishExtinctClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            return EventFilterUtils.ProcessRollClause(ref ctx, _clause, static (ref MotelyVectorSearchContext sctx, int rollIndex) =>
            {
                var stream = sctx.CreateCavendishPrngStream(true);
                for (int i = 0; i < rollIndex; i++)
                    sctx.GetNextCavendishExtinct(ref stream);
                return sctx.GetNextCavendishExtinct(ref stream);
            });
        }
    }
}

public struct GrosMichelExtinctFilterDesc(GrosMichelExtinctClause clause)
    : IMotelySeedFilterDesc<GrosMichelExtinctFilterDesc.GrosMichelExtinctFilter>
{
    private readonly GrosMichelExtinctClause _clause = clause;

    public GrosMichelExtinctFilter CreateFilter(ref MotelyFilterCreationContext ctx)
        => new(_clause);

    public struct GrosMichelExtinctFilter(GrosMichelExtinctClause clause) : IMotelySeedFilter
    {
        private readonly GrosMichelExtinctClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            return EventFilterUtils.ProcessRollClause(ref ctx, _clause, static (ref MotelyVectorSearchContext sctx, int rollIndex) =>
            {
                var stream = sctx.CreateGrosMichelPrngStream(true);
                for (int i = 0; i < rollIndex; i++)
                    sctx.GetNextGrosMichelExtinct(ref stream);
                return sctx.GetNextGrosMichelExtinct(ref stream);
            });
        }
    }
}

// ── Shared utility for roll-based event processing ──

internal static class EventFilterUtils
{
    internal delegate VectorMask RollChecker(ref MotelyVectorSearchContext ctx, int rollIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static VectorMask ProcessRollClause<TClause>(
        ref MotelyVectorSearchContext ctx,
        TClause clause,
        RollChecker checker)
        where TClause : IRollClause
    {
        Debug.Assert(clause.Rolls.Length > 0, "Event roll clause must provide at least one roll index.");

        var clauseMask = VectorMask.NoBitsSet;
        foreach (var rollIndex in clause.Rolls)
        {
            clauseMask |= checker(ref ctx, rollIndex);
        }

        return clauseMask;
    }
}
