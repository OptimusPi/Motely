using System.Diagnostics;
using System.Runtime.CompilerServices;
using Motely;
namespace Motely.Filters;

public sealed class LegendaryJokerClause : JamlClause
{
    public MotelyJoker[] Jokers { get; init; } = [];
    public bool IsWildcard { get; init; }
    public MotelyItemEdition? Edition { get; init; }
    public LegendaryJokerSourceConfig Sources { get; init; } = new();

    /// <summary>
    /// When true, match as soon as The Soul appears in a targeted arcana/Spectral pack (Tarot/Spectral
    /// card), without rolling the legendary joker. Use for "any" + soul-card-only searches.
    /// </summary>
    public bool SoulCardOnly { get; init; }

    /// <summary>
    /// Extra soul-stream edition reads per ante for the fast edition vector prefilter (0 = use
    /// <see cref="LegendaryJokerSourceConfig.BoosterPacks"/> length). Raise when rare multi-soul antes
    /// could otherwise false-negative the prefilter.
    /// </summary>
    public int SoulEditionRolls { get; init; }

    public override int EstimatedCost => 5 + MaxAnte;
    public override string Describe() =>
        IsWildcard ? "legendaryJoker Any"
                   : $"legendaryJoker {string.Join(", ", System.Array.ConvertAll(Jokers, static j => j.ToString()))}";
    public override IMotelySeedFilterDesc CreateDesc() => new LegendaryJokerFilterDesc(this);
}

/// <summary>
/// <see cref="LegendaryJokerPipelineKind.Standard"/> runs the combined edition vector prefilter + soul path.
/// <see cref="LegendaryJokerPipelineKind.FullPathOnly"/> is the pack/soul matcher only (used after
/// <see cref="LegendarySoulEditionFilterDesc"/> in the must pipeline).
/// </summary>
public enum LegendaryJokerPipelineKind
{
    Standard,
    FullPathOnly,
}

public struct LegendaryJokerFilterDesc(
    LegendaryJokerClause clause,
    LegendaryJokerPipelineKind pipeline = LegendaryJokerPipelineKind.Standard
) : IMotelySeedFilterDesc<LegendaryJokerFilterDesc.LegendaryJokerFilter>
{
    private readonly LegendaryJokerClause _clause = clause;
    private readonly LegendaryJokerPipelineKind _pipeline = pipeline;

    public LegendaryJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        var src = _clause.Sources.NormalizeLegendaryJokerBoostersIfEmpty();

        int maxBoosterPack = src.MaxReferencedBoosterSlot();

        var normalizedClause = new LegendaryJokerClause
        {
            Label = _clause.Label,
            Score = _clause.Score,
            Jokers = _clause.Jokers,
            IsWildcard = _clause.IsWildcard,
            Edition = _clause.Edition,
            Antes = _clause.Antes,
            Min = _clause.Min,
            SoulCardOnly = _clause.SoulCardOnly,
            SoulEditionRolls = _clause.SoulEditionRolls,
            Sources = new LegendaryJokerSourceConfig
            {
                ShopItems = src.ShopItems,
                BoosterPacks = src.BoosterPacks,
                ArcanaPacks = src.ArcanaPacks,
                SpectralPacks = src.SpectralPacks,
                SoulCard = src.SoulCard,
                RequireMegaPack = src.RequireMegaPack,
            },
        };

        foreach (var ante in normalizedClause.Antes)
            ctx.CacheBoosterPackStream(ante, force: true);

        if (
            _pipeline == LegendaryJokerPipelineKind.Standard
            && normalizedClause.Edition.HasValue
            && normalizedClause.Min == 1
        )
        {
            foreach (var ante in normalizedClause.Antes)
                ctx.CacheLegendaryJokerStream(
                    ante,
                    MotelyJokerFixedRarityStreamFlags.ExcludeJokerType
                        | MotelyJokerFixedRarityStreamFlags.ExcludeStickers,
                    force: true
                );
        }

        return new LegendaryJokerFilter(normalizedClause, maxBoosterPack, _pipeline);
    }

    public struct LegendaryJokerFilter(
        LegendaryJokerClause clause,
        int maxBoosterPack,
        LegendaryJokerPipelineKind pipeline
    ) : IMotelySeedFilter
    {
        private readonly LegendaryJokerClause _clause = clause;
        private readonly int _maxBoosterPack = maxBoosterPack;
        private readonly LegendaryJokerPipelineKind _pipeline = pipeline;

        [MethodImpl(
            MethodImplOptions.AggressiveInlining
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(
                _clause.SoulCardOnly || _clause.IsWildcard || _clause.Jokers.Length > 0
            );

            var clause = _clause;
            var maxBoosterPack = _maxBoosterPack;
            var pipeline = _pipeline;
            int needed = clause.Min;
            Debug.Assert(needed > 0, "LegendaryJokerClause.Min must be > 0 — loader bug.");

            // Do not prefilter on "soul stream before packs" — that order is invalid for legendary
            // souls (see LegendarySoulMatcher). Edition-only vector prefilter (Min==1) matches
            // Negative + soul joker SIMD prefilter (see NegativeLegendaryJokerSimdFilterDesc);
            // pack/soul path runs as an additional filter so batches buffer before scalar work.
            // we do not drop seeds where the first soul fails edition but a later soul matches.
            uint laneMask = 0;
            for (int lane = 0; lane < MotelyGlobals.MaxVectorWidth; lane++)
            {
                if (ctx.IsLaneValid(lane))
                    laneMask |= 1u << lane;
            }

            VectorMask candidateMask = new VectorMask(laneMask);

            if (
                pipeline == LegendaryJokerPipelineKind.Standard
                && clause.Edition.HasValue
                && clause.Min == 1
            )
            {
                candidateMask = LegendarySoulEditionPrefilter.Apply(ref ctx, clause, laneMask);
                if (candidateMask.IsAllFalse())
                    return candidateMask;
            }

            return ctx.SearchIndividualSeeds(
                candidateMask,
                (ref MotelySingleSearchContext singleCtx) =>
                {
                    int matchCount = 0;

                    foreach (var ante in clause.Antes)
                    {
                        if (
                            LegendarySoulMatcher.MatchAnte(
                                ref singleCtx,
                                ante,
                                clause,
                                maxBoosterPack
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
