using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on soul joker criteria from JSON configuration.
/// </summary>
public struct MotelyJsonSoulJokerFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> soulJokerClauses)
    : IMotelySeedFilterDesc<MotelyJsonSoulJokerFilterDesc.MotelyJsonSoulJokerFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _soulJokerClauses = soulJokerClauses;

    public readonly string Name => "JSON Soul Joker Filter";
    public readonly string Description => "Soul joker filtering with The Soul card verification";

    public MotelyJsonSoulJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var clause in _soulJokerClauses)
        {
            if (clause.EffectiveAntes != null)
            {
                foreach (var ante in clause.EffectiveAntes)
                {
                    ctx.CacheBoosterPackStream(ante);
                }
            }
        }
        
        return new MotelyJsonSoulJokerFilter(_soulJokerClauses);
    }

    public struct MotelyJsonSoulJokerFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses = clauses;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            // STEP 1: VECTORIZED soul joker type/edition pre-filtering 
            var resultMask = VectorMask.AllBitsSet;
            
            foreach (var clause in _clauses)
            {
                var clauseMask = VectorMask.NoBitsSet;
                
                foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
                {
                    // VECTORIZED: Get soul joker for ALL 8 seeds at once
                    var soulJokerStream = ctx.CreateSoulJokerStream(ante);
                    var soulJokers = ctx.GetNextJoker(ref soulJokerStream);
                    
                    // VECTORIZED: Check type for ALL 8 seeds
                    VectorMask typeMatches = VectorMask.AllBitsSet;
                    if (!clause.IsWildcard && clause.JokerEnum.HasValue)
                    {
                        var targetType = (MotelyItemType)clause.JokerEnum.Value;
                        typeMatches = VectorEnum256.Equals(soulJokers.Type, targetType);
                    }
                    
                    // VECTORIZED: Check edition for ALL 8 seeds
                    VectorMask editionMatches = VectorMask.AllBitsSet;
                    if (clause.EditionEnum.HasValue)
                    {
                        editionMatches = VectorEnum256.Equals(soulJokers.Edition, clause.EditionEnum.Value);
                    }
                    
                    clauseMask |= (typeMatches & editionMatches);
                }
                
                resultMask &= clauseMask;
                if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }
            
            // STEP 2: Individual Soul card checking for survivors only
            if (resultMask.IsAllFalse())
                return VectorMask.NoBitsSet;
            
            var clausesForLambda = _clauses;
            return ctx.SearchIndividualSeeds(resultMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                foreach (var clause in clausesForLambda)
                {
                    bool foundSoulCard = false;
                    
                    foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
                    {
                        // EXACT NATIVE PATTERN: Initialize streams with flags
                        MotelySingleBoosterPackStream boosterPackStream = default;
                        MotelySingleTarotStream tarotStream = default;
                        MotelySingleSpectralStream spectralStream = default;
                        bool boosterPackStreamInit = false;
                        bool tarotStreamInit = false, spectralStreamInit = false;
                        
                        for (int i = 0; i < 2 + ante; i++)
                        {
                            if (!boosterPackStreamInit)
                            {
                                boosterPackStream = singleCtx.CreateBoosterPackStream(ante, true, false);
                                boosterPackStreamInit = true;
                            }
                            
                            var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                            
                            if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                            {
                                if (!tarotStreamInit)
                                {
                                    tarotStreamInit = true;
                                    tarotStream = singleCtx.CreateArcanaPackTarotStream(ante, true);
                                }
                                
                                if (singleCtx.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize()))
                                {
                                    foundSoulCard = true;
                                    break;
                                }
                            }
                            else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                            {
                                if (!spectralStreamInit)
                                {
                                    spectralStreamInit = true;
                                    spectralStream = singleCtx.CreateSpectralPackSpectralStream(ante, true);
                                }
                                
                                if (singleCtx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                                {
                                    foundSoulCard = true;
                                    break;
                                }
                            }
                        }
                        
                        if (foundSoulCard) break;
                    }
                    
                    if (!foundSoulCard) return false;
                }
                
                return true;
            });
        }
    }
}