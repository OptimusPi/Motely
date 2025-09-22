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
            _maxAnte = 0;
            foreach (var clause in _clauses)
            {
                for (int ante = clause.WantedAntes.Length - 1; ante >= 0; ante--)
                {
                    if (clause.WantedAntes[ante])
                    {
                        _maxAnte = Math.Max(_maxAnte, ante);
                        break;
                    }
                }
            }
            if (_maxAnte == 0) _maxAnte = 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Length == 0)
                return VectorMask.AllBitsSet;
            
            var resultMask = VectorMask.NoBitsSet;
            var voucherState = new MotelyVectorRunState();
            
            for (int ante = 1; ante <= _maxAnte; ante++)
            {
                var vouchers = ctx.GetAnteFirstVoucher(ante, voucherState);
                
                for (int i = 0; i < _clauses.Length; i++)
                {
                    if (ante < _clauses[i].WantedAntes.Length && _clauses[i].WantedAntes[ante])
                    {
                        VectorMask matches = VectorMask.NoBitsSet;
                        
                        if (_clauses[i].VoucherTypes != null && _clauses[i].VoucherTypes.Count > 1)
                        {
                            foreach (var voucherType in _clauses[i].VoucherTypes)
                            {
                                matches |= VectorEnum256.Equals(vouchers, voucherType);
                            }
                        }
                        else
                        {
                            matches = VectorEnum256.Equals(vouchers, _clauses[i].VoucherType);
                        }
                        
                        resultMask |= matches;
                    }
                }
                
                voucherState.ActivateVoucher(vouchers);
                
                if (resultMask.IsAllTrue())
                    return resultMask;
            }
            
            if (resultMask.IsAllFalse())
                return VectorMask.NoBitsSet;
            
            var clauses = _clauses; // Copy to local for lambda capture
            var maxAnte = _maxAnte; // Copy to local for lambda capture
            return ctx.SearchIndividualSeeds(resultMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                var runState = new MotelyRunState();
                
                foreach (var clause in clauses)
                {
                    // Convert WantedAntes bool array to EffectiveAntes int array for the shared function
                    var effectiveAntes = new List<int>();
                    for (int i = 0; i < clause.WantedAntes.Length; i++)
                    {
                        if (clause.WantedAntes[i])
                            effectiveAntes.Add(i);
                    }
                    
                    bool clauseSatisfied = false;
                    
                    if (clause.VoucherTypes != null && clause.VoucherTypes.Count > 1)
                    {
                        // Check if ANY of the voucher types appear
                        foreach (var voucherType in clause.VoucherTypes)
                        {
                            var tempClause = new MotelyJsonConfig.MotleyJsonFilterClause
                            {
                                VoucherEnum = voucherType,
                                EffectiveAntes = effectiveAntes.ToArray()
                            };
                            if (MotelyJsonScoring.CheckVoucherForClause(ref singleCtx, tempClause, ref runState))
                            {
                                clauseSatisfied = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        var tempClause = new MotelyJsonConfig.MotleyJsonFilterClause
                        {
                            VoucherEnum = clause.VoucherType,
                            EffectiveAntes = effectiveAntes.ToArray()
                        };
                        clauseSatisfied = MotelyJsonScoring.CheckVoucherForClause(ref singleCtx, tempClause, ref runState);
                    }
                    
                    if (!clauseSatisfied)
                        return false;
                }
                
                return true;
            });
        }
    }
}