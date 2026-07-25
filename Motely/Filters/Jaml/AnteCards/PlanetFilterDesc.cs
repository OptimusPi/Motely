using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

[JamlDiscriminator("planetCard", "planetCards",
    ValueEnum = typeof(MotelyPlanetCard), SourceConfigType = typeof(PlanetSourceConfig))]
public sealed class PlanetCardClause : IJamlClause, IAnteScopedClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public MotelyPlanetCard[] Planets { get; set; } = [];

    // null = no sources: in JAML → filter DefaultSources at CreateFilter/score (not parse).
    // applies. Any explicit block (even partial) is used verbatim — defaults never merge in.
    public PlanetSourceConfig? Sources { get; set; }
}

public struct PlanetCardFilterDesc(PlanetCardClause clause)
    : IMotelySeedFilterDesc<PlanetCardFilterDesc.PlanetCardFilter>,
      IJamlClauseDesc<PlanetCardClause>
{
    private readonly PlanetCardClause _clause = clause;

    /// <inheritdoc/>
    public static string[] Discriminators => ["planetCard", "planetCards"];

    /// <inheritdoc/>
    public static string[] ClauseKeys => ["min", "max", "score", "label", "ante", "antes", "sources"];

    /// <inheritdoc/>
    public static bool Set(PlanetCardClause clause, string key, IJamlValueReader value)
    {
        return false;
    }

    /// <inheritdoc/>
    public static bool SetDiscriminatorValue(PlanetCardClause clause, IJamlValueReader value)
    {
        if (!value.TryEnumArray<MotelyPlanetCard>(out var planets)) return false;
        clause.Planets = planets;
        return true;
    }

    /// <summary>
    /// Filter-layer default when Sources is null. Shop only; packs need explicit sources:.
    /// </summary>
    internal static readonly PlanetSourceConfig DefaultSources = new()
    {
        ShopItems = [0, 1, 2, 3, 4, 5, 6, 7],
    };

    public PlanetCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
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

        return new PlanetCardFilter(_clause, maxShopItem, maxBoosterPack);
    }

    public struct PlanetCardFilter(PlanetCardClause clause, int maxShopItem, int maxBoosterPack)
        : IMotelySeedFilter
    {
        private readonly PlanetCardClause _clause = clause;
        private readonly int _maxShopItem = maxShopItem;
        private readonly int _maxBoosterPack = maxBoosterPack;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Planets.Length > 0);
            var clause = _clause;
            int maxShopItem = _maxShopItem;
            int maxBoosterPack = _maxBoosterPack;
            int needed = clause.Min;
            Debug.Assert(needed > 0, "PlanetCardClause.Min must be > 0 — loader bug.");

            Vector256<int> matchCounts = Vector256<int>.Zero;
            var sources = clause.Sources ?? DefaultSources;
            var shopIndices = sources.ShopItems;
            var boosterPacks = sources.BoosterPacks;

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

                        VectorMask isPlanet = VectorEnum256.Equals(
                            item.TypeCategory,
                            MotelyItemTypeCategory.PlanetCard
                        );
                        VectorMask match = MatchPlanets(item, clause) & isPlanet;

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

                // ── Celestial packs SIMD ──
                if (boosterPacks.Length > 0)
                {
                    var packStream = ctx.CreateBoosterPackStream(ante);
                    var planetStream = ctx.CreateCelestialPackPlanetStream(ante);

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
                        VectorMask isCelestial = VectorEnum256.Equals(
                            packType,
                            MotelyBoosterPackType.Celestial
                        );
                        if (isCelestial.IsPartiallyTrue())
                        {
                            var contents = ctx.GetNextCelestialPackContents(
                                ref planetStream,
                                MotelyBoosterPackSize.Normal
                            );

                            if (isTarget)
                            {
                                for (int i = 0; i < contents.Length; i++)
                                {
                                    VectorMask match = MatchPlanets(contents[i], clause);
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
            }

            Vector256<int> comparison = Vector256.GreaterThan(
                matchCounts,
                Vector256.Subtract(Vector256.Create(needed), Vector256.Create(1))
            );
            return new VectorMask(MotelyVectorUtils.VectorizedComparisonToMask(comparison));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static VectorMask MatchPlanets(MotelyItemVector items, PlanetCardClause clause)
        {
            VectorMask mask = VectorMask.NoBitsSet;
            var itemTypes = items.Type;

            for (int i = 0; i < clause.Planets.Length; i++)
            {
                var targetType = (int)MotelyItemTypeCategory.PlanetCard | (int)clause.Planets[i];
                mask |= VectorEnum256.Equals(itemTypes, (MotelyItemType)targetType);
            }

            return mask;
        }
    }
}

/// <summary>
/// <c>sources:</c> block for <c>planetCard:</c>. Colocated with <see cref="PlanetCardFilterDesc"/> (T5).
/// </summary>
public sealed record PlanetSourceConfig
{
    public static readonly string[] SourceKeys = ["shopItems", "boosterPacks"];

    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
}
