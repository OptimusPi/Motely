using System.Runtime.CompilerServices;

namespace Motely.Filters;

/// <summary>
/// Fully vectorized soul joker filter using two-stage approach:
/// 1. Pre-filter: Fast vectorized joker matching
/// 2. Verify: Vectorized Soul card verification in packs
/// </summary>
public readonly struct MotelyJsonSoulJokerFilterDesc
    : IMotelySeedFilterDesc<MotelyJsonSoulJokerFilterDesc.MotelyJsonSoulJokerFilter>
{
    private readonly MotelyJsonSoulJokerFilterCriteria _criteria;

    public MotelyJsonSoulJokerFilterDesc(MotelyJsonSoulJokerFilterCriteria criteria)
    {
        _criteria = criteria;
    }

    public MotelyJsonSoulJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // MULTI-CLAUSE MODEL: Support multiple clauses, all checked against shared stream
        if (_criteria.Clauses.Count == 0)
            throw new ArgumentException(
                $"MotelyJsonSoulJokerFilter requires at least 1 clause, got 0"
            );

        // Use pre-calculated values from criteria
        int minAnte = _criteria.MinAnte;
        int maxAnte = _criteria.MaxAnte;

        // Cache global face stream (Ante 1)
        ctx.CacheSoulJokerStream(1);

        // Cache all streams we'll need for BOTH vectorized and individual checks
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            // For vectorized pre-filter
            ctx.CacheSoulJokerStream(ante);
            ctx.CacheArcanaPackTarotStream(ante, false);
            ctx.CacheSpectralPackSpectralStream(ante, false);
        }

        return new MotelyJsonSoulJokerFilter(
            _criteria.Clauses,
            minAnte,
            maxAnte,
            _criteria.MaxPackSlotsPerAnte
        );
    }

    public readonly struct MotelyJsonSoulJokerFilter : IMotelySeedFilter
    {
        private readonly List<MotelyJsonSoulJokerFilterClause> Clauses;
        private readonly int MinAnte;
        private readonly int MaxAnte;
        private readonly Dictionary<int, int> MaxPackSlotsPerAnte;

        public MotelyJsonSoulJokerFilter(
            List<MotelyJsonSoulJokerFilterClause> clauses,
            int minAnte,
            int maxAnte,
            Dictionary<int, int> maxPackSlotsPerAnte
        )
        {
            Clauses = clauses;
            MinAnte = minAnte;
            MaxAnte = maxAnte;
            MaxPackSlotsPerAnte = maxPackSlotsPerAnte;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            // STAGE 1: Vectorized pre-filter - just detect Soul cards
            // We can't properly track soul joker sequences in vectorized mode
            // because different seeds have different Soul patterns
            VectorMask anySoulFound = VectorMask.NoBitsSet;

            var clauses = Clauses;
            int minAnte = MinAnte;
            int maxAnte = MaxAnte;
            var maxPackSlotsPerAnte = MaxPackSlotsPerAnte;

            // Walk through ALL antes looking for Soul cards
            // Check if ANY clause wants each ante
            for (int ante = minAnte; ante <= maxAnte; ante++)
            {
                bool anteWanted = false;
                foreach (var clause in clauses)
                {
                    if (ante < clause.WantedAntes.Length && clause.WantedAntes[ante])
                    {
                        anteWanted = true;
                        break;
                    }
                }

                if (!anteWanted)
                    continue;

                // Create pack streams for this ante
                var boosterPackStream = ctx.CreateBoosterPackStream(ante, ante > 1, false);
                var tarotStream = ctx.CreateArcanaPackTarotStream(ante, true, false);
                var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, true, false);
                bool tarotStreamInit = false,
                    spectralStreamInit = false;

                int maxPackSlot = maxPackSlotsPerAnte[ante]; // Dictionary always populated by CreateCriteria

                // Walk through each pack slot
                for (int packIndex = 0; packIndex < maxPackSlot; packIndex++)
                {
                    var pack = ctx.GetNextBoosterPack(ref boosterPackStream);

                    // Check if pack is Arcana type
                    VectorMask isArcana = VectorEnum256.Equals(
                        pack.GetPackType(),
                        MotelyBoosterPackType.Arcana
                    );
                    if (!isArcana.IsAllFalse())
                    {
                        if (!tarotStreamInit)
                        {
                            tarotStreamInit = true;
                            tarotStream = ctx.CreateArcanaPackTarotStream(ante, true);
                        }
                        var soulInArcana = ctx.GetNextArcanaPackHasTheSoul(
                            ref tarotStream,
                            MotelyBoosterPackSize.Mega
                        );
                        anySoulFound |= (isArcana & soulInArcana);
                    }

                    // Check if pack is Spectral type
                    VectorMask isSpectral = VectorEnum256.Equals(
                        pack.GetPackType(),
                        MotelyBoosterPackType.Spectral
                    );
                    if (!isSpectral.IsAllFalse())
                    {
                        if (!spectralStreamInit)
                        {
                            spectralStreamInit = true;
                            spectralStream = ctx.CreateSpectralPackSpectralStream(ante, true);
                        }
                        var soulInSpectral = ctx.GetNextSpectralPackHasTheSoul(
                            ref spectralStream,
                            MotelyBoosterPackSize.Mega
                        );
                        anySoulFound |= (isSpectral & soulInSpectral);
                    }
                }
            }

            // Pass seeds with Soul cards to individual validation
            // The individual validation will check ALL clauses against shared stream (prevents desync)
            return ctx.SearchIndividualSeeds(
                anySoulFound,
                (ref MotelySingleSearchContext singleCtx) =>
                {
                    // Use the shared multi-clause checking logic from MotelyJsonScoring
                    // This ensures all clauses check against the SAME stream walkthrough
                    return MotelyJsonScoring.CheckSoulJokerForSeed(
                        clauses,
                        ref singleCtx,
                        earlyExit: true
                    );
                }
            );
        }
    }
}
