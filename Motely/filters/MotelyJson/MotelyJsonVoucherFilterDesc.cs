using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on voucher criteria from JSON configuration.
/// </summary>
public struct MotelyJsonVoucherFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> voucherClauses)
    : IMotelySeedFilterDesc<MotelyJsonVoucherFilterDesc.MotelyJsonVoucherFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _voucherClauses = voucherClauses;

    public readonly string Name => "JSON Voucher Filter";
    public readonly string Description => "Vectorized voucher filtering with activation chains";

    public MotelyJsonVoucherFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var clause in _voucherClauses)
        {
            if (clause.EffectiveAntes != null)
            {
                foreach (var ante in clause.EffectiveAntes)
                {
                    ctx.CacheAnteFirstVoucher(ante);
                }
            }
        }
        
        return new MotelyJsonVoucherFilter(_voucherClauses);
    }

    public struct MotelyJsonVoucherFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses = clauses;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            // VECTORIZED voucher filtering like native PerkeoObservatory
            var voucherState = new MotelyVectorRunState();
            var clauseMasks = new Dictionary<MotelyJsonConfig.MotleyJsonFilterClause, VectorMask>();
            
            foreach (var clause in _clauses)
            {
                clauseMasks[clause] = VectorMask.NoBitsSet;
            }
            
            // Process antes in order for proper voucher activation
            for (int ante = 1; ante <= 8; ante++)
            {
                var vouchers = ctx.GetAnteFirstVoucher(ante, voucherState);
                
                foreach (var clause in _clauses)
                {
                    if (clause.EffectiveAntes != null && clause.EffectiveAntes.Contains(ante))
                    {
                        if (clause.VoucherEnum.HasValue)
                        {
                            var matches = VectorEnum256.Equals(vouchers, clause.VoucherEnum.Value);
                            clauseMasks[clause] |= matches;
                        }
                    }
                }
                
                voucherState.ActivateVoucher(vouchers);
            }
            
            // All voucher clauses must be satisfied
            var resultMask = VectorMask.AllBitsSet;
            foreach (var clauseMask in clauseMasks.Values)
            {
                resultMask &= clauseMask;
                if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }
            
            return resultMask;
        }
    }
}