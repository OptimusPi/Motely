using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using static Motely.MotelyVectorUtils;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on voucher criteria from JSON configuration.
/// </summary>
public struct MotelyJsonVoucherFilterDesc(MotelyJsonVoucherFilterCriteria criteria)
    : IMotelySeedFilterDesc<MotelyJsonVoucherFilterDesc.MotelyJsonVoucherFilter>
{
    private readonly MotelyJsonVoucherFilterCriteria _criteria = criteria;

    public MotelyJsonVoucherFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var clause in _criteria.Clauses)
        {
            if (clause.EffectiveAntes.Length > 0)
            {
                foreach (var anteIndex in clause.EffectiveAntes)
                {
                    ctx.CacheAnteFirstVoucher(anteIndex);
                }
            }
            else
            {
                for (int anteIndex = 0; anteIndex < clause.WantedAntes.Length; anteIndex++)
                {
                    if (!clause.WantedAntes[anteIndex])
                        continue;
                    ctx.CacheAnteFirstVoucher(anteIndex);
                }
            }
        }

        return new MotelyJsonVoucherFilter(_criteria.Clauses, _criteria.MinAnte, _criteria.MaxAnte);
    }

    public struct MotelyJsonVoucherFilter : IMotelySeedFilter
    {
        private readonly MotelyJsonVoucherFilterClause[] _clauses;
        private readonly int _minAnte;
        private readonly int _maxAnte;
        private readonly bool _lookingForPetroglyph;

        /// <summary>True when every clause has Min null or &lt;= 1; enables SIMD-only fast path (no per-clause count).</summary>
        private readonly bool _allClausesMinOne;

        public MotelyJsonVoucherFilter(
            List<MotelyJsonVoucherFilterClause> clauses,
            int minAnte,
            int maxAnte
        )
        {
            _clauses = [.. clauses];
            _minAnte = minAnte;
            _maxAnte = maxAnte;
            _lookingForPetroglyph = _clauses.Any(clause =>
                clause.VoucherType == MotelyVoucher.Petroglyph
            );
            _allClausesMinOne = _clauses.All(c => !c.Min.HasValue || c.Min.Value <= 1);
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(
                _clauses.Length > 0,
                "Voucher filter created with empty clauses - this is a programming error!"
            );

            int maxWantedAntesLength = 0;
            for (int i = 0; i < _clauses.Length; i++)
            {
                int len = _clauses[i].WantedAntes.Length;
                if (len > maxWantedAntesLength)
                    maxWantedAntesLength = len;
            }
            int maxAnteToCheck = Math.Min(_maxAnte, maxWantedAntesLength - 1);
            var voucherState = new MotelyVectorRunState();

            if (_allClausesMinOne)
            {
                // Fast path: all clauses need "at least one" match; OR per clause, then AND across clauses.
                Span<VectorMask> clauseMasks = stackalloc VectorMask[_clauses.Length];
                for (int i = 0; i < clauseMasks.Length; i++)
                    clauseMasks[i] = VectorMask.NoBitsSet;

                for (int ante = 1; ante <= maxAnteToCheck; ante++)
                {
                    var vouchers = ctx.GetAnteFirstVoucher(ante, voucherState, isCached: true);
                    for (int i = 0; i < _clauses.Length; i++)
                    {
                        if (!_clauses[i].WantedAntes[ante])
                            continue;
                        VectorMask match = GetVoucherMatchVectorMask(vouchers, i);
                        clauseMasks[i] |= match;

                        if (_clauses[i].MaxAnte == ante && clauseMasks[i].IsAllFalse())
                            return VectorMask.NoBitsSet;
                    }
                    voucherState.ActivateVoucher(vouchers);
                }

                VectorMask finalMask = VectorMask.AllBitsSet;
                for (int i = 0; i < _clauses.Length; i++)
                    finalMask &= clauseMasks[i];
                return finalMask;
            }

            // Min > 1 path: count matches per clause per lane, then require count >= Min.
            Span<Vector256<int>> clauseCounts = stackalloc Vector256<int>[_clauses.Length];
            for (int i = 0; i < clauseCounts.Length; i++)
                clauseCounts[i] = Vector256<int>.Zero;

            for (int ante = 1; ante <= maxAnteToCheck; ante++)
            {
                var vouchers = ctx.GetAnteFirstVoucher(ante, voucherState, isCached: true);
                for (int i = 0; i < _clauses.Length; i++)
                {
                    if (!_clauses[i].WantedAntes[ante])
                        continue;
                    VectorMask match = GetVoucherMatchVectorMask(vouchers, i);
                    // Convert mask to conditional select format for counting
                    Vector256<int> selectMask = VectorMaskToConditionalSelectMask(match);
                    clauseCounts[i] += Vector256.ConditionalSelect(
                        selectMask,
                        Vector256<int>.One,
                        Vector256<int>.Zero
                    );
                }
                voucherState.ActivateVoucher(vouchers);
            }

            Span<VectorMask> clauseMasksOut = stackalloc VectorMask[_clauses.Length];
            for (int i = 0; i < _clauses.Length; i++)
            {
                int minRequired = _clauses[i].Min ?? 0;
                Vector256<int> comparisonResult = Vector256.GreaterThanOrEqual(
                    clauseCounts[i],
                    Vector256.Create(minRequired)
                );
                clauseMasksOut[i] = new VectorMask(VectorizedComparisonToMask(comparisonResult));
            }

            VectorMask final = VectorMask.AllBitsSet;
            for (int i = 0; i < _clauses.Length; i++)
                final &= clauseMasksOut[i];
            return final;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask GetVoucherMatchVectorMask(
            VectorEnum256<MotelyVoucher> vouchers,
            int clauseIndex
        )
        {
            ref readonly var clause = ref _clauses[clauseIndex];
            if (clause.VoucherTypes?.Count > 1)
            {
                Vector256<int> anyMatch = Vector256<int>.Zero;
                foreach (var voucherType in clause.VoucherTypes!)
                    anyMatch = Vector256.Max(anyMatch, VectorEnum256.Equals(vouchers, voucherType));
                return new VectorMask(VectorizedComparisonToMask(anyMatch));
            }
            Vector256<int> result = VectorEnum256.Equals(vouchers, clause.VoucherType);
            return new VectorMask(VectorizedComparisonToMask(result));
        }
    }
}
