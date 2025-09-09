using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Motely.Filters;

/// <summary>
/// Verifies The Soul card presence in Arcana/Spectral packs - fully vectorized.
/// Used after MotelyJsonSoulJokerPreFilterDesc to confirm Soul card availability.
/// </summary>
public struct MotelyJsonSoulJokerVerifyFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> soulJokerClauses)
    : IMotelySeedFilterDesc<MotelyJsonSoulJokerVerifyFilterDesc.MotelyJsonSoulJokerVerifyFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _soulJokerClauses = soulJokerClauses;

    public MotelyJsonSoulJokerVerifyFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Find the min and max ante
        int minAnte = int.MaxValue, maxAnte = int.MinValue;
        foreach (var clause in _soulJokerClauses)
        {
            foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
            {
                if (ante < minAnte) minAnte = ante;
                if (ante > maxAnte) maxAnte = ante;
            }
        }

        // Cache the streams we'll need
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheBoosterPackStream(ante);
            ctx.CacheArcanaPackTarotStream(ante);
            // Note: No cache method exists for spectral streams in MotelyFilterCreationContext
        }
        
        return new MotelyJsonSoulJokerVerifyFilter(_soulJokerClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonSoulJokerVerifyFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clauses != null && _clauses.Count > 0, "MotelyJsonSoulJokerVerifyFilter executed with no soul joker clauses!");

            var resultMask = VectorMask.AllBitsSet;
            var clauseMasks = new VectorMask[_clauses.Count];
            for (int i = 0; i < clauseMasks.Length; i++) clauseMasks[i] = VectorMask.NoBitsSet;
            
            // Process each clause
            for (int clauseIndex = 0; clauseIndex < _clauses.Count; clauseIndex++)
            {
                var clause = _clauses[clauseIndex];
                VectorMask foundSoulForClause = VectorMask.NoBitsSet;
                
                foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
                {
                    var boosterPackStream = ctx.CreateBoosterPackStream(ante);
                    var tarotStream = ctx.CreateArcanaPackTarotStream(ante, soulOnly: true);
                    var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: true);
                    
                    // Check each pack (2 + ante packs total)
                    for (int packIndex = 0; packIndex < 2 + ante; packIndex++)
                    {
                        var pack = ctx.GetNextBoosterPack(ref boosterPackStream);
                        var packType = pack.GetPackType();
                        
                        // Check Arcana packs
                        VectorMask isArcanaPack = VectorEnum256.Equals(packType, MotelyBoosterPackType.Arcana);
                        if (isArcanaPack.IsPartiallyTrue())
                        {
                            // Use the vectorized method to check for The Soul
                            // GetPackSize returns a vector, need to get the first value
                            var packSize = pack.GetPackSize()[0];
                            VectorMask hasSoul = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, packSize);
                            foundSoulForClause |= (isArcanaPack & hasSoul);
                        }
                        
                        // Check Spectral packs
                        VectorMask isSpectralPack = VectorEnum256.Equals(packType, MotelyBoosterPackType.Spectral);
                        if (isSpectralPack.IsPartiallyTrue())
                        {
                            // Use the vectorized method to check for The Soul
                            // GetPackSize returns a vector, need to get the first value
                            var packSize = pack.GetPackSize()[0];
                            VectorMask hasSoul = ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, packSize);
                            foundSoulForClause |= (isSpectralPack & hasSoul);
                        }
                    }
                }
                
                clauseMasks[clauseIndex] = foundSoulForClause;
            }
            
            // AND all criteria together - all clauses must find The Soul
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                resultMask &= clauseMasks[i];
                if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }
            
            return resultMask;
        }
    }
}