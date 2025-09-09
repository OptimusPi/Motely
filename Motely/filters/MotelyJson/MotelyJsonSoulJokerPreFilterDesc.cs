using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters;

/// <summary>
/// Pre-filter for soul jokers - fully vectorized joker matching without Soul card verification.
/// This is a fast pre-filter that narrows down seeds before checking for The Soul card.
/// </summary>
public readonly struct MotelyJsonSoulJokerPreFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> soulJokerClauses)
    : IMotelySeedFilterDesc<MotelyJsonSoulJokerPreFilterDesc.MotelyJsonSoulJokerPreFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _soulJokerClauses = soulJokerClauses;

    public MotelyJsonSoulJokerPreFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Find the min and max ante
        int minAnte = int.MaxValue, maxAnte = int.MinValue;
        foreach (MotelyJsonConfig.MotleyJsonFilterClause clause in _soulJokerClauses)
        {
            foreach (int ante in clause.EffectiveAntes)
            {
                if (ante < minAnte) minAnte = ante;
                if (ante > maxAnte) maxAnte = ante;
            }
        }

        // Cache the soul joker stream
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheSoulJokerStream(ante);
        }

        return new MotelyJsonSoulJokerPreFilter(_soulJokerClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonSoulJokerPreFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clauses != null && _clauses.Count > 0, "MotelyJsonSoulJokerPreFilter executed with no soul joker clauses!");

            var resultMask = VectorMask.AllBitsSet;
            var clauseMasks = new VectorMask[_clauses.Count];
            for (int i = 0; i < clauseMasks.Length; i++) clauseMasks[i] = VectorMask.NoBitsSet;

            // ANTE LOOP - fully vectorized soul joker checking
            for (int ante = _minAnte; ante <= _maxAnte; ante++)
            {
                var soulJokerStream = ctx.CreateSoulJokerStream(ante);
                var soulJokers = ctx.GetNextJoker(ref soulJokerStream);

                // Check each criteria for this ante
                for (int clauseIndex = 0; clauseIndex < _clauses.Count; clauseIndex++)
                {
                    var clause = _clauses[clauseIndex];
                    if (clause.EffectiveAntes == null) continue;
                    if (Array.IndexOf(clause.EffectiveAntes, ante) < 0) continue;

                    // VECTORIZED type check
                    VectorMask typeMatches = VectorMask.AllBitsSet;
                    if (!clause.IsWildcard && clause.JokerEnum.HasValue)
                    {
                        var targetType = (MotelyItemType)clause.JokerEnum.Value;
                        typeMatches = VectorEnum256.Equals(soulJokers.Type, targetType);
                    }

                    // VECTORIZED edition check
                    VectorMask editionMatches = VectorMask.AllBitsSet;
                    if (clause.EditionEnum.HasValue)
                    {
                        editionMatches = VectorEnum256.Equals(soulJokers.Edition, clause.EditionEnum.Value);
                    }

                    clauseMasks[clauseIndex] |= (typeMatches & editionMatches); // OR across antes
                }
            }

            // AND all criteria together
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                resultMask &= clauseMasks[i];
                if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }

            return resultMask;
        }
    }
}