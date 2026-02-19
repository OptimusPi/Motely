using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters;

public sealed class PlanetCardClause : IJamlClause
{
    public string Label { get; init; } = "";
    public required MotelyPlanetCard[] Planets { get; init; }
    public PlanetSourceConfig Sources { get; init; } = new();
    public int[] Antes { get; init; } = [];
    public int Min { get; init; } = 1;
}

public struct PlanetCardFilterDesc(PlanetCardClause clause)
    : IMotelySeedFilterDesc<PlanetCardFilterDesc.PlanetCardFilter>
{
    private readonly PlanetCardClause _clause = clause;

    public PlanetCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
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

        return new PlanetCardFilter(_clause, maxShopItem, maxBoosterPack);
    }

    public struct PlanetCardFilter(PlanetCardClause clause, int maxShopItem, int maxBoosterPack) : IMotelySeedFilter
    {
        private readonly PlanetCardClause _clause = clause;
        private readonly int _maxShopItem = maxShopItem;
        private readonly int _maxBoosterPack = maxBoosterPack;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Planets.Length > 0);
            var clause = _clause;
            int maxShopItem = _maxShopItem;
            int maxBoosterPack = _maxBoosterPack;

            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                int needed = clause.Min;
                Debug.Assert(needed > 0, "PlanetCardClause.Min must be > 0 — loader bug.");

                int count = 0;
                var shopItems = clause.Sources.ShopItems;
                var boosterPacks = clause.Sources.BoosterPacks;

                foreach (var ante in clause.Antes)
                {
                    // ── Shop items ──
                    if (shopItems.Length > 0)
                    {
                        var shopStream = singleCtx.CreateShopItemStream(ante);

                        for (int slot = 0; slot <= maxShopItem; slot++)
                        {
                            var item = singleCtx.GetNextShopItem(ref shopStream);
                            bool isTarget = false;
                            for (int i = 0; i < shopItems.Length; i++)
                            {
                                if (shopItems[i] == slot) { isTarget = true; break; }
                            }

                            if (isTarget
                                && item.TypeCategory == MotelyItemTypeCategory.PlanetCard
                                && MatchesPlanet(item, clause))
                            {
                                count++;
                            }
                        }
                    }

                    // ── Celestial packs ──
                    if (boosterPacks.Length > 0)
                    {
                        var packStream = singleCtx.CreateBoosterPackStream(ante);
                        var planetStream = singleCtx.CreateCelestialPackPlanetStream(ante);

                        for (int p = 0; p <= maxBoosterPack; p++)
                        {
                            var pack = singleCtx.GetNextBoosterPack(ref packStream);
                            bool isTarget = false;
                            for (int i = 0; i < boosterPacks.Length; i++)
                            {
                                if (boosterPacks[i] == p) { isTarget = true; break; }
                            }

                            if (isTarget && pack.GetPackType() == MotelyBoosterPackType.Celestial)
                            {
                                var contents = singleCtx.GetNextCelestialPackContents(
                                    ref planetStream, pack.GetPackSize());
                                for (int i = 0; i < contents.Length; i++)
                                {
                                    if (MatchesPlanet(contents[i], clause))
                                        count++;
                                }
                            }
                            else if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                            {
                                singleCtx.GetNextCelestialPackContents(
                                    ref planetStream, pack.GetPackSize());
                            }
                        }
                    }

                    if (count >= needed) break;
                }

                return count >= needed;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MatchesPlanet(MotelyItem item, PlanetCardClause clause)
        {
            Debug.Assert(clause.Planets.Length > 0, "PlanetCardClause.Planets must not be empty — loader bug.");

            var itemType = item.Type;
            for (int i = 0; i < clause.Planets.Length; i++)
            {
                if (itemType == (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)clause.Planets[i]))
                    return true;
            }
            return false;
        }
    }
}
