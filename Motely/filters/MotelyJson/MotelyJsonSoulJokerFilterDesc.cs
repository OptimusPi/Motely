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
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clauses != null && _clauses.Count > 0, "MotelyJsonSoulJokerFilter executed with no soul joker clauses!");

            // STAGE 1: Pre-filter with vectorized joker matching
            var jokerMask = VectorMask.AllBitsSet;
            var clauseJokerMasks = new VectorMask[_clauses.Count];
            for (int i = 0; i < clauseJokerMasks.Length; i++) clauseJokerMasks[i] = VectorMask.NoBitsSet;
            
            // Check soul jokers
            for (int ante = _minAnte; ante <= _maxAnte; ante++)
            {
                ulong anteBit = 1UL << (ante - 1);
                var soulJokerStream = ctx.CreateSoulJokerStream(ante);
                var soulJokers = ctx.GetNextJoker(ref soulJokerStream);
                
                for (int clauseIndex = 0; clauseIndex < _clauses.Count; clauseIndex++)
                {
                    var clause = _clauses[clauseIndex];
                    // Check ante bitmask
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
                    
                    clauseJokerMasks[clauseIndex] |= (typeMatches & editionMatches);
                }
            }
            
            // AND all joker criteria
            for (int i = 0; i < clauseJokerMasks.Length; i++)
            {
                jokerMask &= clauseJokerMasks[i];
                if (jokerMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }
            
            // STAGE 2: Verify The Soul card presence - FULLY VECTORIZED
            var soulCardMask = VectorMask.AllBitsSet;
            var clauseSoulMasks = new VectorMask[_clauses.Count];
            for (int i = 0; i < clauseSoulMasks.Length; i++) clauseSoulMasks[i] = VectorMask.NoBitsSet;
            
            for (int clauseIndex = 0; clauseIndex < _clauses.Count; clauseIndex++)
            {
                var clause = _clauses[clauseIndex];
                VectorMask foundSoulForClause = VectorMask.NoBitsSet;
                
                // Check all antes set in the bitmask
                for (int ante = _minAnte; ante <= _maxAnte; ante++)
                {
                    ulong anteBit = 1UL << (ante - 1);
                    if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) continue;
                    var boosterPackStream = ctx.CreateBoosterPackStream(ante);
                    var tarotStream = ctx.CreateArcanaPackTarotStream(ante, soulOnly: true);
                    var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: true);
                    
                    for (int packIndex = 0; packIndex < 2 + ante; packIndex++)
                    {
                        var pack = ctx.GetNextBoosterPack(ref boosterPackStream);
                        var packType = pack.GetPackType();
                        
                        
                        // Check Arcana packs with vectorized method
                        VectorMask isArcanaPack = VectorEnum256.Equals(packType, MotelyBoosterPackType.Arcana);
                        if (isArcanaPack.IsPartiallyTrue())
                        {
                            // GetPackSize returns a vector, need to get the first value
                            var packSize = pack.GetPackSize()[0];
                            VectorMask hasSoul = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, packSize);
                            foundSoulForClause |= (isArcanaPack & hasSoul);
                        }
                        
                        // Check Spectral packs with vectorized method
                        VectorMask isSpectralPack = VectorEnum256.Equals(packType, MotelyBoosterPackType.Spectral);
                        if (isSpectralPack.IsPartiallyTrue())
                        {
                            // GetPackSize returns a vector, need to get the first value
                            var packSize = pack.GetPackSize()[0];
                            VectorMask hasSoul = ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, packSize);
                            foundSoulForClause |= (isSpectralPack & hasSoul);
                        }
                    }
                }
                
                clauseSoulMasks[clauseIndex] = foundSoulForClause;
            }
            
            // AND all Soul card criteria
            for (int i = 0; i < clauseSoulMasks.Length; i++)
            {
                soulCardMask &= clauseSoulMasks[i];
                if (soulCardMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }
            
            // Return the intersection of both stages
            return jokerMask & soulCardMask;
        }
    }
}