using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

[JamlDiscriminator("spectralCard", "spectralCards",
    ValueEnum = typeof(MotelySpectralCard), SourceConfigType = typeof(SpectralCardSourceConfig))]
public sealed class SpectralCardClause : IJamlClause, IAnteScopedClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public MotelySpectralCard[] Spectrals { get; set; } = [];

    // null = no sources: in JAML → filter DefaultSources at CreateFilter/score (not parse).
    public SpectralCardSourceConfig? Sources { get; set; }
}

public struct SpectralCardFilterDesc(SpectralCardClause clause)
    : IMotelySeedFilterDesc<SpectralCardFilterDesc.SpectralCardFilter>,
      IJamlClauseDesc<SpectralCardClause>
{
    private readonly SpectralCardClause _clause = clause;

    /// <inheritdoc/>
    public static string[] Discriminators => ["spectralCard", "spectralCards"];

    /// <inheritdoc/>
    public static string[] ClauseKeys => ["min", "max", "score", "label", "ante", "antes", "sources"];

    /// <inheritdoc/>
    public static bool Set(SpectralCardClause clause, string key, IJamlValueReader value)
    {
        return false;
    }

    /// <inheritdoc/>
    public static bool SetDiscriminatorValue(SpectralCardClause clause, IJamlValueReader value)
    {
        if (!value.TryEnumArray<MotelySpectralCard>(out var spectrals)) return false;
        clause.Spectrals = spectrals;
        return true;
    }

    /// <summary>
    /// Filter-layer default when Sources is null. Shop only; packs/specialty need explicit sources:.
    /// </summary>
    internal static readonly SpectralCardSourceConfig DefaultSources = new()
    {
        ShopItems = [0, 1, 2, 3, 4, 5, 6, 7],
    };

    public SpectralCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        var sources = _clause.Sources ?? DefaultSources;

        foreach (var ante in _clause.Antes)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }

        int maxShopItem = 0;
        for (int i = 0; i < sources.ShopItems.Length; i++)
        {
            if (sources.ShopItems[i] > maxShopItem)
                maxShopItem = sources.ShopItems[i];
        }

        int maxBoosterPack = 0;
        for (int i = 0; i < sources.BoosterPacks.Length; i++)
        {
            if (sources.BoosterPacks[i] > maxBoosterPack)
                maxBoosterPack = sources.BoosterPacks[i];
        }

        int maxSixthSense = 0;
        for (int i = 0; i < sources.SixthSense.Length; i++)
        {
            if (sources.SixthSense[i] > maxSixthSense)
                maxSixthSense = sources.SixthSense[i];
        }

        int maxSeance = 0;
        for (int i = 0; i < sources.Seance.Length; i++)
        {
            if (sources.Seance[i] > maxSeance)
                maxSeance = sources.Seance[i];
        }

        return new SpectralCardFilter(
            _clause,
            maxShopItem,
            maxBoosterPack,
            maxSixthSense,
            maxSeance
        );
    }

    public struct SpectralCardFilter(
        SpectralCardClause clause,
        int maxShopItem,
        int maxBoosterPack,
        int maxSixthSense,
        int maxSeance
    ) : IMotelySeedFilter
    {
        private readonly SpectralCardClause _clause = clause;
        private readonly int _maxShopItem = maxShopItem;
        private readonly int _maxBoosterPack = maxBoosterPack;
        private readonly int _maxSixthSense = maxSixthSense;
        private readonly int _maxSeance = maxSeance;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Spectrals.Length > 0);
            var clause = _clause;
            int maxShopItem = _maxShopItem;
            int maxBoosterPack = _maxBoosterPack;
            int maxSixthSense = _maxSixthSense;
            int maxSeance = _maxSeance;
            int needed = clause.Min;
            Debug.Assert(needed > 0, "SpectralCardClause.Min must be > 0 — loader bug.");

            Vector256<int> matchCounts = Vector256<int>.Zero;
            var sources = clause.Sources ?? DefaultSources;
            if (sources.RequireMegaPack)
                return ctx.SearchIndividualSeeds(
                    (MotelySingleSearchContext single) =>
                        JamlScoring.CountSpectralCardOccurrencesForFilter(ref single, clause)
                        >= needed
                            ? 1
                            : 0
                );

            var shopIndices = sources.ShopItems;
            var boosterPacks = sources.BoosterPacks;
            var sixthSenseRolls = sources.SixthSense;
            var seanceRolls = sources.Seance;

            foreach (var ante in clause.Antes)
            {
                // ── Shop items SIMD ──
                if (shopIndices.Length > 0)
                {
                    var shopStream = ctx.CreateShopItemStream(ante);

                    for (int slot = 0; slot <= maxShopItem; slot++)
                    {
                        var item = ctx.GetNextShopItem(ref shopStream);
                        bool isTarget = false;
                        for (int i = 0; i < shopIndices.Length; i++)
                        {
                            if (shopIndices[i] == slot)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        if (!isTarget)
                            continue;

                        VectorMask isSpectral = VectorEnum256.Equals(
                            item.TypeCategory,
                            MotelyItemTypeCategory.SpectralCard
                        );
                        VectorMask match = MatchSpectrals(item, clause) & isSpectral;

                        if (match.IsPartiallyTrue())
                        {
                            matchCounts = Vector256.Add(
                                matchCounts,
                                Vector256.ConditionalSelect(
                                    MotelyVectorUtils.VectorMaskToConditionalSelectMask(match),
                                    Vector256.Create(1),
                                    Vector256<int>.Zero
                                )
                            );
                        }
                    }
                }

                // ── Spectral packs SIMD ──
                // Note: GetNextSpectralPackContents takes scalar MotelyBoosterPackSize.
                // Pack size varies per lane, so we use Normal as baseline.
                if (boosterPacks.Length > 0)
                {
                    var packStream = ctx.CreateBoosterPackStream(ante);
                    var spectralStream = ctx.CreateSpectralPackSpectralStream(ante);

                    for (int p = 0; p <= maxBoosterPack; p++)
                    {
                        var pack = ctx.GetNextBoosterPack(ref packStream);
                        bool isTarget = false;
                        for (int i = 0; i < boosterPacks.Length; i++)
                        {
                            if (boosterPacks[i] == p)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        var packType = pack.GetPackType();
                        VectorMask isSpectral = VectorEnum256.Equals(
                            packType,
                            MotelyBoosterPackType.Spectral
                        );
                        if (isSpectral.IsPartiallyTrue())
                        {
                            // Spectral Normal = 2 cards, Jumbo/Mega = 4.
                            // Use Normal as baseline.
                            var contents = ctx.GetNextSpectralPackContents(
                                ref spectralStream,
                                MotelyBoosterPackSize.Normal
                            );

                            if (isTarget)
                            {
                                for (int i = 0; i < contents.Length; i++)
                                {
                                    VectorMask match = MatchSpectrals(contents[i], clause);
                                    if (match.IsPartiallyTrue())
                                    {
                                        matchCounts = Vector256.Add(
                                            matchCounts,
                                            Vector256.ConditionalSelect(
                                                MotelyVectorUtils.VectorMaskToConditionalSelectMask(
                                                    match
                                                ),
                                                Vector256.Create(1),
                                                Vector256<int>.Zero
                                            )
                                        );
                                    }
                                }
                            }
                        }
                    }
                }

                // ── Sixth Sense SIMD ──
                if (sixthSenseRolls.Length > 0)
                {
                    var sixthSenseStream = ctx.CreateSixthSenseSpectralStream(ante);

                    for (int roll = 0; roll <= maxSixthSense; roll++)
                    {
                        var item = ctx.GetNextSpectral(ref sixthSenseStream);
                        bool isTarget = false;
                        for (int i = 0; i < sixthSenseRolls.Length; i++)
                        {
                            if (sixthSenseRolls[i] == roll)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        if (isTarget)
                        {
                            VectorMask match = MatchSpectrals(item, clause);
                            if (match.IsPartiallyTrue())
                            {
                                matchCounts = Vector256.Add(
                                    matchCounts,
                                    Vector256.ConditionalSelect(
                                        MotelyVectorUtils.VectorMaskToConditionalSelectMask(match),
                                        Vector256.Create(1),
                                        Vector256<int>.Zero
                                    )
                                );
                            }
                        }
                    }
                }

                // ── Seance SIMD ──
                if (seanceRolls.Length > 0)
                {
                    var seanceStream = ctx.CreateSeanceSpectralStream(ante);

                    for (int roll = 0; roll <= maxSeance; roll++)
                    {
                        var item = ctx.GetNextSpectral(ref seanceStream);
                        bool isTarget = false;
                        for (int i = 0; i < seanceRolls.Length; i++)
                        {
                            if (seanceRolls[i] == roll)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        if (isTarget)
                        {
                            VectorMask match = MatchSpectrals(item, clause);
                            if (match.IsPartiallyTrue())
                            {
                                matchCounts = Vector256.Add(
                                    matchCounts,
                                    Vector256.ConditionalSelect(
                                        MotelyVectorUtils.VectorMaskToConditionalSelectMask(match),
                                        Vector256.Create(1),
                                        Vector256<int>.Zero
                                    )
                                );
                            }
                        }
                    }
                }
            }

            Vector256<int> comparison = Vector256.GreaterThan(
                matchCounts,
                Vector256.Subtract(Vector256.Create(needed), Vector256.Create(1))
            );
            return new VectorMask(MotelyVectorUtils.VectorizedComparisonToMask(comparison));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static VectorMask MatchSpectrals(MotelyItemVector items, SpectralCardClause clause)
        {
            VectorMask mask = VectorMask.NoBitsSet;
            var itemTypes = items.Type;

            for (int i = 0; i < clause.Spectrals.Length; i++)
            {
                var targetType =
                    (int)MotelyItemTypeCategory.SpectralCard | (int)clause.Spectrals[i];
                mask |= VectorEnum256.Equals(itemTypes, (MotelyItemType)targetType);
            }

            return mask;
        }
    }
}

/// <summary>
/// <c>sources:</c> block for <c>spectralCard:</c>. Colocated with <see cref="SpectralCardFilterDesc"/> (T5).
/// </summary>
public sealed record SpectralCardSourceConfig
{
    /// <summary>requireMega/requireMegaPack: both real aliases for RequireMegaPack below.</summary>
    public static readonly string[] SourceKeys =
        ["shopItems", "boosterPacks", "sixthSense", "seance", "etherealTag", "requireMega", "requireMegaPack"];

    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] SixthSense { get; set; } = [];
    public int[] Seance { get; set; } = [];
    public bool RequireMegaPack { get; set; }

    /// <summary>
    /// When true, booster Spectral scoring may consume the Ethereal-tag bonus pack (second weighted slot, no natural Spectral).
    /// </summary>
    public bool EtherealTag { get; set; }

    // TODO: OmenGlobe — voucher that allows Spectral cards to appear in Arcana packs.
    // This is voucher-state-gated AND changes the Arcana pack PRNG path (not a simple slot array).
    // Much more complex than other sources — needs voucher state tracking + pack stream branching.
    // Do not implement naively.
}
