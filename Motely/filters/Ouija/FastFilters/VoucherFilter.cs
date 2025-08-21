using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Ouija.FastFilters;

/// <summary>
/// FAST filter - Vouchers are vectorized and have simple state tracking.
/// Processes 8 seeds in parallel using SIMD.
/// </summary>
public static class VoucherFilter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorMask CheckAll(
        VectorMask inputMask,
        IEnumerable<OuijaConfig.FilterItem> voucherClauses,
        ref MotelyVectorSearchContext ctx,
        ref MotelyVectorRunStateVoucher voucherState)
    {
        var mask = inputMask;
        
        // Sort by ante to process in order (important for state)
        var sortedClauses = voucherClauses.OrderBy(c => c.EffectiveAntes?.FirstOrDefault() ?? 0);
        
        foreach (var clause in sortedClauses)
        {
            mask &= CheckSingleClause(ref ctx, clause, ref voucherState);
            
            // Early exit if all seeds filtered out
            if (mask.IsAllFalse())
                return mask;
        }
        
        return mask;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VectorMask CheckSingleClause(
        ref MotelyVectorSearchContext ctx,
        OuijaConfig.FilterItem clause,
        ref MotelyVectorRunStateVoucher voucherState)
    {
        if (!clause.VoucherEnum.HasValue)
            return VectorMask.AllBitsSet;

        var mask = VectorMask.AllBitsSet;
        
        foreach (var ante in clause.EffectiveAntes)
        {
            mask &= CheckVoucherAtAnte(ref ctx, clause, ante, ref voucherState);
            
            if (mask.IsAllFalse())
                break;
        }
        
        return mask;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VectorMask CheckVoucherAtAnte(
        ref MotelyVectorSearchContext ctx,
        OuijaConfig.FilterItem clause,
        int ante,
        ref MotelyVectorRunStateVoucher voucherState)
    {
        var voucherStream = ctx.CreateVoucherStream(ante);
        var voucher = ctx.GetNextVoucher(ref voucherStream, voucherState);
        return VectorEnum256.Equals(voucher, clause.VoucherEnum.Value);
    }
    
    /// <summary>
    /// Single-seed version for when using SearchIndividualSeeds
    /// </summary>
    public static bool CheckSingle(
        ref MotelySingleSearchContext ctx,
        OuijaConfig.FilterItem clause,
        int ante,
        ref MotelyRunState voucherState)
    {
        if (!clause.VoucherEnum.HasValue)
            return false;

        // Check if already active
        if (voucherState.IsVoucherActive(clause.VoucherEnum.Value))
        {
            DebugLogger.Log($"[Voucher] {clause.VoucherEnum.Value} already active");
            return true;
        }

        var voucher = ctx.GetAnteFirstVoucher(ante, voucherState);
        
        if (voucher == clause.VoucherEnum.Value)
        {
            voucherState.ActivateVoucher(voucher);
            DebugLogger.Log($"[Voucher] Found and activated {voucher}");
            return true;
        }

        return false;
    }
}