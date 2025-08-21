using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Ouija.FastFilters;

/// <summary>
/// FASTEST filter - Tags have no state and are fully vectorized.
/// Processes 8 seeds in parallel using SIMD.
/// </summary>
public static class TagFilter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorMask CheckAll(
        VectorMask inputMask, 
        IEnumerable<OuijaConfig.FilterItem> tagClauses,
        ref MotelyVectorSearchContext ctx)
    {
        var mask = inputMask;
        
        // Process each tag clause
        foreach (var clause in tagClauses)
        {
            mask &= CheckSingleClause(ref ctx, clause);
            
            // Early exit if all seeds filtered out
            if (mask.IsAllFalse()) 
                return mask;
        }
        
        return mask;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VectorMask CheckSingleClause(
        ref MotelyVectorSearchContext ctx, 
        OuijaConfig.FilterItem clause)
    {
        if (!clause.TagEnum.HasValue) 
            return VectorMask.AllBitsSet;

        var mask = VectorMask.AllBitsSet;
        
        foreach (var ante in clause.EffectiveAntes)
        {
            // Create tag stream for this ante
            var tagStream = ctx.CreateTagStream(ante);
            
            // Get both tags (2 PRNG calls)
            var smallTag = ctx.GetNextTag(ref tagStream);
            var bigTag = ctx.GetNextTag(ref tagStream);

            // Check based on what we're looking for
            var tagMatches = clause.TagTypeEnum switch
            {
                MotelyTagType.SmallBlind => VectorEnum256.Equals(smallTag, clause.TagEnum.Value),
                MotelyTagType.BigBlind => VectorEnum256.Equals(bigTag, clause.TagEnum.Value),
                _ => VectorEnum256.Equals(smallTag, clause.TagEnum.Value) | 
                     VectorEnum256.Equals(bigTag, clause.TagEnum.Value)
            };

            mask &= tagMatches;
            
            // Early exit if no seeds match
            if (mask.IsAllFalse()) 
                break;
        }

        return mask;
    }
    
    /// <summary>
    /// Single-seed version for when using SearchIndividualSeeds
    /// </summary>
    public static bool CheckSingle(
        ref MotelySingleSearchContext ctx, 
        OuijaConfig.FilterItem clause, 
        int ante)
    {
        if (!clause.TagEnum.HasValue) 
            return false;

        var tagStream = ctx.CreateTagStream(ante);
        var smallTag = ctx.GetNextTag(ref tagStream);
        var bigTag = ctx.GetNextTag(ref tagStream);

        return clause.TagTypeEnum switch
        {
            MotelyTagType.SmallBlind => smallTag == clause.TagEnum.Value,
            MotelyTagType.BigBlind => bigTag == clause.TagEnum.Value,
            _ => smallTag == clause.TagEnum.Value || bigTag == clause.TagEnum.Value
        };
    }
}