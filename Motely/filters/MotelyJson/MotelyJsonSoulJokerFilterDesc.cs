using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

        // Cache all streams we'll need
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheSoulJokerStream(ante);
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
            var soulStream = searchContext.CreateSoulJokerStream(ante);
            var wouldBe = searchContext.GetNextJoker(ref soulStream);
            
            // Check if joker type matches
            if (!clause.IsWildcard && clause.JokerType.HasValue)
            {
                if (wouldBe.Type != (MotelyItemType)clause.JokerType.Value) return false;
            }
            
            // Check if edition matches
            if (clause.EditionEnum.HasValue)
            {
                if (wouldBe.Edition != clause.EditionEnum.Value) return false;
            }
            
            // Now verify Soul cards are actually obtainable from packs
            MotelySingleTarotStream tarotStream = default;
            MotelySingleSpectralStream spectralStream = default;
            MotelySingleBoosterPackStream boosterPackStream = default;
            bool boosterPackStreamInit = false;
            bool tarotStreamInit = false, spectralStreamInit = false;
            
            int maxPackSlot = clause.PackSlotBitmask == 0 ? (2 + ante) : 
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
                        return true;
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
                        return true;
                    }
                }
            }
            
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clauses != null && _clauses.Count > 0, "MotelyJsonSoulJokerFilter executed with no soul joker clauses!");

            // STAGE 1: Vectorized fast pre-check - eliminate 99%+ of seeds by checking soul joker stream
            var jokerMask = VectorMask.AllBitsSet;
            var clauseJokerMasks = new VectorMask[_clauses.Count];
            for (int i = 0; i < clauseJokerMasks.Length; i++) clauseJokerMasks[i] = VectorMask.NoBitsSet;
            
            for (int ante = _minAnte; ante <= _maxAnte; ante++)
            {
                ulong anteBit = 1UL << (ante - 1);
                var soulJokerStream = ctx.CreateSoulJokerStream(ante);
                var soulJokers = ctx.GetNextJoker(ref soulJokerStream);
                
                for (int clauseIndex = 0; clauseIndex < _clauses.Count; clauseIndex++)
                {
                    var clause = _clauses[clauseIndex];
                    if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) continue;
                    
                    VectorMask typeMatches = VectorMask.AllBitsSet;
                    if (!clause.IsWildcard && clause.JokerType.HasValue)
                    {
                        var targetType = (MotelyItemType)clause.JokerType.Value;
                        typeMatches = VectorEnum256.Equals(soulJokers.Type, targetType);
                    }
                    
                    VectorMask editionMatches = VectorMask.AllBitsSet;
                    if (clause.EditionEnum.HasValue)
                    {
                        editionMatches = VectorEnum256.Equals(soulJokers.Edition, clause.EditionEnum.Value);
                    }
                    
                    var combinedMatches = typeMatches & editionMatches;
                    clauseJokerMasks[clauseIndex] |= combinedMatches;
                }
            }

            // Fast fail - exit early if pre-check eliminates all seeds
            for (int i = 0; i < clauseJokerMasks.Length; i++)
            {
                jokerMask &= clauseJokerMasks[i];
                if (jokerMask.IsAllFalse()) return VectorMask.NoBitsSet; // FAST EXIT!
            }
            
            // STAGE 2: Drop to individual seeds for pack verification (handles variable pack sizes)
            var clauses = _clauses; // Capture for lambda
            int minAnte = _minAnte, maxAnte = _maxAnte; // Capture for lambda
            
            return ctx.SearchIndividualSeeds(jokerMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // We already know from Stage 1 that the soul joker exists and matches!
                // Track which clauses have been satisfied
                var clausesSatisfied = new bool[clauses.Count];
                
                // Check all antes in order (ante loop outside, clauses inside)
                for (int ante = minAnte; ante <= maxAnte; ante++)
                {
                    for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                    {
                        // Skip if this clause is already satisfied
                        if (clausesSatisfied[clauseIndex]) continue;
                        
                        var clause = clauses[clauseIndex];
                        ulong anteBit = 1UL << (ante - 1);
                        if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) continue;
                        
                        if (CheckAnteForSoulJoker(ante, clause, ref singleCtx))
                        {
                            clausesSatisfied[clauseIndex] = true;
                        }
                    }
                }
                
                // All clauses must be satisfied
                for (int i = 0; i < clausesSatisfied.Length; i++)
                {
                    if (!clausesSatisfied[i]) return false;
                }
                
                return true;
            });
        }
    }
}