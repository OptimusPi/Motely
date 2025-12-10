using System.Runtime.CompilerServices;

namespace Motely.Filters;

/// <summary>
/// Fully vectorized soul joker filter using two-stage approach:
/// 1. Pre-filter: Fast vectorized joker matching
/// 2. Verify: Vectorized Soul card verification in packs
/// </summary>
public readonly struct MotelyJsonSoulJokerFilterDesc(MotelyJsonSoulJokerFilterCriteria criteria)
    : IMotelySeedFilterDesc<MotelyJsonSoulJokerFilterDesc.MotelyJsonSoulJokerFilter>
{
    private readonly MotelyJsonSoulJokerFilterCriteria _criteria = criteria;

    public MotelyJsonSoulJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // SINGLE-CLAUSE MODEL: Enforce exactly ONE clause per filter
        if (_criteria.Clauses.Count != 1)
            throw new ArgumentException($"MotelyJsonSoulJokerFilter expects exactly 1 clause, got {_criteria.Clauses.Count}");

        // Use pre-calculated values from criteria
        int minAnte = _criteria.MinAnte;
        int maxAnte = _criteria.MaxAnte;

        // Cache all streams we'll need for BOTH vectorized and individual checks
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            // For vectorized pre-filter
            ctx.CacheSoulJokerStream(ante);
        }

        return new MotelyJsonSoulJokerFilter(
            _criteria.Clauses[0],
            minAnte,
            maxAnte,
            _criteria.MaxPackSlotsPerAnte
        );
    }

    public struct MotelyJsonSoulJokerFilter(
        MotelyJsonSoulJokerFilterClause clause,
        int minAnte,
        int maxAnte,
        Dictionary<int, int> maxPackSlotsPerAnte
    ) : IMotelySeedFilter
    {
        private readonly MotelyJsonSoulJokerFilterClause Clause = clause;
        private readonly int MinAnte = minAnte;
        private readonly int MaxAnte = maxAnte;
        private readonly Dictionary<int, int> MaxPackSlotsPerAnte = maxPackSlotsPerAnte;
        private readonly int _minThreshold = clause.Min ?? 1; // Pre-calculate ONCE!

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            // STAGE 1: Vectorized pre-filter - just detect Soul cards
            // We can't properly track soul joker sequences in vectorized mode
            // because different seeds have different Soul patterns
            VectorMask anySoulFound = VectorMask.NoBitsSet;

            var clause = Clause; // SINGLE clause
            int minAnte = MinAnte;
            int maxAnte = MaxAnte;
            var maxPackSlotsPerAnte = MaxPackSlotsPerAnte;

            // Walk through ALL antes looking for Soul cards
            for (int ante = minAnte; ante <= maxAnte; ante++)
            {
                // Check if THIS clause wants this ante
                bool anteWanted = ante < clause.WantedAntes.Length && clause.WantedAntes[ante];

                if (!anteWanted)
                    continue;

                // Create pack streams for this ante
                var boosterPackStream = ctx.CreateBoosterPackStream(ante, ante > 1, false);
                var tarotStream = ctx.CreateArcanaPackTarotStream(ante, false);
                var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, false);
                bool tarotStreamInit = false,
                    spectralStreamInit = false;

                int maxPackSlot = maxPackSlotsPerAnte[ante];  // Dictionary always populated by CreateCriteria

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
            // The individual validation will check the specific joker requirements
            int minThreshold = _minThreshold; // Use pre-computed value
            return ctx.SearchIndividualSeeds(
                anySoulFound,
                (ref MotelySingleSearchContext singleCtx) =>
                {
                    // Track count for SINGLE clause
                    int clauseCount = 0;

                    // Soul joker has TWO components with different ante-dependency behavior:
                    // 1. Face/Type (Perkeo, Canio, etc.) - NOT ante-dependent (same PRNG sequence for entire seed)
                    // 2. Edition (Negative, Polychrome, etc.) - IS ante-dependent (different per ante)
                    //
                    // Solution: Use TWO separate streams:
                    // - globalSoulFaceStream: Created once, reused across ALL antes, checks ONLY face/type
                    // - soulEditionStream: Created fresh per ante, checks ONLY edition
                    var globalSoulFaceStream = singleCtx.CreateSoulJokerStream(1);

                    // Walk through ALL antes sequentially
                    for (int ante = minAnte; ante <= maxAnte; ante++)
                    {
                        // Create per-ante edition stream for edition checks (ante-dependent)
                        var soulEditionStream = singleCtx.CreateSoulJokerStream(ante);

                        var boosterPackStream = singleCtx.CreateBoosterPackStream(
                            ante,
                            ante > 1,
                            false
                        );
                        var tarotStream = singleCtx.CreateArcanaPackTarotStream(ante, false);
                        var spectralStream = singleCtx.CreateSpectralPackSpectralStream(
                            ante,
                            false
                        );
                        bool tarotStreamInit = false,
                            spectralStreamInit = false;

                        int maxPackSlot = maxPackSlotsPerAnte.ContainsKey(ante)
                            ? maxPackSlotsPerAnte[ante]
                            : 3;
                        for (int packIndex = 0; packIndex < maxPackSlot; packIndex++)
                        {
                            var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);

                            bool hasSoul = false;
                            if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                            {
                                if (!tarotStreamInit)
                                {
                                    tarotStreamInit = true;
                                    tarotStream = singleCtx.CreateArcanaPackTarotStream(ante, true);
                                }
                                hasSoul = singleCtx.GetNextArcanaPackHasTheSoul(
                                    ref tarotStream,
                                    pack.GetPackSize()
                                );
                            }
                            else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                            {
                                if (!spectralStreamInit)
                                {
                                    spectralStreamInit = true;
                                    spectralStream = singleCtx.CreateSpectralPackSpectralStream(
                                        ante,
                                        true
                                    );
                                }
                                hasSoul = singleCtx.GetNextSpectralPackHasTheSoul(
                                    ref spectralStream,
                                    pack.GetPackSize()
                                );
                            }

                            // If Soul found, get next joker from BOTH streams
                            if (hasSoul)
                            {
                                // Consume from BOTH streams:
                                // - Face stream for type matching (NOT ante-dependent)
                                // - Edition stream for edition matching (IS ante-dependent)
                                var soulJokerFace = singleCtx.GetNextJoker(
                                    ref globalSoulFaceStream
                                );
                                var soulJokerEdition = singleCtx.GetNextJoker(
                                    ref soulEditionStream
                                );

                                // Check this joker against THE clause
                                // Check if this ante is wanted
                                if (
                                    ante < clause.WantedAntes.Length
                                    && clause.WantedAntes[ante]
                                )
                                {
                                    // Check pack slot if specified
                                    if (
                                        packIndex < clause.WantedPackSlots.Length
                                        && clause.WantedPackSlots[packIndex]
                                    )
                                    {
                                        // Check mega requirement
                                        if (
                                            !clause.RequireMega
                                            || pack.GetPackSize() == MotelyBoosterPackSize.Mega
                                        )
                                        {
                                            // Check joker type using FACE stream (not ante-dependent)
                                            bool typeMatches = true;
                                            if (!clause.IsWildcard)
                                            {
                                                if (
                                                    clause.JokerTypes != null
                                                    && clause.JokerTypes.Count > 0
                                                )
                                                {
                                                    // Multiple types specified - match ANY of them (OR logic)
                                                    typeMatches = false;
                                                    foreach (var jokerType in clause.JokerTypes)
                                                    {
                                                        var expectedType = (MotelyItemType)(
                                                            (int)MotelyItemTypeCategory.Joker
                                                            | (int)jokerType
                                                        );
                                                        if (soulJokerFace.Type == expectedType)
                                                        {
                                                            typeMatches = true;
                                                            break;
                                                        }
                                                    }
                                                }
                                                else if (clause.JokerType.HasValue)
                                                {
                                                    // Single type specified
                                                    var expectedType = (MotelyItemType)(
                                                        (int)MotelyItemTypeCategory.Joker
                                                        | (int)clause.JokerType.Value
                                                    );
                                                    typeMatches = (soulJokerFace.Type == expectedType);
                                                }
                                            }
                                            // If IsWildcard is true (e.g., "Any"), typeMatches stays true

                                            if (typeMatches)
                                            {
                                                // Check edition using EDITION stream (ante-dependent)
                                                if (
                                                    !clause.EditionEnum.HasValue
                                                    || soulJokerEdition.Edition == clause.EditionEnum.Value
                                                )
                                                {
                                                    // This joker matches the clause!
                                                    clauseCount++;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Check if the clause met its Min threshold (pre-calculated value!)
                    return clauseCount >= minThreshold;
                }
            );
        }
    }
}
