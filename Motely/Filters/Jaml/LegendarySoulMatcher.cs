using System.Runtime.CompilerServices;
using Motely;

namespace Motely.Filters;

/// <summary>
/// Soul / legendary checks must follow the same pack order and RNG order as
/// <see cref="PerkeoObservatoryFilterDesc"/> (pack stream with generated-first for ante 1,
/// then The Soul in arcana/spectral, then <see cref="MotelySingleSearchContext.GetNextJoker"/>).
/// The older "read soul stream before packs" path mis-aligned streams and matched nothing.
/// </summary>
internal static class LegendarySoulMatcher
{
    internal static bool MatchAnte(
        ref MotelySingleSearchContext ctx,
        int ante,
        LegendaryJokerClause clause,
        int maxBoosterPack
    )
    {
        var src = clause.Sources.NormalizeSoulJokerBoostersIfEmpty();

        // Default CreateBoosterPackStream(ante) uses generatedFirstPack = (ante > 1), so ante 1
        // prepends a synthetic Buffoon — indices and pack types no longer match PerkeoObservatory.
        var packStream = ctx.CreateBoosterPackStream(ante, true, false);

        MotelySingleTarotStream tarotStream = default;
        MotelySingleSpectralStream spectralStream = default;
        bool tarotInit = false;
        bool spectralInit = false;
        MotelySingleJokerFixedRarityStream soulStream = default;
        bool soulStreamInited = false;

        for (int p = 0; p <= maxBoosterPack; p++)
        {
            var pack = ctx.GetNextBoosterPack(ref packStream);

            bool isTarget = IsBoosterSlotTargetForLegendary(src, p, pack)
                && (!src.RequireMegaPack || pack.GetPackSize() == MotelyBoosterPackSize.Mega);

            if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
            {
                if (!tarotInit)
                {
                    tarotInit = true;
                    tarotStream = ctx.CreateArcanaPackTarotStream(ante, true);
                }

                bool hasSoul = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize());

                if (!isTarget || !hasSoul)
                    continue;

                if (clause.SoulCardOnly)
                    return true;

                if (!soulStreamInited)
                {
                    soulStream = ctx.CreateSoulJokerStream(ante);
                    soulStreamInited = true;
                }

                var legendaryJoker = ctx.GetNextJoker(ref soulStream);
                if (LegendaryJokerMatchesFull(clause, legendaryJoker))
                    return true;
            }
            else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
            {
                if (!spectralInit)
                {
                    spectralInit = true;
                    // PerkeoObservatory: ante 1 spectral uses soulOnly false; ante 2+ uses true.
                    spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: ante != 1);
                }

                bool hasSoul = ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize());

                if (!isTarget || !hasSoul)
                    continue;

                if (clause.SoulCardOnly)
                    return true;

                if (!soulStreamInited)
                {
                    soulStream = ctx.CreateSoulJokerStream(ante);
                    soulStreamInited = true;
                }

                var legendaryJoker = ctx.GetNextJoker(ref soulStream);
                if (LegendaryJokerMatchesFull(clause, legendaryJoker))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Soul-card-only path: same pack walk / slot rules as <see cref="MatchAnte"/>, returns on first The Soul in a targeted arcana/spectral pack.
    /// </summary>
    internal static bool MatchAnteShopPackHasSoulOnly(
        ref MotelySingleSearchContext ctx,
        int ante,
        SoulJokerSourceConfig src,
        int maxBoosterPack
    )
    {
        var packStream = ctx.CreateBoosterPackStream(ante, true, false);

        MotelySingleTarotStream tarotStream = default;
        MotelySingleSpectralStream spectralStream = default;
        bool tarotInit = false;
        bool spectralInit = false;

        for (int p = 0; p <= maxBoosterPack; p++)
        {
            var pack = ctx.GetNextBoosterPack(ref packStream);

            bool isTarget = IsBoosterSlotTargetForLegendary(src, p, pack)
                && (!src.RequireMegaPack || pack.GetPackSize() == MotelyBoosterPackSize.Mega);

            if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
            {
                if (!tarotInit)
                {
                    tarotInit = true;
                    tarotStream = ctx.CreateArcanaPackTarotStream(ante, true);
                }

                bool hasSoul = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize());
                if (hasSoul && isTarget)
                    return true;
            }
            else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
            {
                if (!spectralInit)
                {
                    spectralInit = true;
                    spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: ante != 1);
                }

                bool hasSoul = ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize());
                if (hasSoul && isTarget)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Split mode (non-empty arcana and/or spectral slot lists): only those paths count.
    /// Legacy mode: <see cref="SoulJokerSourceConfig.BoosterPacks"/> — slot matches regardless of rolled pack type
    /// (arcana/spectral branches still gate The Soul).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsBoosterSlotTargetForLegendary(
        SoulJokerSourceConfig src,
        int p,
        MotelyBoosterPack pack
    )
    {
        bool split =
            src.ArcanaBoosterPacks.Length > 0 || src.SpectralBoosterPacks.Length > 0;
        if (!split)
        {
            for (int i = 0; i < src.BoosterPacks.Length; i++)
            {
                if (src.BoosterPacks[i] == p)
                    return true;
            }

            return false;
        }

        var type = pack.GetPackType();
        if (type == MotelyBoosterPackType.Arcana)
        {
            for (int i = 0; i < src.ArcanaBoosterPacks.Length; i++)
            {
                if (src.ArcanaBoosterPacks[i] == p)
                    return true;
            }

            return false;
        }

        if (type == MotelyBoosterPackType.Spectral)
        {
            for (int i = 0; i < src.SpectralBoosterPacks.Length; i++)
            {
                if (src.SpectralBoosterPacks[i] == p)
                    return true;
            }

            return false;
        }

        return false;
    }

    /// <summary>Whether <paramref name="ty"/> matches one of the clause's legendary jokers (no allocation).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TypeMatchesLegendary(LegendaryJokerClause clause, MotelyItemType ty)
    {
        for (int i = 0; i < clause.Jokers.Length; i++)
        {
            if (ty == (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)clause.Jokers[i]))
                return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LegendaryJokerMatchesFull(
        LegendaryJokerClause clause,
        Motely.MotelyItem joker
    )
    {
        if (clause.IsWildcard)
            return !clause.Edition.HasValue || joker.Edition == clause.Edition.Value;

        if (clause.Jokers.Length == 0)
            return !clause.Edition.HasValue || joker.Edition == clause.Edition.Value;

        if (!TypeMatchesLegendary(clause, joker.Type))
            return false;
        return !clause.Edition.HasValue || joker.Edition == clause.Edition.Value;
    }
}
