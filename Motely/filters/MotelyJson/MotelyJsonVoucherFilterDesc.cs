using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on voucher criteria from JSON configuration.
/// </summary>
public struct MotelyJsonVoucherFilterDesc(List<MotelyJsonVoucherFilterClause> voucherClauses)
    : IMotelySeedFilterDesc<MotelyJsonVoucherFilterDesc.MotelyJsonVoucherFilter>
{
    private readonly List<MotelyJsonVoucherFilterClause> _voucherClauses = voucherClauses;

    public MotelyJsonVoucherFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Cache only the antes we actually need
        foreach (var clause in _voucherClauses)
        {
            // Extract antes from bitmask
            for (int bit = 0; bit < 64; bit++)
            {
                if ((clause.AnteBitmask & (1UL << bit)) != 0)
                {
                    ctx.CacheAnteFirstVoucher(bit + 1);
                }
            }
        }
        
        return new MotelyJsonVoucherFilter(_voucherClauses);
    }

    public struct MotelyJsonVoucherFilter : IMotelySeedFilter
    {
        private readonly MotelyJsonVoucherFilterClause[] _clauses;
        private readonly int _maxAnte;

        public MotelyJsonVoucherFilter(List<MotelyJsonVoucherFilterClause> clauses)
        {
            _clauses = clauses.ToArray();
            
            // Pre-calculate max ante we need to check from bitmasks
            _maxAnte = 0;
            foreach (var clause in _clauses)
            {
                if (clause.AnteBitmask != 0)
                {
                    // Find highest set bit
                    for (int bit = 63; bit >= 0; bit--)
                    {
                        if ((clause.AnteBitmask & (1UL << bit)) != 0)
                        {
                            _maxAnte = Math.Max(_maxAnte, bit + 1);
                            break;
                        }
                    }
                }
            }
            if (_maxAnte == 0) _maxAnte = 8; // Default if no antes specified
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Length == 0)
                return VectorMask.AllBitsSet;
            
            // Stack-allocated clause masks - no heap allocation!
            Span<VectorMask> clauseMasks = stackalloc VectorMask[_clauses.Length];
            for (int i = 0; i < clauseMasks.Length; i++)
                clauseMasks[i] = VectorMask.NoBitsSet;
            
            var voucherState = new MotelyVectorRunState();
            
            // Only process antes we need
            for (int ante = 1; ante <= _maxAnte; ante++)
            {
                var vouchers = ctx.GetAnteFirstVoucher(ante, voucherState);
                ulong anteBit = 1UL << (ante - 1);
                
                for (int i = 0; i < _clauses.Length; i++)
                {
                    // Single bit test instead of Contains()!
                    if ((_clauses[i].AnteBitmask & anteBit) != 0)
                    {
                        VectorMask matches = VectorMask.NoBitsSet;
                        
                        if (_clauses[i].VoucherTypes != null && _clauses[i].VoucherTypes.Count > 1)
                        {
                            // Multi-value: OR logic - match any voucher in the list
                            foreach (var voucherType in _clauses[i].VoucherTypes)
                            {
                                matches |= VectorEnum256.Equals(vouchers, voucherType);
                            }
                        }
                        else
                        {
                            // Single value
                            matches = VectorEnum256.Equals(vouchers, _clauses[i].VoucherType);
                        }
                        
                        clauseMasks[i] |= matches;
                    }
                }
                
                voucherState.ActivateVoucher(vouchers);
            }
            
            // All voucher clauses must be satisfied
            // CRITICAL FIX: If any clause found nothing (NoBitsSet), the entire filter fails!
            var resultMask = VectorMask.AllBitsSet;
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                // FIX: If this clause found nothing across all antes, fail immediately
                if (clauseMasks[i].IsAllFalse())
                {
                    return VectorMask.NoBitsSet;
                }
                
                resultMask &= clauseMasks[i];
                if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }
            
            return resultMask;
        }
    }
}