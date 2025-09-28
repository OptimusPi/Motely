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
            // Extract antes from array (ante is 0-based in WantedAntes, but 1-based for CacheAnteFirstVoucher)
            for (int anteIndex = 0; anteIndex < 40; anteIndex++)
            {
                if (clause.WantedAntes[anteIndex])
                {
                    ctx.CacheAnteFirstVoucher(anteIndex + 1); // Convert to 1-based ante number
                }
            }
        }
        
        return new MotelyJsonVoucherFilter(_voucherClauses);
    }

    // Pre-calculated data for hot path optimization
    private struct ClausePreCalc
    {
        public bool HasMultipleTypes;
        public int TypeCount;
        public int[] EffectiveAntes; // Pre-calculated from WantedAntes
        
        public static ClausePreCalc[] CreateArray(MotelyJsonVoucherFilterClause[] clauses)
        {
            var result = new ClausePreCalc[clauses.Length];
            for (int i = 0; i < clauses.Length; i++)
            {
                var clause = clauses[i];
                var effectiveAntes = new List<int>();
                for (int ante = 0; ante < clause.WantedAntes.Length; ante++)
                {
                    if (clause.WantedAntes[ante])
                        effectiveAntes.Add(ante); // Just use the ante index directly
                }
                
                result[i] = new ClausePreCalc
                {
                    HasMultipleTypes = clause.VoucherTypes != null && clause.VoucherTypes.Count > 1,
                    TypeCount = clause.VoucherTypes?.Count ?? 1,
                    EffectiveAntes = effectiveAntes.ToArray()
                };
            }
            return result;
        }
    }
    
    public struct MotelyJsonVoucherFilter : IMotelySeedFilter
    {
        private readonly MotelyJsonVoucherFilterClause[] _clauses;
        private readonly int _maxAnte;
        private readonly int _clauseCount;
        private readonly ClausePreCalc[] _preCalc;

        public MotelyJsonVoucherFilter(List<MotelyJsonVoucherFilterClause> clauses)
        {
            _clauses = clauses.ToArray();
            _clauseCount = _clauses.Length;
            _preCalc = ClausePreCalc.CreateArray(_clauses);
            _maxAnte = 0;
            foreach (var clause in _clauses)
            {
                for (int ante = clause.WantedAntes.Length - 1; ante >= 0; ante--)
                {
                    if (clause.WantedAntes[ante])
                    {
                        _maxAnte = Math.Max(_maxAnte, ante + 1); // Convert 0-based index to 1-based ante
                        break;
                    }
                }
            }
            if (_maxAnte == 0) _maxAnte = 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauseCount == 0)
                return VectorMask.AllBitsSet;
            
            var voucherState = new MotelyVectorRunState();
            VectorMask result = VectorMask.AllBitsSet;
            
            // SIMPLE: For each clause, check if ANY of its wanted antes has the voucher
            for (int i = 0; i < _clauseCount; i++)
            {
                var clause = _clauses[i];
                var preCalc = _preCalc[i];
                VectorMask clauseMatch = VectorMask.NoBitsSet;
                
                // Get vouchers for each ante this clause wants
                if (preCalc.EffectiveAntes != null)
                {
                    for (int j = 0; j < preCalc.EffectiveAntes.Length; j++)
                    {
                        int ante = preCalc.EffectiveAntes[j];
                        var vouchers = ctx.GetAnteFirstVoucher(ante, in voucherState);
                        
                        // Check if this ante has the voucher we want
                        var mask = VectorEnum256.Equals(vouchers, clause.VoucherType);
                        clauseMatch |= mask;
                        
                        // Activate vouchers for state management
                        voucherState.ActivateVoucher(vouchers);
                        
                        // Special handling for Hieroglyph - check the bonus voucher too
                        VectorMask hieroglyphMask = VectorEnum256.Equals(vouchers, MotelyVoucher.Hieroglyph);
                        if (hieroglyphMask.IsPartiallyTrue())
                        {
                            // Get the bonus voucher that would appear after Hieroglyph reset
                            var voucherStream = ctx.CreateVoucherStream(ante);
                            var bonusVouchers = ctx.GetNextVoucher(ref voucherStream, in voucherState);
                            
                            // Check if the bonus voucher matches this clause
                            var bonusMask = VectorEnum256.Equals(bonusVouchers, clause.VoucherType);
                            clauseMatch |= bonusMask;
                            
                            // Activate bonus vouchers for seeds that had Hieroglyph
                            voucherState.ActivateVoucher(bonusVouchers);
                        }
                    }
                }
                
                // AND with result - ALL clauses must match
                result &= clauseMatch;
                if (result.IsAllFalse()) return VectorMask.NoBitsSet;
            }
            
            if (result.IsAllFalse())
                return VectorMask.NoBitsSet;
            
            var clauses = _clauses; // Copy to local for lambda capture
            var maxAnte = _maxAnte; // Copy to local for lambda capture
            var preCalcArray = _preCalc; // Copy to local for lambda capture
            return ctx.SearchIndividualSeeds(result, (ref MotelySingleSearchContext singleCtx) =>
            {
                var runState = new MotelyRunState();
                var vouchers = new MotelyVoucher[maxAnte + 1]; // Direct indexing: vouchers[1] = ante 1
                
                // Get all vouchers upfront (like boss filter does)
                var bonusVouchers = new MotelyVoucher[maxAnte + 1]; // Store Hieroglyph bonuses
                for (int ante = 1; ante <= maxAnte; ante++)
                {
                    vouchers[ante] = singleCtx.GetAnteFirstVoucher(ante, in runState);
                    runState.ActivateVoucher(vouchers[ante]);
                    
                    // If it's Hieroglyph, also get and store the bonus voucher
                    if (vouchers[ante] == MotelyVoucher.Hieroglyph)
                    {
                        var voucherStream = singleCtx.CreateVoucherStream(ante);
                        bonusVouchers[ante] = singleCtx.GetNextVoucher(ref voucherStream, in runState);
                        runState.ActivateVoucher(bonusVouchers[ante]);
                    }
                }
                
                // Check all clauses (simple like boss filter)
                foreach (var clause in clauses)
                {
                    bool matched = false;
                    
                    // Use preCalc.EffectiveAntes if available (like boss filter)
                    var preCalc = preCalcArray[Array.IndexOf(clauses, clause)];
                    if (preCalc.EffectiveAntes != null)
                    {
                        foreach (var ante in preCalc.EffectiveAntes)
                        {
                            if (ante <= maxAnte)
                            {
                                var voucher = vouchers[ante];
                                // Check primary voucher
                                if (clause.VoucherType == voucher || 
                                    (clause.VoucherTypes != null && clause.VoucherTypes.Contains(voucher)))
                                {
                                    matched = true;
                                    break;
                                }
                                // Also check Hieroglyph bonus voucher if applicable
                                if (bonusVouchers[ante] != default && 
                                    (clause.VoucherType == bonusVouchers[ante] || 
                                     (clause.VoucherTypes != null && clause.VoucherTypes.Contains(bonusVouchers[ante]))))
                                {
                                    matched = true;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (!matched) return false;
                }
                
                return true;
            });
        }
    }
}