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
public readonly struct MotelyJsonSoulJokerFilterDesc(MotelyJsonSoulJokerFilterCriteria criteria)
    : IMotelySeedFilterDesc<MotelyJsonSoulJokerFilterDesc.MotelyJsonSoulJokerFilter>
{
    private readonly MotelyJsonSoulJokerFilterCriteria _criteria = criteria;

    public MotelyJsonSoulJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Use pre-calculated values from criteria
        int minAnte = _criteria.MinAnte;
        int maxAnte = _criteria.MaxAnte;

        // Cache all streams we'll need for BOTH vectorized and individual checks
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            // For vectorized pre-filter
            ctx.CacheSoulJokerStream(ante);
        }

        return new MotelyJsonSoulJokerFilter(_criteria.Clauses, minAnte, maxAnte);
    }

    public struct MotelyJsonSoulJokerFilter(List<MotelyJsonSoulJokerFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonSoulJokerFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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
            for (int ante = _minAnte; ante <= _maxAnte; ante++)
            {
                // Skip antes that no clause cares about
                bool anteNeeded = false;
                for (int i = 0; i < _clauses.Count; i++)
                {
                    if (ante < _clauses[i].WantedAntes.Length && _clauses[i].WantedAntes[ante])
                    {
                        anteNeeded = true;
                        break;
                    }
                }
                if (!anteNeeded) continue;
                
                // Get the soul joker for this ante ONCE (vectorized)
                var soulStream = ctx.CreateSoulJokerStream(ante);
                var soulJoker = ctx.GetNextJoker(ref soulStream);
                
                // Check each clause against this same soul joker
                for (int clauseIdx = 0; clauseIdx < _clauses.Count; clauseIdx++)
                {
                    var clause = _clauses[clauseIdx];
                    
                    // Fix array indexing - ante is 1-based but arrays are 0-based
                    if (ante >= clause.WantedAntes.Length || !clause.WantedAntes[ante]) continue;
                    
                    // Check if it matches what we're looking for - match the individual validation logic
                    VectorMask anteMatches = VectorMask.AllBitsSet;
                    
                    if (clause.JokerType.HasValue && !clause.IsWildcard)
                    {
                        // FIXED: Soul joker vector contains raw joker values, not full ItemType values  
                        var targetSoulJoker = (MotelyItemType)clause.JokerType.Value;
                        anteMatches = VectorEnum256.Equals(soulJoker.Type, targetSoulJoker);
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
                // Defensive check for null clauses
                if (clauses == null || clauses.Count == 0)
                    return true; // No clauses means pass through
                
                // Filter out any null clauses that might have been created
                var validClauses = new List<MotelyJsonSoulJokerFilterClause>();
                foreach (var clause in clauses)
                {
                    if (clause != null && clause.WantedAntes != null)
                        validClauses.Add(clause);
                }
                if (validClauses.Count == 0)
                    return true; // No valid clauses means pass through
                    
                try 
                {
                    return MotelyJsonScoring.CheckSoulJokerForSeed(validClauses, ref singleCtx, true); // earlyExit=true for filter
                }
                catch (NullReferenceException ex)
                {
                    Console.WriteLine($"[DEBUG] NullRef in CheckSoulJokerForSeed - ValidClauses: {validClauses?.Count}, Exception: {ex.Message}");
                    Console.WriteLine($"[DEBUG] Stack: {ex.StackTrace}");
                    if (validClauses != null)
                    {
                        for (int i = 0; i < validClauses.Count; i++)
                        {
                            var clause = validClauses[i];
                            Console.WriteLine($"[DEBUG] Clause {i}: WantedAntes={clause?.WantedAntes?.Length}, WantedPackSlots={clause?.WantedPackSlots?.Length}");
                        }
                    }
                    throw; // Re-throw after logging
                }
            });
        }
    }
}