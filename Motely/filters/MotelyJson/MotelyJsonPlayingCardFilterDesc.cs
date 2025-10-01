using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on playing card criteria from JSON configuration.
/// </summary>
public struct MotelyJsonPlayingCardFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> playingCardClauses)
    : IMotelySeedFilterDesc<MotelyJsonPlayingCardFilterDesc.MotelyJsonPlayingCardFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _playingCardClauses = playingCardClauses;

    public MotelyJsonPlayingCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var clause in _playingCardClauses)
        {
            if (clause.EffectiveAntes != null)
            {
                foreach (var ante in clause.EffectiveAntes)
                {
                    ctx.CacheBoosterPackStream(ante);
                }
            }
        }
        
        // PRE-CALCULATE ANTE RANGE
        int minAnte = int.MaxValue, maxAnte = int.MinValue;
        foreach (var clause in _playingCardClauses)
        {
            foreach (var ante in clause.EffectiveAntes)
            {
                if (ante < minAnte) minAnte = ante;
                if (ante > maxAnte) maxAnte = ante;
            }
        }
        
        return new MotelyJsonPlayingCardFilter(_playingCardClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonPlayingCardFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            // Copy struct members to locals to avoid CS1673
            var clauses = _clauses;
            
            // USE THE SHARED FUNCTION - same logic as scoring!
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                // Check all clauses using the SAME shared function used in scoring
                foreach (var clause in clauses)
                {
                    bool clauseMatched = false;
                    
                    foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
                    {
                        // Use the SHARED function with earlyExit=true for filtering
                        if (MotelyJsonScoring.CountPlayingCardOccurrences(ref singleCtx, clause, ante, earlyExit: true) > 0)
                        {
                            clauseMatched = true;
                            break;
                        }
                    }
                    
                    // All clauses must match
                    if (!clauseMatched)
                        return false;
                }
                
                return true;
            });
        }
    }
}