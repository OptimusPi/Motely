using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using static Motely.MotelyVectorUtils;

namespace Motely.Filters.Jaml;

public sealed class VoucherClause : IJamlClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public required MotelyVoucher[] Vouchers { get; set; }

    /// <summary>
    /// Voucher-stream indices per ante: 0 = ante award, 1+ = further draws on that ante's
    /// voucher stream (Hieroglyph bonus, voucher-tag shop extras, etc.).
    /// </summary>
    public required int[] Rolls { get; set; }
}

public struct VoucherFilterDesc(VoucherClause clause)
    : IMotelySeedFilterDesc<VoucherFilterDesc.VoucherFilter>
{
    private readonly VoucherClause _clause = clause;

    public readonly VoucherFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        int maxAnte = 0;
        for (int i = 0; i < _clause.Antes.Length; i++)
        {
            if (_clause.Antes[i] > maxAnte)
                maxAnte = _clause.Antes[i];
        }

        // Cache all antes 1..maxAnte so state-building passes on non-target antes work.
        for (int ante = 1; ante <= maxAnte; ante++)
            ctx.CacheAnteFirstVoucher(ante);

        return new VoucherFilter(_clause, maxAnte);
    }

    public struct VoucherFilter(VoucherClause clause, int maxAnte) : IMotelySeedFilter
    {
        private readonly VoucherClause _clause = clause;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Vouchers.Length > 0);

            var clause = _clause;
            int maxAnte = _maxAnte;

            Vector256<int> matchCounts = Vector256<int>.Zero;
            var voucherState = new MotelyVectorRunState();

            for (int ante = 1; ante <= maxAnte; ante++)
            {
                bool isTarget = false;
                for (int i = 0; i < clause.Antes.Length; i++)
                {
                    if (clause.Antes[i] == ante)
                    {
                        isTarget = true;
                        break;
                    }
                }
                var vouchers = ctx.GetAnteFirstVoucher(ante, voucherState);
                voucherState.ActivateVoucher(vouchers);

                if (!isTarget)
                    continue;

                matchCounts = AccumulateVoucherRolls(
                    ref ctx,
                    ante,
                    ref voucherState,
                    vouchers,
                    clause,
                    matchCounts
                );
            }

            var comparison = Vector256.GreaterThan(
                matchCounts,
                Vector256.Subtract(Vector256.Create(clause.Min), Vector256.Create(1))
            );
            return new VectorMask(VectorizedComparisonToMask(comparison));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<int> AccumulateVoucherRolls(
            ref MotelyVectorSearchContext ctx,
            int ante,
            ref MotelyVectorRunState voucherState,
            VectorEnum256<MotelyVoucher> anteVouchers,
            VoucherClause clause,
            Vector256<int> matchCounts
        )
        {
            int maxRoll = MapFeatureRolls.MaxRollIndex(clause.Rolls);
            VectorEnum256<MotelyVoucher> streamDraw1 = default;
            VectorEnum256<MotelyVoucher> streamDraw2 = default;

            if (maxRoll >= 1)
            {
                var voucherStream = ctx.CreateVoucherStream(ante);
                streamDraw1 = ctx.GetNextVoucher(ref voucherStream, voucherState);
                if (maxRoll >= 2)
                    streamDraw2 = ctx.GetNextVoucher(ref voucherStream, voucherState);
            }

            foreach (var roll in clause.Rolls)
            {
                if (roll == 0)
                    matchCounts = AddVoucherMatches(matchCounts, anteVouchers, clause);
                else if (roll == 1)
                    matchCounts = AddVoucherMatches(matchCounts, streamDraw1, clause);
                else if (roll == 2)
                    matchCounts = AddVoucherMatches(matchCounts, streamDraw2, clause);
            }

            return matchCounts;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<int> AddVoucherMatches(
            Vector256<int> counts,
            VectorEnum256<MotelyVoucher> vouchers,
            VoucherClause clause,
            VectorMask? includeMask = null
        )
        {
            Vector256<int> matchMask = Vector256<int>.Zero;

            if (clause.Vouchers.Length == 1)
            {
                matchMask = VectorEnum256.Equals(vouchers, clause.Vouchers[0]);
            }
            else
            {
                foreach (var v in clause.Vouchers)
                    matchMask = Vector256.Max(matchMask, VectorEnum256.Equals(vouchers, v));
            }

            if (includeMask.HasValue)
            {
                matchMask = Vector256.BitwiseAnd(
                    matchMask,
                    MotelyVectorUtils.VectorMaskToConditionalSelectMask(includeMask.Value)
                );
            }

            return Vector256.Add(
                counts,
                Vector256.ConditionalSelect(matchMask, Vector256.Create(1), Vector256<int>.Zero)
            );
        }
    }
}

/// <summary>
/// Combines multiple <see cref="VoucherClause"/>s into a single ante-loop filter so voucher
/// PRNG state is built only once. Use when two or more voucher clauses appear in the same
/// Must/MustNot set (e.g. Telescope@ante1 + Observatory@ante2).
/// </summary>
public struct MultiVoucherFilterDesc(VoucherClause[] clauses)
    : IMotelySeedFilterDesc<MultiVoucherFilterDesc.MultiVoucherFilter>
{
    private readonly VoucherClause[] _clauses = clauses;

    public readonly MultiVoucherFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        int maxAnte = 0;
        foreach (var c in _clauses)
            for (int i = 0; i < c.Antes.Length; i++)
                if (c.Antes[i] > maxAnte)
                    maxAnte = c.Antes[i];

        for (int ante = 1; ante <= maxAnte; ante++)
            ctx.CacheAnteFirstVoucher(ante);

        return new MultiVoucherFilter(_clauses, maxAnte);
    }

    public struct MultiVoucherFilter(VoucherClause[] clauses, int maxAnte) : IMotelySeedFilter
    {
        private readonly VoucherClause[] _clauses = clauses;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var clauses = _clauses;
            int maxAnte = _maxAnte;
            var voucherState = new MotelyVectorRunState();

            Span<Vector256<int>> matchCounts = stackalloc Vector256<int>[clauses.Length];

            for (int ante = 1; ante <= maxAnte; ante++)
            {
                var vouchers = ctx.GetAnteFirstVoucher(ante, voucherState);
                voucherState.ActivateVoucher(vouchers);

                // Determine which clauses target this ante
                bool anyTarget = false;
                for (int ci = 0; ci < clauses.Length; ci++)
                {
                    var anteList = clauses[ci].Antes;
                    for (int ai = 0; ai < anteList.Length; ai++)
                    {
                        if (anteList[ai] == ante)
                        {
                            anyTarget = true;
                            break;
                        }
                    }
                }

                if (!anyTarget)
                    continue;

                // Accumulate matches for each targeting clause
                for (int ci = 0; ci < clauses.Length; ci++)
                {
                    var clause = clauses[ci];
                    bool isTarget = false;
                    for (int ai = 0; ai < clause.Antes.Length; ai++)
                        if (clause.Antes[ai] == ante)
                        {
                            isTarget = true;
                            break;
                        }
                    if (!isTarget)
                        continue;

                    matchCounts[ci] = VoucherFilterDesc.VoucherFilter.AccumulateVoucherRolls(
                        ref ctx,
                        ante,
                        ref voucherState,
                        vouchers,
                        clause,
                        matchCounts[ci]
                    );
                }
            }

            // AND all clause pass masks together
            VectorMask result = VectorMask.AllBitsSet;
            for (int ci = 0; ci < clauses.Length; ci++)
            {
                var clause = clauses[ci];
                VectorMask clauseMask = Vector256.GreaterThan(
                    matchCounts[ci],
                    Vector256.Subtract(Vector256.Create(clause.Min), Vector256.Create(1))
                );
                result &= clauseMask;
            }
            return result;
        }
    }
}
