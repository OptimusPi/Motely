using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using static Motely.MotelyVectorUtils;

namespace Motely.Filters;

public sealed class VoucherClause : IJamlClause
{
    public string Label { get; init; } = "";
    public int Score { get; init; }
    public required MotelyVoucher[] Vouchers { get; init; }
    public int[] Antes { get; init; } = [];
    public int Min { get; init; } = 1;
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
            ctx.CacheAnteFirstVoucher(_clause.Antes[i]);
            if (_clause.Antes[i] > maxAnte)
                maxAnte = _clause.Antes[i];
        }

        return new VoucherFilter(_clause, maxAnte);
    }

    public struct VoucherFilter(VoucherClause clause, int maxAnte) : IMotelySeedFilter
    {
        private readonly VoucherClause _clause = clause;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
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
                if (!isTarget)
                    continue;

                var vouchers = ctx.GetAnteFirstVoucher(ante, voucherState);
                matchCounts = AddVoucherMatches(matchCounts, vouchers, clause);

                voucherState.ActivateVoucher(vouchers);

                var hieroglyphMask = new VectorMask(
                    VectorizedComparisonToMask(VectorEnum256.Equals(vouchers, MotelyVoucher.Hieroglyph))
                );

                if (!hieroglyphMask.IsAllFalse())
                {
                    var voucherStream = ctx.CreateVoucherStream(ante);
                    var bonusVouchers = ctx.GetNextVoucher(ref voucherStream, voucherState);
                    matchCounts = AddVoucherMatches(matchCounts, bonusVouchers, clause, hieroglyphMask);
                    voucherState.ActivateVoucher(bonusVouchers, hieroglyphMask);
                }
            }

            var comparison = Vector256.GreaterThan(
                matchCounts,
                Vector256.Subtract(Vector256.Create(clause.Min), Vector256.Create(1))
            );
            return new VectorMask(VectorizedComparisonToMask(comparison));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> AddVoucherMatches(
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
