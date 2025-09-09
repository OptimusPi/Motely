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
            
            // PURE VECTORIZED BOSS FILTERING
            var resultMask = VectorMask.AllBitsSet;
            
            foreach (var clause in _clauses)
            {
                if (!clause.BossEnum.HasValue) continue;
                
                var clauseMask = VectorMask.NoBitsSet;
                
                // Create boss stream ONCE and maintain state across antes
                var bossStream = ctx.CreateBossStream(1); // Start from ante 1
                
                // Generate bosses for all antes up to the maximum needed
                int maxAnte = clause.EffectiveAntes.Max();
                var bosses = new VectorEnum256<MotelyBossBlind>[maxAnte];
                
                for (int ante = 1; ante <= maxAnte; ante++)
                {
                    bosses[ante - 1] = ctx.GetNextBoss(ref bossStream);
                }
                
                // Check if any of the requested antes match
                foreach (var ante in clause.EffectiveAntes)
                {
                    VectorMask matches = VectorEnum256.Equals(bosses[ante - 1], clause.BossEnum.Value);
                    clauseMask |= matches;
                }
                
                resultMask &= clauseMask;
                if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }
            
            return resultMask;
        }
    }
}
