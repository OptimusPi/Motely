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
                
                // Check if we should consider this pack (must be Mega if requireMega is true)
                bool shouldCheckPack = !(clause.RequireMega && pack.GetPackSize() != MotelyBoosterPackSize.Mega);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream = searchContext.CreateArcanaPackTarotStream(ante, true);
                    }
                    
                    // ALWAYS advance the stream to keep RNG in sync
                    bool hasSoul = searchContext.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize());
                    
                    // Only count it if we should check this pack
                    if (shouldCheckPack && hasSoul)
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
                    
                    // ALWAYS advance the stream to keep RNG in sync
                    bool hasSoul = searchContext.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize());
                    
                    // Only count it if we should check this pack
                    if (shouldCheckPack && hasSoul)
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
            // CRITICAL: Loop ANTES first, then clauses - to match individual validation order
            VectorMask matchingMask = VectorMask.AllBitsSet;
            
            // Create a mask for each clause
            VectorMask[] clauseMasks = new VectorMask[_clauses.Count];
            for (int i = 0; i < clauseMasks.Length; i++)
                clauseMasks[i] = VectorMask.NoBitsSet;
            
            // Loop ANTES first for proper stream synchronization
            for (int ante = 1; ante <= 8; ante++)
            {
                // Check each clause for this ante
                for (int clauseIdx = 0; clauseIdx < _clauses.Count; clauseIdx++)
                {
                    var clause = _clauses[clauseIdx];
                    if ((clause.AnteBitmask & (1UL << (ante - 1))) == 0) continue;
                    
                    // Get the soul joker for this ante (vectorized)
                    var soulStream = ctx.CreateSoulJokerStream(ante);
                    var soulJoker = ctx.GetNextJoker(ref soulStream);
                    
                    // Check if it matches what we're looking for
                    VectorMask anteMatches;
                    if (clause.JokerType.HasValue && !clause.IsWildcard)
                    {
                        // Extract just the joker type from the item
                        var jokerType = new VectorEnum256<MotelyJoker>(
                            Vector256.BitwiseAnd(soulJoker.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask))
                        );
                        anteMatches = VectorEnum256.Equals(jokerType, clause.JokerType.Value);
                    }
                    else
                    {
                        anteMatches = VectorMask.AllBitsSet; // Wildcard matches all
                    }
                    
                    // Check edition if specified
                    if (clause.EditionEnum.HasValue)
                    {
                        VectorMask editionMatches = VectorEnum256.Equals(soulJoker.Edition, clause.EditionEnum.Value);
                        anteMatches &= editionMatches; // AND with edition requirement
                    }
                    
                    clauseMasks[clauseIdx] |= anteMatches; // OR - clause can match at any ante
                }
            }
            
            // AND all clause masks together
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                matchingMask &= clauseMasks[i];
                if (matchingMask.IsAllFalse())
                    return VectorMask.NoBitsSet; // Early exit if no seeds match
            }
            
            // STAGE 2: Individual seed validation with PROPER ANTE-FIRST LOOP ORDER
            var clauses = _clauses; // Copy for lambda
            var minimumAnte = _minAnte;
            var maximumAnte = _maxAnte;
            return ctx.SearchIndividualSeeds(matchingMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // Track which clauses have been satisfied
                bool[] clauseSatisfied = new bool[clauses.Count];
                
                // CRITICAL FIX: Loop ANTES first, then clauses - create streams ONCE per ante
                for (int ante = minimumAnte; ante <= maximumAnte; ante++)
                {
                    // Get clauses that need to be checked for this ante
                    var clausesForThisAnte = clauses.Where(c => (c.AnteBitmask & (1UL << (ante - 1))) != 0).ToList();
                    if (clausesForThisAnte.Count == 0) continue;

                    // Create streams ONCE per ante - shared by ALL clauses for this ante
                    var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, true, false);
                    var tarotStream = singleCtx.CreateArcanaPackTarotStream(ante, true);
                    var spectralStream = singleCtx.CreateSpectralPackSpectralStream(ante, true);
                    var soulStream = singleCtx.CreateSoulJokerStream(ante);

                    // Track which pack slots have soul cards
                    int? soulPackSlot = null;

                    // Find where The Soul card actually is (there's only ONE per ante)
                    int maxPackSlot = ante == 1 ? 4 : 6;
                    MotelyBoosterPackSize? soulPackSize = null;
                    for (int packIndex = 0; packIndex < maxPackSlot; packIndex++)
                    {
                        var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);

                        bool hasSoul = false;
                        if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                        {
                            hasSoul = singleCtx.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize());
                        }
                        else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                        {
                            hasSoul = singleCtx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize());
                        }

                        if (hasSoul)
                        {
                            soulPackSlot = packIndex;
                            soulPackSize = pack.GetPackSize();
                            break; // Found the soul pack
                        }
                    }

                    // If no soul pack found for this ante, all clauses for this ante fail
                    if (!soulPackSlot.HasValue)
                    {
                        // Check if any clauses REQUIRE this ante - if so, seed fails
                        foreach (var clause in clausesForThisAnte)
                        {
                            // If this clause only has this ante, it fails
                            if (System.Numerics.BitOperations.PopCount(clause.AnteBitmask) == 1)
                                return false;
                        }
                        continue; // Try other antes
                    }

                    // Get the actual soul joker (shared stream, read once)
                    var soulJoker = singleCtx.GetNextJoker(ref soulStream);

                    // Now check ALL clauses for this ante using the SAME soul joker
                    for (int clauseIdx = 0; clauseIdx < clausesForThisAnte.Count; clauseIdx++)
                    {
                        var clause = clausesForThisAnte[clauseIdx];
                        
                        // Check if this clause cares about the pack slot where soul was found
                        if (clause.PackSlotBitmask != 0 && ((clause.PackSlotBitmask >> soulPackSlot.Value) & 1) == 0)
                            continue; // This clause doesn't care about this pack slot

                        // Check requireMega - if required but pack isn't mega, skip
                        if (clause.RequireMega && soulPackSize != MotelyBoosterPackSize.Mega)
                            continue; // This clause requires mega but pack isn't mega

                        // Check joker type
                        bool jokerMatches = !clause.JokerType.HasValue || clause.IsWildcard || soulJoker.GetJoker() == clause.JokerType.Value;

                        // Check edition  
                        bool editionMatches = !clause.EditionEnum.HasValue || soulJoker.Edition == clause.EditionEnum.Value;

                        if (jokerMatches && editionMatches)
                        {
                            // Find the index of this clause in the original list and mark it as satisfied
                            for (int i = 0; i < clauses.Count; i++)
                            {
                                if (ReferenceEquals(clauses[i], clause) || clauses[i].Equals(clause))
                                {
                                    clauseSatisfied[i] = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // After checking all antes, verify ALL clauses are satisfied
                for (int i = 0; i < clauses.Count; i++)
                {
                    if (!clauseSatisfied[i])
                        return false; // This clause was never satisfied
                }

                return true; // All clauses satisfied
            });
        }
    }
}