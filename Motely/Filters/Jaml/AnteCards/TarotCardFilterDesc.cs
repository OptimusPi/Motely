using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

[JamlDiscriminator("tarotCard", "tarotCards",
    ValueEnum = typeof(MotelyTarotCard), SourceConfigType = typeof(TarotCardSourceConfig))]
public sealed class TarotCardClause : IJamlClause, IAnteScopedClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public MotelyTarotCard[] Tarots { get; set; } = [];

    // null = no sources: in JAML → filter DefaultSources at CreateFilter/score (not parse).
    public TarotCardSourceConfig? Sources { get; set; }
}

public struct TarotCardFilterDesc(TarotCardClause clause)
    : IMotelySeedFilterDesc<TarotCardFilterDesc.TarotCardFilter>,
      IJamlClauseDesc<TarotCardClause>
{
    private readonly TarotCardClause _clause = clause;

    /// <inheritdoc/>
    public static string[] Discriminators => ["tarotCard", "tarotCards"];

    /// <inheritdoc/>
    public static string[] ClauseKeys => ["min", "max", "score", "label", "ante", "antes", "sources"];

    /// <inheritdoc/>
    public static bool Set(TarotCardClause clause, string key, IJamlValueReader value)
    {
        return false;
    }

    /// <inheritdoc/>
    public static bool SetDiscriminatorValue(TarotCardClause clause, IJamlValueReader value)
    {
        if (!value.TryEnumArray<MotelyTarotCard>(out var tarots)) return false;
        clause.Tarots = tarots;
        return true;
    }

    /// <summary>
    /// Filter-layer default when Sources is null. Shop only; packs/specialty need explicit sources:.
    /// </summary>
    internal static readonly TarotCardSourceConfig DefaultSources = new()
    {
        ShopItems = [0, 1, 2, 3, 4, 5, 6, 7],
    };

    public TarotCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
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

        int maxEmperor = 0;
        for (int i = 0; i < sources.Emperor.Length; i++)
        {
            if (sources.Emperor[i] > maxEmperor)
                maxEmperor = sources.Emperor[i];
        }

        int maxPurpleSeal = 0;
        for (int i = 0; i < sources.PurpleSealOrEightBall.Length; i++)
        {
            if (sources.PurpleSealOrEightBall[i] > maxPurpleSeal)
                maxPurpleSeal = sources.PurpleSealOrEightBall[i];
        }

        return new TarotCardFilter(_clause, maxShopItem, maxBoosterPack, maxEmperor, maxPurpleSeal);
    }

    public struct TarotCardFilter(
        TarotCardClause clause,
        int maxShopItem,
        int maxBoosterPack,
        int maxEmperor,
        int maxPurpleSeal
    ) : IMotelySeedFilter
    {
        private readonly TarotCardClause _clause = clause;
        private readonly int _maxShopItem = maxShopItem;
        private readonly int _maxBoosterPack = maxBoosterPack;
        private readonly int _maxEmperor = maxEmperor;
        private readonly int _maxPurpleSeal = maxPurpleSeal;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Tarots.Length > 0);
            var clause = _clause;
            int maxShopItem = _maxShopItem;
            int maxBoosterPack = _maxBoosterPack;
            int maxEmperor = _maxEmperor;
            int maxPurpleSeal = _maxPurpleSeal;
            int needed = clause.Min;
            Debug.Assert(needed > 0, "TarotCardClause.Min must be > 0 — loader bug.");

            Vector256<int> matchCounts = Vector256<int>.Zero;
            var sources = clause.Sources ?? DefaultSources;
            var shopIndices = sources.ShopItems;
            var boosterPacks = sources.BoosterPacks;
            var emperorRolls = sources.Emperor;
            var sealRolls = sources.PurpleSealOrEightBall;

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

                        VectorMask isTarot = VectorEnum256.Equals(
                            item.TypeCategory,
                            MotelyItemTypeCategory.TarotCard
                        );
                        VectorMask match = MatchTarots(item, clause) & isTarot;

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

                // ── Arcana packs SIMD ──
                // Note: GetNextArcanaPackContents takes scalar MotelyBoosterPackSize.
                // Pack size varies per lane, so we process each size variant separately.
                if (boosterPacks.Length > 0)
                {
                    var packStream = ctx.CreateBoosterPackStream(ante);
                    var tarotStream = ctx.CreateArcanaPackTarotStream(ante);

                    // SIMD prefilter is intentionally over-permissive: iterating past ante 1's real
                    // pack count (4) yields phantom matches from the PRNG stream, but those are
                    // rejected in the scoring phase which re-verifies scalar per-ante.
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
                        VectorMask isArcana = VectorEnum256.Equals(
                            packType,
                            MotelyBoosterPackType.Arcana
                        );
                        if (isArcana.IsPartiallyTrue())
                        {
                            // Use Normal size (3 cards) as the baseline — all Arcana packs
                            // have at least 3 cards. Jumbo/Mega have 5.
                            var contents = ctx.GetNextArcanaPackContents(
                                ref tarotStream,
                                MotelyBoosterPackSize.Normal
                            );

                            if (isTarget)
                            {
                                for (int i = 0; i < contents.Length; i++)
                                {
                                    VectorMask match = MatchTarots(contents[i], clause);
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

                // ── Emperor SIMD ──
                if (emperorRolls.Length > 0)
                {
                    var emperorStream = ctx.CreateEmperorTarotStream(ante);

                    for (int roll = 0; roll <= maxEmperor; roll++)
                    {
                        var tarots = ctx.GetNextEmperorTarots(ref emperorStream);
                        bool isTarget = false;
                        for (int i = 0; i < emperorRolls.Length; i++)
                        {
                            if (emperorRolls[i] == roll)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        if (isTarget)
                        {
                            VectorMask match1 = MatchTarots(tarots[0], clause);
                            VectorMask match2 = MatchTarots(tarots[1], clause);

                            if (match1.IsPartiallyTrue())
                                matchCounts = Vector256.Add(
                                    matchCounts,
                                    Vector256.ConditionalSelect(
                                        MotelyVectorUtils.VectorMaskToConditionalSelectMask(match1),
                                        Vector256.Create(1),
                                        Vector256<int>.Zero
                                    )
                                );
                            if (match2.IsPartiallyTrue())
                                matchCounts = Vector256.Add(
                                    matchCounts,
                                    Vector256.ConditionalSelect(
                                        MotelyVectorUtils.VectorMaskToConditionalSelectMask(match2),
                                        Vector256.Create(1),
                                        Vector256<int>.Zero
                                    )
                                );
                        }
                    }
                }

                // ── Purple Seal SIMD ──
                if (sealRolls.Length > 0)
                {
                    var purpleSealStream = ctx.CreatePurpleSealTarotStream(ante);

                    for (int roll = 0; roll <= maxPurpleSeal; roll++)
                    {
                        var item = ctx.GetNextTarot(ref purpleSealStream);
                        bool isTarget = false;
                        for (int i = 0; i < sealRolls.Length; i++)
                        {
                            if (sealRolls[i] == roll)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        if (isTarget)
                        {
                            VectorMask match = MatchTarots(item, clause);
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
        internal static VectorMask MatchTarots(MotelyItemVector items, TarotCardClause clause)
        {
            VectorMask mask = VectorMask.NoBitsSet;
            var itemTypes = items.Type;

            for (int i = 0; i < clause.Tarots.Length; i++)
            {
                var targetType = (int)MotelyItemTypeCategory.TarotCard | (int)clause.Tarots[i];
                mask |= VectorEnum256.Equals(itemTypes, (MotelyItemType)targetType);
            }

            return mask;
        }
    }
}

/// <summary>
/// <c>sources:</c> block for <c>tarotCard:</c>. Colocated with <see cref="TarotCardFilterDesc"/> (T5).
/// </summary>
public sealed record TarotCardSourceConfig
{
    public static readonly string[] SourceKeys =
        ["shopItems", "boosterPacks", "emperor", "purpleSealOrEightBall", "charmTag"];

    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] Emperor { get; set; } = [];
    public int[] PurpleSealOrEightBall { get; set; } = [];

    /// <summary>
    /// When true, booster arcana scoring may consume the Charm-tag bonus pack (second weighted slot, no natural Arcana).
    /// </summary>
    public bool CharmTag { get; set; }
}
