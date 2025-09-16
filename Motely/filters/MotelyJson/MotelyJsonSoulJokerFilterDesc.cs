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
        }
        
        return new MotelyJsonSoulJokerFilter(_soulJokerClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonSoulJokerFilter(List<MotelyJsonSoulJokerFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonSoulJokerFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;

        /// <summary>
        /// SHARED FUNCTION - used by both filter (with earlyExit=true) and scoring (with earlyExit=false)
        /// FIXED: Uses the CORRECT order like PerkeoObservatory - check Soul Joker result FIRST, then verify Soul card exists
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool CheckSoulJokerForSeed(List<MotelyJsonSoulJokerFilterClause> clauses, ref MotelySingleSearchContext searchContext, bool earlyExit = true)
        {
            int matchedClauses = 0;
            bool[] clauseSatisfied = new bool[clauses.Count];
            
            // Loop ANTES first - create streams ONCE per ante, check ALL clauses
            for (int ante = 1; ante <= 8; ante++)
            {
                // Skip antes that no clause cares about - use proper array indexing (ante is 1-based)
                bool anteNeeded = false;
                foreach (var clause in clauses)
                {
                    if (ante < clause.WantedAntes.Length && clause.WantedAntes[ante])
                    {
                        anteNeeded = true;
                        break;
                    }
                }
                if (!anteNeeded) continue;
                
                // CORRECT ORDER: Check Soul Joker result FIRST (like PerkeoObservatory does)
                var soulStream = searchContext.CreateSoulJokerStream(ante);
                var soulJoker = searchContext.GetNextJoker(ref soulStream);
                
                // Check ALL clauses for this ante's Soul Joker result
                for (int clauseIdx = 0; clauseIdx < clauses.Count; clauseIdx++)
                {
                    var clause = clauses[clauseIdx];
                    
                    // Skip if clause doesn't care about this ante
                    if (ante >= clause.WantedAntes.Length || !clause.WantedAntes[ante]) continue;
                    
                    // Skip if already satisfied
                    if (clauseSatisfied[clauseIdx]) continue;
                    
                    // Check joker type first - cast MotelyJoker to MotelyItemType for comparison
                    if (clause.JokerType.HasValue && !clause.IsWildcard && soulJoker.Type != (MotelyItemType)clause.JokerType.Value)
                        continue;
                    
                    // Check edition
                    if (clause.EditionEnum.HasValue && soulJoker.Edition != clause.EditionEnum.Value)
                        continue;
                    
                    // Soul Joker result matches! Now verify Soul card exists in the right pack slot
                    bool soulCardFound = false;
                    
                    // Create streams for pack checking
                    var boosterPackStream = searchContext.CreateBoosterPackStream(ante, true, false);
                    var tarotStream = searchContext.CreateArcanaPackTarotStream(ante, true);
                    var spectralStream = searchContext.CreateSpectralPackSpectralStream(ante, true);
                    
                    int maxPackSlot = ante == 1 ? 4 : 6;
                    bool tarotStreamInit = false, spectralStreamInit = false;
                    
                    for (int packIndex = 0; packIndex < maxPackSlot; packIndex++)
                    {
                        var pack = searchContext.GetNextBoosterPack(ref boosterPackStream);
                        
                        // Check pack slot requirement
                        if (clause.WantedPackSlots.Any(x => x) && (packIndex >= clause.WantedPackSlots.Length || !clause.WantedPackSlots[packIndex]))
                            continue;
                        
                        // Check mega requirement  
                        if (clause.RequireMega && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                            continue;
                        
                        bool hasSoul = false;
                        if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                        {
                            if (!tarotStreamInit)
                            {
                                tarotStreamInit = true;
                                tarotStream = searchContext.CreateArcanaPackTarotStream(ante, true);
                            }
                            hasSoul = searchContext.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize());
                        }
                        else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                        {
                            if (!spectralStreamInit)
                            {
                                spectralStreamInit = true;
                                spectralStream = searchContext.CreateSpectralPackSpectralStream(ante, true);
                            }
                            hasSoul = searchContext.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize());
                        }
                        
                        if (hasSoul)
                        {
                            soulCardFound = true;
                            break;
                        }
                    }
                    
                    if (soulCardFound)
                    {
                        // This clause is satisfied!
                        clauseSatisfied[clauseIdx] = true;
                        matchedClauses++;
                        
                        if (earlyExit && matchedClauses == clauses.Count)
                            return true; // All clauses satisfied - early exit for filter
                    }
                }
            }
            
            // For filter: return true only if ALL clauses satisfied
            // For scoring: return count of satisfied clauses (but we return bool for now)
            return matchedClauses == clauses.Count;
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
                    
                    // Fix array indexing - ante is 1-based but arrays are 0-based
                    if (ante >= clause.WantedAntes.Length || !clause.WantedAntes[ante]) continue;
                    
                    // Get the soul joker for this ante (vectorized)
                    var soulStream = ctx.CreateSoulJokerStream(ante);
                    var soulJoker = ctx.GetNextJoker(ref soulStream);
                    
                    // Check if it matches what we're looking for - match the individual validation logic
                    VectorMask anteMatches = VectorMask.AllBitsSet;
                    
                    if (clause.JokerType.HasValue && !clause.IsWildcard)
                    {
                        // Just cast it - MotelyJoker can be cast directly to MotelyItemType
                        anteMatches = VectorEnum256.Equals(soulJoker.Type, (MotelyItemType)clause.JokerType.Value);
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
            
            // Check if any clause has no matches across all antes
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                if (clauseMasks[i].IsAllFalse())
                    return VectorMask.NoBitsSet; // Early exit if any clause can't be satisfied
            }
            
            // AND all clause masks together
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                matchingMask &= clauseMasks[i];
                if (matchingMask.IsAllFalse())
                    return VectorMask.NoBitsSet; // Early exit if no seeds match
            }
            
            // STAGE 2: Individual seed validation using the corrected shared function
            var clauses = _clauses; // Copy for lambda
            return ctx.SearchIndividualSeeds(matchingMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                return CheckSoulJokerForSeed(clauses, ref singleCtx, true); // earlyExit=true for filter
            });
        }
    }
}