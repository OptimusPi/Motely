using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

public struct MotelyJsonJokerFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> jokerClauses)
    : IMotelySeedFilterDesc<MotelyJsonJokerFilterDesc.MotelyJsonJokerFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _jokerClauses = jokerClauses;

    public readonly string Name => "JSON Joker Filter";
    public readonly string Description => "Vectorized joker filtering";

    public MotelyJsonJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var clause in _jokerClauses)
        {
            if (clause.EffectiveAntes != null)
            {
                foreach (var ante in clause.EffectiveAntes)
                {
                    ctx.CacheBoosterPackStream(ante);
                    ctx.CacheShopJokerStream(ante);
                }
            }
        }
        
        return new MotelyJsonJokerFilter(_jokerClauses);
    }

    public struct MotelyJsonJokerFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses = clauses;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            // Keep existing working logic but optimize it later
            var clausesForLambda = _clauses;
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                foreach (var clause in clausesForLambda)
                {
                    var runState = new MotelyRunState();
                    bool found = MotelyJsonScoring.CheckSingleClause(ref singleCtx, clause, ref runState);
                    if (!found) return false;
                }
                
                return true;
            });
        }
    }
}