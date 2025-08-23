using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Numerics;

namespace Motely.Filters;

/// <summary>
/// Fast filtering functions for PreFilter phase - existence checks only, no counting
/// Returns true/false for early exit optimization
/// </summary>
public static class MotelyJsonFiltering
{
    #region Vector Filtering Functions
    // These handle vectorized filtering for PreFilter phase
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorMask VectorFilter_Tags(ref MotelyVectorSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause)
    {
        Debug.Assert(clause.TagEnum.HasValue, "VectorFilter_Tags requires TagEnum");
        
        // OR logic across antes - tag can be found in ANY of the specified antes
        var clauseMask = VectorMask.NoBitsSet;
        
        foreach (var ante in clause.EffectiveAntes ?? [])
        {
            var tagStream = ctx.CreateTagStream(ante);
            var smallTag = ctx.GetNextTag(ref tagStream);
            var bigTag = ctx.GetNextTag(ref tagStream);

            var tagMatches = clause.TagTypeEnum switch
            {
                MotelyTagType.SmallBlind => VectorEnum256.Equals(smallTag, clause.TagEnum.Value),
                MotelyTagType.BigBlind => VectorEnum256.Equals(bigTag, clause.TagEnum.Value),
                _ => VectorEnum256.Equals(smallTag, clause.TagEnum.Value) | VectorEnum256.Equals(bigTag, clause.TagEnum.Value)
            };

            clauseMask |= tagMatches; // OR logic
        }

        return clauseMask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorMask VectorFilter_Vouchers(List<MotelyJsonConfig.MotleyJsonFilterClause> clausesList, ref MotelyVectorSearchContext searchContext, ref MotelyVectorRunStateVoucher voucherState)
    {
        Debug.Assert(clausesList.Count > 0, "VectorFilter_Vouchers requires non-empty clause list");
        
        var mask = VectorMask.AllBitsSet;

        // Pre-compute max ante
        int maxAnte = 0;
        foreach (var clause in clausesList)
        {
            if (clause.EffectiveAntes != null && clause.EffectiveAntes.Length > 0)
            {
                int clauseMax = clause.EffectiveAntes[0];
                for (int i = 1; i < clause.EffectiveAntes.Length; i++)
                {
                    if (clause.EffectiveAntes[i] > clauseMax)
                        clauseMax = clause.EffectiveAntes[i];
                }
                maxAnte = Math.Max(maxAnte, clauseMax);
            }
        }

        // Loop through each voucher clause and AND them together
        foreach (var clause in clausesList)
        {
            var clauseMask = VectorMask.NoBitsSet; // OR across antes for this clause

            // Check each ante for this voucher
            foreach (var ante in clause.EffectiveAntes ?? [])
            {
                if (ante <= maxAnte)
                {
                    var vouchers = searchContext.GetAnteFirstVoucher(ante, voucherState);
                    
                    VectorMask matches = VectorEnum256.Equals(vouchers, clause.VoucherEnum.Value);
                    clauseMask |= matches; // OR logic across antes

                    // Activate vouchers that we found
                    if (matches.IsPartiallyTrue())
                    {
                        voucherState.ActivateVoucher(clause.VoucherEnum.Value);
                        
                        // Special case: Hieroglyph changes the NEXT ante's voucher
                        if (clause.VoucherEnum.Value == MotelyVoucher.Hieroglyph)
                        {
                            DebugLogger.Log($"[FilterVouchers] Hieroglyph activated in ante {ante}, next ante will have upgraded voucher");
                        }
                    }
                }
            }

            // AND this clause result with overall mask
            mask &= clauseMask;
            if (mask.IsAllFalse())
            {
                return mask; // Early exit
            }
        }

        return mask;
    }

    // TODO: Add other vector filtering functions:
    // - VectorFilter_Tarots
    // - VectorFilter_Planets 
    // - VectorFilter_Spectrals
    // - VectorFilter_Bosses
    // - VectorFilter_Jokers (shop slots only)
    
    #endregion

    #region Individual Filtering Functions
    // These handle individual seed filtering for complex logic
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExistsSoulJoker(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState)
    {
        // Fast existence check for soul jokers - early exit on first match
        // TODO: Move soul joker filtering logic here
        return false; // Placeholder
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExistsPlayingCard(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante)
    {
        // Fast existence check for playing cards - early exit on first match
        // TODO: Move playing card filtering logic here
        return false; // Placeholder
    }

    // TODO: Add other existence check functions as needed
    
    #endregion
}