using System.Diagnostics;
using System.Runtime.CompilerServices;
using Motely;
using System.Runtime.Intrinsics;
using static Motely.MotelyVectorUtils;

namespace Motely.Filters;

public sealed class VoucherClause : IJamlClause
{
    public string Label { get; init; } = "";
    public required MotelyVoucher[] Vouchers { get; init; }
    public int[] Antes { get; init; } = [];
    public int Min { get; init; } = 1;
}

public struct VoucherFilterDesc(VoucherClause clause)
    : IMotelySeedFilterDesc<VoucherFilterDesc.VoucherFilter>
{
    private readonly VoucherClause _clause = clause;

    public VoucherFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        int maxAnte = 0;
        for (int i = 0; i < _clause.Antes.Length; i++)
        {
            ctx.CacheAnteFirstVoucher(_clause.Antes[i]);
            if (_clause.Antes[i] > maxAnte) maxAnte = _clause.Antes[i];
        }

        return new VoucherFilter(_clause, maxAnte);
    }

    public struct VoucherFilter(VoucherClause clause, int maxAnte) : IMotelySeedFilter
    {
        private readonly VoucherClause _clause = clause;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Vouchers.Length > 0);

            var clause = _clause;
            int maxAnte = _maxAnte;

            VectorMask result = VectorMask.NoBitsSet;
            var voucherState = new MotelyVectorRunState();

            for (int ante = 1; ante <= maxAnte; ante++)
            {
                bool isTarget = false;
                for (int i = 0; i < clause.Antes.Length; i++)
                {
                    if (clause.Antes[i] == ante) { isTarget = true; break; }
                }
                if (!isTarget) continue;

                var vouchers = ctx.GetAnteFirstVoucher(ante, voucherState);
                result |= GetVoucherMatch(vouchers, clause);

                voucherState.ActivateVoucher(vouchers);

                if (result.IsAllTrue()) return result;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask GetVoucherMatch(
            VectorEnum256<MotelyVoucher> vouchers,
            VoucherClause clause)
        {
            if (clause.Vouchers.Length == 1)
            {
                Vector256<int> r = VectorEnum256.Equals(vouchers, clause.Vouchers[0]);
                return new VectorMask(VectorizedComparisonToMask(r));
            }

            Vector256<int> anyMatch = Vector256<int>.Zero;
            foreach (var v in clause.Vouchers)
                anyMatch = Vector256.Max(anyMatch, VectorEnum256.Equals(vouchers, v));
            return new VectorMask(VectorizedComparisonToMask(anyMatch));
        }
    }
}
