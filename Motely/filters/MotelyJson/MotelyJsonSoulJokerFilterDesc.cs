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

    // Pre-calculated data for hot path optimization
    private struct ClausePreCalc
    {
        public bool HasPackSlots;
        
        public static ClausePreCalc[] CreateArray(List<MotelyJsonSoulJokerFilterClause> clauses)
        {
            var result = new ClausePreCalc[clauses.Count];
            for (int i = 0; i < clauses.Count; i++)
            {
                var clause = clauses[i];
                result[i] = new ClausePreCalc
                {
                    HasPackSlots = clause.WantedPackSlots != null && HasAnyTrue(clause.WantedPackSlots)
                };
            }
            return result;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasAnyTrue(bool[] array)
        {
            for (int i = 0; i < array.Length; i++)
                if (array[i]) return true;
            return false;
        }
    }
    
    public struct MotelyJsonSoulJokerFilter(List<MotelyJsonSoulJokerFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonSoulJokerFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;
        private readonly int _clauseCount = clauses.Count;
        private readonly ClausePreCalc[] _preCalc = ClausePreCalc.CreateArray(clauses);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clauses != null && _clauseCount > 0, "MotelyJsonSoulJokerFilter called with null or empty clauses");
            
            // STAGE 1: Vectorized pre-filter - check if the soul joker matches what we're looking for
            // CRITICAL: Loop ANTES first, then clauses - to match individual validation order
            VectorMask matchingMask = VectorMask.AllBitsSet;
            
            // Create a mask for each clause
            VectorMask[] clauseMasks = new VectorMask[_clauseCount];
            for (int i = 0; i < clauseMasks.Length; i++)
                clauseMasks[i] = VectorMask.NoBitsSet;
            
            // Loop ANTES first for proper stream synchronization
            // Start from ante 0 to support ante 0 filters!
            for (int ante = 0; ante <= 8; ante++)
            {
                // Skip antes that no clause cares about (optimized loop)
                bool anteNeeded = false;
                for (int i = 0; i < _clauseCount; i++)
                {
                    var wantedAntes = _clauses[i].WantedAntes;
                    if (ante < wantedAntes.Length && wantedAntes[ante])
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
                for (int clauseIdx = 0; clauseIdx < _clauseCount; clauseIdx++)
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
                catch (NullReferenceException)
                {
                    throw;
                }
            });
        }
    }
}