using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using static Motely.MotelyVectorUtils;
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
            IsWildcard = _clause.IsWildcard,
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

            bool editionMatch = !clause.Edition.HasValue || legendaryJoker.Edition == clause.Edition.Value;
            bool jokerMatch = targetTypes.Length == 0;

            if (!jokerMatch)
            {
                for (int i = 0; i < targetTypes.Length; i++)
                {
                    if (legendaryJoker.Type == targetTypes[i])
                    {
                        jokerMatch = true;
                        break;
                    }
                }
            }

            // Wrong joker? Don't even bother checking packs.
            if (!editionMatch || !jokerMatch)
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

            var clause = _clause;
            var maxBoosterPack = _maxBoosterPack;
            var targetTypes = _targetTypes;
            int needed = clause.Min;
            Debug.Assert(needed > 0, "SoulJokerClause.Min must be > 0 — loader bug.");

            Vector256<int> candidateCounts = Vector256<int>.Zero;

            foreach (var ante in clause.Antes)
            {
                var soulStream = ctx.CreateSoulJokerStream(ante);
                var legendaryJoker = ctx.GetNextJoker(ref soulStream);
                VectorMask preMask = MatchLegendaryJokers(legendaryJoker);

                if (preMask.IsPartiallyTrue())
                {
                    candidateCounts = Vector256.Add(
                        candidateCounts,
                        Vector256.ConditionalSelect(
                            VectorMaskToConditionalSelectMask(preMask),
                            Vector256.Create(1),
                            Vector256<int>.Zero
                        )
                    );
                }
            }

            Vector256<int> minVec = Vector256.Create(needed);
            Vector256<int> comparison = Vector256.GreaterThan(
                candidateCounts,
                Vector256.Subtract(minVec, Vector256.Create(1))
            );
            VectorMask candidateMask = new(MotelyVectorUtils.VectorizedComparisonToMask(comparison));

            if (candidateMask.IsAllFalse())
                return candidateMask;

            return ctx.SearchIndividualSeeds(
                candidateMask,
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly VectorMask MatchLegendaryJokers(in MotelyItemVector item)
        {
            VectorMask jokerMatch;
            if (_clause.IsWildcard)
            {
                jokerMatch = VectorMask.AllBitsSet;
            }
            else
            {
                jokerMatch = VectorMask.NoBitsSet;
                for (int t = 0; t < _targetTypes.Length; t++)
                    jokerMatch |= VectorEnum256.Equals(item.Type, _targetTypes[t]);
            }

            if (_clause.Edition.HasValue)
                jokerMatch &= VectorEnum256.Equals(item.Edition, _clause.Edition.Value);

            return jokerMatch;
        }
    }
}
