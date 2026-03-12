using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Motely;

namespace Motely.Filters;

public sealed class LegendaryJokerClause : IJamlClause
{
    public string Label { get; init; } = "";
    public int Score { get; init; }
    public MotelyJoker[] Jokers { get; init; } = [];
    public bool IsWildcard { get; init; }
    public MotelyItemEdition? Edition { get; init; }
    public SoulJokerSourceConfig Sources { get; init; } = new();
    public int[] Antes { get; init; } = [];
    public int Min { get; init; } = 1;
}

public struct LegendaryJokerFilterDesc(LegendaryJokerClause clause)
    : IMotelySeedFilterDesc<LegendaryJokerFilterDesc.LegendaryJokerFilter>
{
    private readonly LegendaryJokerClause _clause = clause;

    public LegendaryJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        var boosterPacks = _clause.Sources.BoosterPacks;
        Debug.Assert(boosterPacks.Length > 0,
            "Legendary joker clause should have normalized default sources at config load time.");

        foreach (var ante in _clause.Antes)
        {
            ctx.CacheBoosterPackStream(ante);
        }

        int maxBoosterPack = 0;
        for (int i = 0; i < boosterPacks.Length; i++)
        {
            if (boosterPacks[i] > maxBoosterPack)
                maxBoosterPack = boosterPacks[i];
        }

        // Pre-compute target joker type (cold path)
        var jokerTypes = new MotelyItemType[_clause.Jokers.Length];
        for (int i = 0; i < _clause.Jokers.Length; i++)
            jokerTypes[i] = (MotelyItemType)(
                (int)MotelyItemTypeCategory.Joker | (int)_clause.Jokers[i]
            );

        var normalizedClause = new LegendaryJokerClause
        {
            Label = _clause.Label,
            Score = _clause.Score,
            Jokers = _clause.Jokers,
            Edition = _clause.Edition,
            Antes = _clause.Antes,
            Min = _clause.Min,
            Sources = new SoulJokerSourceConfig
            {
                ShopItems = _clause.Sources.ShopItems,
                BoosterPacks = boosterPacks,
                SoulCard = _clause.Sources.SoulCard,
            },
        };

        return new LegendaryJokerFilter(normalizedClause, maxBoosterPack, jokerTypes);
    }

    public struct LegendaryJokerFilter(
        LegendaryJokerClause clause,
        int maxBoosterPack,
        MotelyItemType[] targetTypes
    ) : IMotelySeedFilter
    {
        private readonly LegendaryJokerClause _clause = clause;
        private readonly int _maxBoosterPack = maxBoosterPack;
        private readonly MotelyItemType[] _targetTypes = targetTypes;

        /// <summary>
        /// Check if the soul joker in a given ante matches our target.
        /// Follows Trickeoglyph pattern: check the JOKER FIRST (cheap, deterministic),
        /// then check if The Soul actually appears in a pack (expensive).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckSoulJokerInAnte(
            int ante,
            LegendaryJokerClause clause,
            int maxBoosterPack,
            MotelyItemType[] targetTypes,
            ref MotelySingleSearchContext singleCtx
        )
        {
            var boosterPacks = clause.Sources.BoosterPacks;

            // ── STEP 1: Check joker FIRST (cheap!) ──
            // The soul joker stream output is deterministic per seed+ante,
            // regardless of whether The Soul actually appears in a pack.
            var soulStream = singleCtx.CreateSoulJokerStream(ante);
            var legendaryJoker = singleCtx.GetNextJoker(ref soulStream);

            bool jokerMatch;
            if (targetTypes.Length == 0)
            {
                // Wildcard: any legendary joker
                jokerMatch = !clause.Edition.HasValue || legendaryJoker.Edition == clause.Edition.Value;
            }
            else
            {
                jokerMatch = false;
                for (int i = 0; i < targetTypes.Length; i++)
                {
                    if (legendaryJoker.Type == targetTypes[i])
                    {
                        if (!clause.Edition.HasValue || legendaryJoker.Edition == clause.Edition.Value)
                        {
                            jokerMatch = true;
                            break;
                        }
                    }
                }
            }

            // Wrong joker? Don't even bother checking packs.
            if (!jokerMatch)
                return false;

            // ── STEP 2: Does The Soul actually appear in a pack? ──
            if (boosterPacks.Length == 0)
                return false;

            MotelySingleTarotStream tarotStream = default;
            MotelySingleSpectralStream spectralStream = default;
            bool tarotStreamInit = false;
            bool spectralStreamInit = false;
            var packStream = singleCtx.CreateBoosterPackStream(ante);

            for (int p = 0; p <= maxBoosterPack; p++)
            {
                MotelyBoosterPack pack = singleCtx.GetNextBoosterPack(ref packStream);

                bool isTarget = false;
                for (int i = 0; i < boosterPacks.Length; i++)
                {
                    if (boosterPacks[i] == p)
                    {
                        isTarget = true;
                        break;
                    }
                }

                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream = singleCtx.CreateArcanaPackTarotStream(ante, true);
                    }

                    if (
                        isTarget
                        && singleCtx.GetNextArcanaPackHasTheSoul(
                            ref tarotStream,
                            pack.GetPackSize()
                        )
                    )
                        return true;
                }

                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    if (!spectralStreamInit)
                    {
                        spectralStreamInit = true;
                        spectralStream = singleCtx.CreateSpectralPackSpectralStream(ante, true);
                    }

                    if (
                        isTarget
                        && singleCtx.GetNextSpectralPackHasTheSoul(
                            ref spectralStream,
                            pack.GetPackSize()
                        )
                    )
                        return true;
                }
            }

            return false;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.IsWildcard || _clause.Jokers.Length > 0);

            // Copy struct fields to locals — lambdas can't capture struct 'this'
            var clause = _clause;
            var maxBoosterPack = _maxBoosterPack;
            var targetTypes = _targetTypes;
            int needed = clause.Min;
            Debug.Assert(needed > 0, "SoulJokerClause.Min must be > 0 — loader bug.");

            return ctx.SearchIndividualSeeds(
                (ref MotelySingleSearchContext singleCtx) =>
                {
                    int matchCount = 0;

                    foreach (var ante in clause.Antes)
                    {
                        if (
                            CheckSoulJokerInAnte(
                                ante,
                                clause,
                                maxBoosterPack,
                                targetTypes,
                                ref singleCtx
                            )
                        )
                        {
                            matchCount++;
                            if (matchCount >= needed)
                                return true;
                        }
                    }

                    return false;
                }
            );
        }
    }
}
