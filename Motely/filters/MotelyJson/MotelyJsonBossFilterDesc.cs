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

    public readonly string Name => "JSON Boss Filter";
    public readonly string Description => "Vectorized boss blind filtering";

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

        return new MotelyJsonBossFilter(_bossClauses, _bossClauses[0].EffectiveAntes[0], _bossClauses[^1].EffectiveAntes[^1]);
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
            
            // COPY EXACT SCORING PATTERN - use individual seed checking like MotelyJsonScoring
            var clausesForLambda = _clauses;
            return ctx.SearchIndividualSeeds(VectorMask.AllBitsSet, (ref MotelySingleSearchContext singleCtx) =>
            {
                var runState = new MotelyRunState();
                
                foreach (var clause in clausesForLambda)
                {
                    if (clause.EffectiveAntes == null || !clause.BossEnum.HasValue) continue;
                    
                    bool clauseSatisfied = false;
                    
                    foreach (var ante in clause.EffectiveAntes)
                    {
                        try
                        {
                            var bossStream = singleCtx.CreateBossStream();
                            var boss = singleCtx.GetBossForAnte(ref bossStream, ante, ref runState);
                            
#if DEBUG
                            Console.WriteLine($"[BossFilter] Ante {ante}: found={boss}, target={clause.BossEnum.Value}, match={boss == clause.BossEnum.Value}");
#endif
                            
                            if (boss == clause.BossEnum.Value)
                            {
                                clauseSatisfied = true;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Boss generation can fail for some seeds
#if DEBUG
                            Console.WriteLine($"[BossFilter] Ante {ante}: EXCEPTION {ex.Message}");
#endif
                            continue;
                        }
                    }
                    
                    if (!clauseSatisfied) return false;
                }
                
                return true;
            });
        }
    }
}