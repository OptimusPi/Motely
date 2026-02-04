using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters;

/// <summary>
/// ULTRA-FAST edition-only soul joker filter (Value="Any" + edition specified)
/// CRITICAL OPTIMIZATION: Skips ALL soul card detection in vectorized mode!
/// Only peeks edition stream (1-2 PRNG calls per ante) for instant early-exit
/// </summary>
public readonly struct MotelyJsonSoulJokerEditionOnlyFilterDesc(
    MotelyJsonSoulJokerFilterCriteria criteria
)
    : IMotelySeedFilterDesc<MotelyJsonSoulJokerEditionOnlyFilterDesc.MotelyJsonSoulJokerEditionOnlyFilter>
{
    private readonly MotelyJsonSoulJokerFilterCriteria _criteria = criteria;

    public MotelyJsonSoulJokerEditionOnlyFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Cache soul joker EDITION-ONLY streams for all antes (ultra-fast!)
        for (int ante = _criteria.MinAnte; ante <= _criteria.MaxAnte; ante++)
        {
            ctx.CacheSoulJokerStream(
                ante,
                MotelyJokerFixedRarityStreamFlags.ExcludeJokerType
                    | MotelyJokerFixedRarityStreamFlags.ExcludeStickers
            );
        }

        return new MotelyJsonSoulJokerEditionOnlyFilter(
            _criteria.Clauses,
            _criteria.MinAnte,
            _criteria.MaxAnte
        );
    }

    public struct MotelyJsonSoulJokerEditionOnlyFilter : IMotelySeedFilter
    {
        private readonly List<MotelyJsonSoulJokerFilterClause> _clauses;
        private readonly int _minAnte;
        private readonly int _maxAnte;

        public MotelyJsonSoulJokerEditionOnlyFilter(
            List<MotelyJsonSoulJokerFilterClause> clauses,
            int minAnte,
            int maxAnte
        )
        {
            _clauses = clauses;
            _minAnte = minAnte;
            _maxAnte = maxAnte;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(
                _clauses != null && _clauses.Count > 0,
                "SoulJokerEditionOnly filter created with empty clauses - this is a programming error!"
            );

            // For edition-only checks, we DON'T need to detect soul cards!
            // We just check the edition stream for the specific ante(s) required
            // This is BLAZING FAST (1-2 PRNG calls per ante)

            VectorMask resultMask = VectorMask.AllBitsSet;

            // Process clauses in order (most restrictive first, already sorted in criteria)
            foreach (var clause in _clauses)
            {
                VectorMask clauseMatched = VectorMask.NoBitsSet;

                // Check each ante this clause wants
                for (int ante = _minAnte; ante <= _maxAnte; ante++)
                {
                    if (ante >= clause.WantedAntes.Length || !clause.WantedAntes[ante])
                        continue;

                    // Create edition-ONLY stream for this ante (ULTRA FAST!)
                    // ExcludeJokerType + ExcludeStickers = only check edition PRNG (1 cheap call!)
                    var editionStream = ctx.CreateSoulJokerStream(
                        ante,
                        MotelyJokerFixedRarityStreamFlags.ExcludeJokerType
                            | MotelyJokerFixedRarityStreamFlags.ExcludeStickers
                    );

                    // Check first soul joker edition
                    clauseMatched |= VectorEnum256.Equals(
                        ctx.GetNextJoker(ref editionStream).Edition,
                        clause.EditionEnum!.Value
                    );

                    // Check second soul joker edition (in case ante has 2 soul jokers)
                    //clauseMatched |= VectorEnum256.Equals(ctx.GetNextJoker(ref editionStream).Edition, clause.EditionEnum!.Value);
                }

                // If clause requires Min count, handle in individual scoring
                // For vectorized mode, we just need to know if ANY match exists
                resultMask &= clauseMatched;

                // EARLY EXIT: If entire vector failed, no point checking other clauses
                if (resultMask.IsAllFalse())
                    return VectorMask.NoBitsSet;
            }

            // Edition-only filters are wildcard (Any) by definition; no face verification required.
            // Keep this strictly SIMD-only for maximum hotpath performance.
            return resultMask;
        }
    }
}
