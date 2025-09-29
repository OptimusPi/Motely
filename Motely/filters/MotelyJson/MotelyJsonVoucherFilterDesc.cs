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
            // Extract antes from array (ante is 1-based, but we're iterating 0-based array)
            for (int anteIndex = 0; anteIndex < 40; anteIndex++)
            {
                if (clause.WantedAntes[anteIndex])
                {
                    ctx.CacheAnteFirstVoucher(anteIndex); // Antes are 0-based
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
                if (HasAntes(clause.WantedAntes))
                {
                    // Find highest set bit (WantedAntes is 40 elements, so check backwards from index 39)
                    for (int ante = clause.WantedAntes.Length - 1; ante >= 0; ante--)
                    {
                        if (ante < clause.WantedAntes.Length && clause.WantedAntes[ante])
                        {
                            _maxAnte = Math.Max(_maxAnte, ante);
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
            for (int ante = 0; ante <= _maxAnte && ante < _clauses[0].WantedAntes.Length; ante++)
            {
                var vouchers = ctx.GetAnteFirstVoucher(ante, voucherState);
                
                for (int i = 0; i < _clauses.Length; i++)
                {
                    // Check if this ante is wanted
                    if (ante < _clauses[i].WantedAntes.Length && _clauses[i].WantedAntes[ante])
                    {
                        VectorMask matches = VectorMask.NoBitsSet;
                        
                        if (_clauses[i].VoucherTypes?.Count > 1)
                        {
                            // Multi-value: OR logic - match any voucher in the list
                            foreach (var voucherType in _clauses[i].VoucherTypes!)
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
            
            if (resultMask.IsAllFalse())
                return VectorMask.NoBitsSet;
            
            // Verify each passing seed individually to avoid SIMD bugs
            var clauses = _clauses; // Copy to local for lambda capture
            var maxAnte = _maxAnte; // Copy to local for lambda capture
            return ctx.SearchIndividualSeeds(resultMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                var singleVoucherState = new MotelyRunState();
                
                // Re-check all clauses for this individual seed
                foreach (var clause in clauses)
                {
                    bool clauseSatisfied = false;
                    
                    // Check all antes for this clause
                    for (int ante = 0; ante <= maxAnte && ante < clause.WantedAntes.Length; ante++)
                    {
                        var voucher = singleCtx.GetAnteFirstVoucher(ante, singleVoucherState);
                        singleVoucherState.ActivateVoucher(voucher);
                        
                        if (ante < clause.WantedAntes.Length && clause.WantedAntes[ante])
                        {
                            bool voucherMatches = false;
                            if (clause.VoucherTypes != null && clause.VoucherTypes.Count > 1)
                            {
                                // Multi-value check
                                foreach (var voucherType in clause.VoucherTypes)
                                {
                                    if (voucher == voucherType)
                                    {
                                        voucherMatches = true;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                // Single value check
                                voucherMatches = voucher == clause.VoucherType;
                            }
                            
                            if (voucherMatches)
                            {
                                clauseSatisfied = true;
                                break;
                            }
                        }
                    }
                    
                    if (!clauseSatisfied)
                        return false; // This seed doesn't satisfy this clause
                }
                
                return true; // All clauses satisfied
            });
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasAntes(bool[] antes)
        {
            for (int i = 0; i < antes.Length; i++)
                if (antes[i]) return true;
            return false;
        }
    }
}