using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on boss blind criteria from JSON configuration.
/// </summary>
public struct MotelyJsonBossFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> bossClauses)
    : IMotelySeedFilterDesc<MotelyJsonBossFilterDesc.MotelyJsonBossFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _bossClauses = bossClauses;

    public MotelyJsonBossFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Find min/max antes for optimization
        int minAnte = 39, maxAnte = 1;
        foreach (var clause in _bossClauses)
        {
            if (clause.EffectiveAntes != null)
            {
                foreach (var ante in clause.EffectiveAntes)
                {
                    if (ante < minAnte) minAnte = ante;
                    if (ante > maxAnte) maxAnte = ante;
                }
            }
        }

        return new MotelyJsonBossFilter(_bossClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonBossFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
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
            var maxAnte = _maxAnte;
            
            // USE THE SHARED FUNCTION - same logic as scoring!
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                var state = new MotelyRunState();
                
                // Check all clauses using the SAME shared function used in scoring
                foreach (var clause in clauses)
                {
                    if (!clause.BossEnum.HasValue) continue;
                    
                    bool matched = false;
                    foreach (var ante in clause.EffectiveAntes)
                    {
                        if (MotelyJsonScoring.CheckBossSingle(ref singleCtx, clause, ante, ref state))
                        {
                            matched = true;
                            break;
                        }
                    }
                    
                    if (!matched) return false;
                }
                
                return true;
            });
        }
    }
}
