using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
namespace Motely.Filters;

public sealed class SpectralCardClause : IJamlClause
{
    public string Label { get; init; } = "";
    public int Score { get; init; }
    public required MotelySpectralCard[] Spectrals { get; init; }
    public SpectralCardSourceConfig Sources { get; init; } = new();
    public int[] Antes { get; init; } = [];
    public int Min { get; init; } = 1;
    public int? Max { get; init; }
}

public struct SpectralCardFilterDesc(SpectralCardClause clause)
    : IMotelySeedFilterDesc<SpectralCardFilterDesc.SpectralCardFilter>
{
    private readonly SpectralCardClause _clause = clause;

    public SpectralCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var ante in _clause.Antes)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }

        int maxShopItem = 0;
        for (int i = 0; i < _clause.Sources.ShopItems.Length; i++)
        {
            if (_clause.Sources.ShopItems[i] > maxShopItem)
                maxShopItem = _clause.Sources.ShopItems[i];
        }

        int maxBoosterPack = 0;
        for (int i = 0; i < _clause.Sources.BoosterPacks.Length; i++)
        {
            if (_clause.Sources.BoosterPacks[i] > maxBoosterPack)
                maxBoosterPack = _clause.Sources.BoosterPacks[i];
        }

        int maxSixthSense = 0;
        for (int i = 0; i < _clause.Sources.SixthSense.Length; i++)
        {
            if (_clause.Sources.SixthSense[i] > maxSixthSense)
                maxSixthSense = _clause.Sources.SixthSense[i];
        }

        int maxSeance = 0;
        for (int i = 0; i < _clause.Sources.Seance.Length; i++)
        {
            if (_clause.Sources.Seance[i] > maxSeance)
                maxSeance = _clause.Sources.Seance[i];
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

        [MethodImpl(
            MethodImplOptions.AggressiveInlining
        )]
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
            var shopIndices = clause.Sources.ShopItems;
            var boosterPacks = clause.Sources.BoosterPacks;
            var sixthSenseRolls = clause.Sources.SixthSense;
            var seanceRolls = clause.Sources.Seance;

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
