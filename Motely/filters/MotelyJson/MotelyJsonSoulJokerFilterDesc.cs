using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Fully vectorized soul joker filter using two-stage approach:
/// 1. Pre-filter: Fast vectorized joker matching
/// 2. Verify: Vectorized Soul card verification in packs
/// </summary>
public readonly struct MotelyJsonSoulJokerFilterDesc(List<MotelyJsonSoulJokerFilterClause> soulJokerClauses)
    : IMotelySeedFilterDesc<MotelyJsonSoulJokerFilterDesc.MotelyJsonSoulJokerFilter>
{
    private readonly List<MotelyJsonSoulJokerFilterClause> _soulJokerClauses = soulJokerClauses;

    public MotelyJsonSoulJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Calculate ante range from bitmasks
        var (minAnte, maxAnte) = MotelyJsonFilterClause.CalculateAnteRange(_soulJokerClauses);

        // Cache all streams we'll need for BOTH vectorized and individual checks
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            // For vectorized pre-filter
            ctx.CacheSoulJokerStream(ante);
            
            // For individual seed validation
            ctx.CacheBoosterPackStream(ante);
            ctx.CacheArcanaPackTarotStream(ante);
            // Note: No cache method exists for spectral streams in MotelyFilterCreationContext
        }
        
        return new MotelyJsonSoulJokerFilter(_soulJokerClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonSoulJokerFilter(List<MotelyJsonSoulJokerFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonSoulJokerFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool CheckAnteForSoulJoker(int ante, MotelyJsonSoulJokerFilterClause clause, ref MotelySingleSearchContext searchContext)
        {
            // FIRST check if The Soul card exists in any packs
            MotelySingleTarotStream tarotStream = default;
            MotelySingleSpectralStream spectralStream = default;
            MotelySingleBoosterPackStream boosterPackStream = default;
            bool boosterPackStreamInit = false;
            bool tarotStreamInit = false, spectralStreamInit = false;
            bool foundSoulInPack = false;
            
            // Default: Check first 4 packs for ante 1, first 6 for ante 2+
            int maxPackSlot = clause.PackSlotBitmask == 0 ? (ante == 1 ? 4 : 6) : 
                (64 - System.Numerics.BitOperations.LeadingZeroCount(clause.PackSlotBitmask));
            
            for (int packIndex = 0; packIndex < maxPackSlot; packIndex++)
            {
                // Skip if this pack slot isn't in our filter
                if (clause.PackSlotBitmask != 0 && ((clause.PackSlotBitmask >> packIndex) & 1) == 0) 
                    continue;
                
                if (!boosterPackStreamInit)
                {
                    boosterPackStream = searchContext.CreateBoosterPackStream(ante, true, false);
                    boosterPackStreamInit = true;
                }
                
                var pack = searchContext.GetNextBoosterPack(ref boosterPackStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream = searchContext.CreateArcanaPackTarotStream(ante, true);
                    }
                    
                    if (searchContext.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize()))
                    {
                        foundSoulInPack = true;
                        break;
                    }
                }
                
                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    if (!spectralStreamInit)
                    {
                        spectralStreamInit = true;
                        spectralStream = searchContext.CreateSpectralPackSpectralStream(ante, true);
                    }
                    
                    if (searchContext.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                    {
                        foundSoulInPack = true;
                        break;
                    }
                }
            }
            
            // If no Soul card found in any pack, return false
            if (!foundSoulInPack)
                return false;
            
            // NOW check if the soul joker matches what we're looking for
            var soulStream = searchContext.CreateSoulJokerStream(ante);
            var wouldBe = searchContext.GetNextJoker(ref soulStream);
            
            // Direct enum comparisons
            if (!clause.IsWildcard && clause.JokerType.HasValue)
            {
                if (wouldBe.GetJoker() != clause.JokerType.Value) return false;
            }
            
            // Direct edition comparison
            if (clause.EditionEnum.HasValue)
            {
                if (wouldBe.Edition != clause.EditionEnum.Value) return false;
            }
            
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clauses != null && _clauses.Count > 0, "MotelyJsonSoulJokerFilter called with null or empty clauses");
            
            // STAGE 1: Vectorized pre-filter - check if the soul joker matches what we're looking for
            VectorMask matchingMask = VectorMask.AllBitsSet;
            
            foreach (var clause in _clauses)
            {
                VectorMask clauseMask = VectorMask.NoBitsSet;
                
                // Check each ante in the clause's bitmask
                for (int ante = 1; ante <= 8; ante++)
                {
                    if ((clause.AnteBitmask & (1UL << (ante - 1))) == 0) continue;
                    
                    // Get the soul joker for this ante (vectorized)
                    var soulStream = ctx.CreateSoulJokerStream(ante);
                    var soulJoker = ctx.GetNextJoker(ref soulStream);
                    
                    // Check if it matches what we're looking for
                    if (clause.JokerType.HasValue && !clause.IsWildcard)
                    {
                        // Extract just the joker type from the item
                        var jokerType = new VectorEnum256<MotelyJoker>(
                            Vector256.BitwiseAnd(soulJoker.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask))
                        );
                        VectorMask matches = VectorEnum256.Equals(jokerType, clause.JokerType.Value);
                        clauseMask |= matches; // OR - can match at any ante
                    }
                    else
                    {
                        clauseMask = VectorMask.AllBitsSet; // Wildcard matches all
                    }
                    
                    // Check edition if specified
                    if (clause.EditionEnum.HasValue)
                    {
                        VectorMask editionMatches = VectorEnum256.Equals(soulJoker.Edition, clause.EditionEnum.Value);
                        clauseMask &= editionMatches; // AND with edition requirement
                    }
                }
                
                matchingMask &= clauseMask; // AND - all clauses must pass
                if (matchingMask.IsAllFalse())
                    return VectorMask.NoBitsSet; // Early exit if no seeds match
            }
            
            // STAGE 2: Individual seed validation to verify The Soul is actually obtainable
            var clauses = _clauses; // Copy for lambda
            return ctx.SearchIndividualSeeds(matchingMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // Check each clause - ALL must pass (AND logic)
                foreach (var clause in clauses)
                {
                    bool clauseMatched = false;
                    
                    // Check each ante in the clause's bitmask
                    for (int ante = 1; ante <= 8; ante++)
                    {
                        if ((clause.AnteBitmask & (1UL << (ante - 1))) == 0) continue;
                        
                        // Use the static method that checks both soul joker AND The Soul card availability
                        if (CheckAnteForSoulJoker(ante, clause, ref singleCtx))
                        {
                            clauseMatched = true;
                            break; // This clause is satisfied
                        }
                    }
                    
                    if (!clauseMatched)
                        return false; // This clause failed, seed doesn't match
                }
                
                return true; // All clauses passed
            });
        }
    }
}