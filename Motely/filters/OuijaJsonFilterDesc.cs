using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;
namespace Motely.Filters;

/// <summary>
/// Clean filter descriptor for MongoDB-style queries
/// </summary>
public struct OuijaJsonFilterDesc(OuijaConfig config) : IMotelySeedFilterDesc<OuijaJsonFilterDesc.OuijaJsonFilter>
{
    public static bool PrefilterEnabled;
    public static Action<string, int, int[]> OnResultFound;
    private readonly OuijaConfig _config = config;
    public int Cutoff { get; set; } = 1;
    public bool AutoCutoff { get; set; } = false;



    public string Name => _config.Name ?? "OuijaJsonFilter";
    public string Description => _config.Description ?? "JSON-configured filter";

    public OuijaJsonFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        return new OuijaJsonFilter(_config);
    }

    public struct OuijaJsonFilter : IMotelySeedFilter
        {
            public static bool IsCancelled;
            private readonly OuijaConfig _config;

        public OuijaJsonFilter(OuijaConfig config)
        {
            _config = config;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            VectorMask mask = VectorMask.AllBitsSet;
            if (_config.Must?.Count > 0)
            {
                foreach (var clause in _config.Must)
                {
                    mask = ProcessClause(ref searchContext, clause, mask, true);
                    if (mask.IsAllFalse()) return mask;
                }
            }

            if (_config.MustNot?.Count > 0)
            {
                foreach (var clause in _config.MustNot)
                {
                    mask = ProcessClause(ref searchContext, clause, mask, false);
                    if (mask.IsAllFalse()) return mask;
                }
            }

            return mask;
        }

        public bool FilterSingle(ref MotelySingleSearchContext ctx, ulong seed)
        {
            var voucherState = new MotelyRunState();

            if (_config.Must?.Count > 0)
            {
                foreach (var clause in _config.Must)
                {
                    if (clause.Min.HasValue && clause.Min.Value > 1)
                    {
                        if (CountOccurrences(ref ctx, clause, ref voucherState) < clause.Min.Value)
                            return false;
                    }
                    else if (!CheckSingleClause(ref ctx, clause, ref voucherState))
                        return false;
                }
            }

            if (_config.MustNot?.Count > 0)
            {
                foreach (var clause in _config.MustNot)
                {
                    if (CheckSingleClause(ref ctx, clause, ref voucherState))
                        return false;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask ProcessClause(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, VectorMask mask, bool isMust)
        {
            var result = clause.ItemTypeEnum switch
            {
                MotelyFilterItemType.Joker => CheckJoker(ref ctx, clause),
                MotelyFilterItemType.SoulJoker => CheckSoulJoker(ref ctx, clause),
                MotelyFilterItemType.TarotCard => CheckTarot(ref ctx, clause),
                MotelyFilterItemType.PlanetCard => CheckPlanet(ref ctx, clause),
                MotelyFilterItemType.SpectralCard => CheckSpectral(ref ctx, clause),
                MotelyFilterItemType.SmallBlindTag or MotelyFilterItemType.BigBlindTag => CheckTag(ref ctx, clause),
                MotelyFilterItemType.PlayingCard => CheckPlayingCard(ref ctx, clause),
                MotelyFilterItemType.Boss => CheckBoss(ref ctx, clause),
                _ => throw new ArgumentOutOfRangeException(nameof(clause.ItemTypeEnum), clause.ItemTypeEnum, null)

            };

            return isMust ? mask & result : mask & (result ^ VectorMask.AllBitsSet);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckJoker(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            if (clause.Sources?.ShopSlots?.Length > 0)
                return VectorMask.AllBitsSet;

            var mask = VectorMask.AllBitsSet;
            foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
            {
                var shopStream = ctx.CreateShopItemStream(ante);
                var packStream = ctx.CreateBoosterPackStream(ante);

                if (clause.Sources?.ShopSlots?.Length > 0)
                {
                    var slotSet = new HashSet<int>(clause.Sources.ShopSlots);
                    int maxSlot = clause.Sources.ShopSlots.Max();
                    for (int slot = 0; slot <= maxSlot; slot++)
                    {
                        var item = ctx.GetNextShopItem(ref shopStream);
                        if (!slotSet.Contains(slot) || item.TypeCategory != MotelyItemTypeCategory.Joker) continue;

                        var joker = new MotelyItem(item.Value).GetJoker();
                        var matches = clause.JokerEnum.HasValue ?
                            joker == clause.JokerEnum.Value :
                            CheckWildcardMatch(joker, clause.WildcardEnum);

                        if (matches && CheckEditionAndStickers(item, clause))
                        {
                            mask &= VectorMask.AllBitsSet;
                            break;
                        }
                    }
                }

                if (clause.Sources?.PackSlots?.Length > 0)
                {
                    var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
                    int maxPacks = clause.Sources.PackSlots.Max() + 1;
                    for (int i = 0; i < maxPacks; i++)
                    {
                        var pack = ctx.GetNextBoosterPack(ref packStream);
                        if (pack.GetPackType() != MotelyBoosterPackType.Buffoon) continue;
                        if (clause.Sources.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                        var contents = ctx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize());
                        for (int j = 0; j < contents.Count; j++)
                        {
                            var item = contents[j];
                            var joker = new MotelyItem(item.Value).GetJoker();
                            var matches = clause.JokerEnum.HasValue ?
                                joker == clause.JokerEnum.Value :
                                CheckWildcardMatch(joker, clause.WildcardEnum);

                            if (matches && CheckEditionAndStickers(item, clause))
                            {
                                mask &= VectorMask.AllBitsSet;
                                goto NextAnte;
                            }
                        }
                    }
                }

            NextAnte:;
                if (mask.IsAllFalse()) break;
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckSoulJoker(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            var mask = VectorMask.AllBitsSet;
            foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                var soulStream = ctx.CreateSoulJokerStream(ante, MotelyJokerStreamFlags.Default);
                bool foundSoul = false;

                for (int i = 0; i < (ante == 1 ? 4 : 6); i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana || pack.GetPackType() == MotelyBoosterPackType.Spectral)
                    {
                        if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                        var hasSoul = pack.GetPackType() == MotelyBoosterPackType.Arcana ?
                            CheckArcanaForSoul(ref ctx, ante, pack.GetPackSize()) :
                            CheckSpectralForSoul(ref ctx, ante, pack.GetPackSize());

                        if (hasSoul)
                        {
                            var soulJoker = ctx.GetNextJoker(ref soulStream);
                            var matches = !clause.JokerEnum.HasValue || soulJoker.Type == new MotelyItem(clause.JokerEnum.Value).Type;
                            if (matches && CheckEditionAndStickers(soulJoker, clause))
                            {
                                foundSoul = true;
                                break;
                            }
                        }
                    }
                }

                if (!foundSoul)
                {
                    mask = VectorMask.AllBitsClear;
                    break;
                }
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckTarot(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            if (!clause.TarotEnum.HasValue) return VectorMask.AllBitsSet;

            var mask = VectorMask.AllBitsSet;
            foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
            {
                bool found = CheckShopForTarot(ref ctx, clause, ante) || CheckPacksForTarot(ref ctx, clause, ante);
                if (!found)
                {
                    mask = VectorMask.AllBitsClear;
                    break;
                }
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckPlanet(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            if (!clause.PlanetEnum.HasValue) return VectorMask.AllBitsSet;

            var mask = VectorMask.AllBitsSet;
            foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
            {
                bool found = CheckShopForPlanet(ref ctx, clause, ante) || CheckPacksForPlanet(ref ctx, clause, ante);
                if (!found)
                {
                    mask = VectorMask.AllBitsClear;
                    break;
                }
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckSpectral(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            var mask = VectorMask.AllBitsSet;
            foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
            {
                bool found = CheckShopForSpectral(ref ctx, clause, ante) || CheckPacksForSpectral(ref ctx, clause, ante);
                if (!found)
                {
                    mask = VectorMask.AllBitsClear;
                    break;
                }
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckTag(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            if (!clause.TagEnum.HasValue) return VectorMask.AllBitsSet;

            var mask = VectorMask.AllBitsSet;
            foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
            {
                var tagStream = ctx.CreateTagStream(ante);
                var smallTag = ctx.GetNextTag(ref tagStream);
                var bigTag = ctx.GetNextTag(ref tagStream);

                var tagMatches = clause.TagTypeEnum switch
                {
                    MotelyTagType.SmallBlind => VectorEnum256.Equals(smallTag, clause.TagEnum.Value),
                    MotelyTagType.BigBlind => VectorEnum256.Equals(bigTag, clause.TagEnum.Value),
                    _ => VectorEnum256.Equals(smallTag, clause.TagEnum.Value) | VectorEnum256.Equals(bigTag, clause.TagEnum.Value)
                };

                mask &= tagMatches;
                if (mask.IsAllFalse()) break;
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckPlayingCard(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            var mask = VectorMask.AllBitsSet;
            foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
            {
                bool found = CheckPacksForPlayingCard(ref ctx, clause, ante);
                if (!found)
                {
                    mask = VectorMask.AllBitsClear;
                    break;
                }
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckBoss(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            if (!clause.BossEnum.HasValue) return VectorMask.AllBitsSet;

            var mask = VectorMask.AllBitsSet;
            foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
            {
                var bossStream = ctx.CreateBossStream(ante);
                var boss = ctx.GetNextBoss(ref bossStream);
                var bossMatches = VectorEnum256.Equals(boss, clause.BossEnum.Value);
                mask &= bossMatches;
                if (mask.IsAllFalse()) break;
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckVoucher(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!clause.VoucherEnum.HasValue) return VectorMask.AllBitsSet;

            var voucherStream = ctx.CreateVoucherStream(ante);
            var voucher = ctx.GetNextVoucher(ref voucherStream);
            return VectorEnum256.Equals(voucher, clause.VoucherEnum.Value);
        }

        private static int CountOccurrences(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, ref MotelyRunState voucherState)
        {
            int totalCount = 0;
            foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
            {
                var anteCount = clause.ItemTypeEnum switch
                {
                    MotelyFilterItemType.Joker => CheckJokerSingle(ref ctx, clause, ante),
                    MotelyFilterItemType.SoulJoker => CheckSoulJokerSingle(ref ctx, clause, ante, ref voucherState),
                    MotelyFilterItemType.TarotCard => CheckTarotSingle(ref ctx, clause, ante, ref voucherState) ? 1 : 0,
                    MotelyFilterItemType.PlanetCard => CheckPlanetSingle(ref ctx, clause, ante) ? 1 : 0,
                    MotelyFilterItemType.SpectralCard => CheckSpectralSingle(ref ctx, clause, ante) ? 1 : 0,
                    MotelyFilterItemType.SmallBlindTag or MotelyFilterItemType.BigBlindTag => CheckTagSingle(ref ctx, clause, ante) ? 1 : 0,
                    MotelyFilterItemType.Voucher => CheckVoucherSingle(ref ctx, clause, ante, ref voucherState) ? 1 : 0,
                    MotelyFilterItemType.PlayingCard => CheckPlayingCardSingle(ref ctx, clause, ante) ? 1 : 0,
                    MotelyFilterItemType.Boss => CheckBossSingle(ref ctx, clause, ante) ? 1 : 0,
                    _ => 0
                };
                totalCount += anteCount;
            }
            return totalCount;
        }

        private static bool CheckSingleClause(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, ref MotelyRunState voucherState)
        {
            if (clause.EffectiveAntes?.Length == 0) return false;

            foreach (var ante in clause.EffectiveAntes)
            {
                var found = clause.ItemTypeEnum switch
                {
                    MotelyFilterItemType.Joker => CheckJokerSingle(ref ctx, clause, ante) > 0,
                    MotelyFilterItemType.SoulJoker => CheckSoulJokerSingle(ref ctx, clause, ante, ref voucherState) > 0,
                    MotelyFilterItemType.TarotCard => CheckTarotSingle(ref ctx, clause, ante, ref voucherState),
                    MotelyFilterItemType.PlanetCard => CheckPlanetSingle(ref ctx, clause, ante),
                    MotelyFilterItemType.SpectralCard => CheckSpectralSingle(ref ctx, clause, ante),
                    MotelyFilterItemType.SmallBlindTag or MotelyFilterItemType.BigBlindTag => CheckTagSingle(ref ctx, clause, ante),
                    MotelyFilterItemType.PlayingCard => CheckPlayingCardSingle(ref ctx, clause, ante),
                    MotelyFilterItemType.Boss => CheckBossSingle(ref ctx, clause, ante),
                    _ => false
                };

                if (found)
                {
                    if (clause.ItemTypeEnum == MotelyFilterItemType.Joker && clause.JokerEnum == MotelyJoker.Showman)
                        voucherState.ActivateShowman();
                    return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckWildcardMatch(MotelyJoker joker, JokerWildcard? wildcard)
        {
            if (!wildcard.HasValue) return false;
            if (wildcard == JokerWildcard.AnyJoker) return true;

            var rarity = (MotelyJokerRarity)((int)joker & Motely.JokerRarityMask);
            return wildcard switch
            {
                JokerWildcard.AnyCommon => rarity == MotelyJokerRarity.Common,
                JokerWildcard.AnyUncommon => rarity == MotelyJokerRarity.Uncommon,
                JokerWildcard.AnyRare => rarity == MotelyJokerRarity.Rare,
                _ => false
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckEditionAndStickers(in MotelyItem item, OuijaConfig.FilterItem clause)
        {
            if (clause.EditionEnum.HasValue && item.Edition != clause.EditionEnum.Value)
                return false;

            if (clause.StickerEnums?.Count > 0)
            {
                foreach (var sticker in clause.StickerEnums)
                {
                    var hasSticker = sticker switch
                    {
                        MotelyJokerSticker.Eternal => item.IsEternal,
                        MotelyJokerSticker.Perishable => item.IsPerishable,
                        MotelyJokerSticker.Rental => item.IsRental,
                        _ => true
                    };
                    if (!hasSticker) return false;
                }
            }

            return true;
        }

        private static bool CheckArcanaForSoul(ref MotelyVectorSearchContext ctx, int ante, MotelyBoosterPackSize size)
        {
            var tarotStream = ctx.CreateArcanaPackTarotStream(ante, soulOnly: true);
            var contents = ctx.GetNextArcanaPackContents(ref tarotStream, size);
            for (int i = 0; i < contents.Count; i++)
                    {
                        if (contents[i].Type == MotelyItemType.Soul)
                    return true;
            }
            return false;
        }

        private static bool CheckSpectralForSoul(ref MotelyVectorSearchContext ctx, int ante, MotelyBoosterPackSize size)
        {
            var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
            var contents = ctx.GetNextSpectralPackContents(ref spectralStream, size);
            for (int i = 0; i < contents.Count; i++)
                    {
                        if (contents[i].Type == MotelyItemType.Soul)
                    return true;
            }
            return false;
        }

        private static bool CheckShopForTarot(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (clause.Sources?.ShopSlots?.Length == 0) return false;

            var shopStream = ctx.CreateShopItemStream(ante);
            var slots = clause.Sources?.ShopSlots ?? new[] { 0, 1, 2, 3, 4, 5 };
            var slotSet = new HashSet<int>(slots);

            for (int i = 0; i < slots.Max(); i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (slotSet.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.TarotCard)
                {
                    var tarot = new MotelyItem(item.Value).GetTarot();
                    if (tarot == clause.TarotEnum.Value)
                        return true;
                }
            }
            return false;
        }

        private static bool CheckPacksForTarot(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (clause.Sources?.PackSlots?.Length == 0) return false;

            var packStream = ctx.CreateBoosterPackStream(ante);
            var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 };

            for (int i = 0; i < (ante == 1 ? 4 : 6); i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                    var tarotStream = ctx.CreateArcanaPackTarotStream(ante);
                    var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                    for (int j = 0; j < contents.Count; j++)
                    {
                        if (contents[j].Type == (MotelyItemType)clause.TarotEnum.Value)
                            return true;
                    }
                }
            }
            return false;
        }

        private static bool CheckShopForPlanet(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (clause.Sources?.ShopSlots?.Length == 0) return false;

            var shopStream = ctx.CreateShopItemStream(ante);
            var slots = clause.Sources?.ShopSlots ?? new[] { 0, 1, 2, 3, 4, 5 };
            var slotSet = new HashSet<int>(slots);

            for (int i = 0; i < slots.Max(); i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (slotSet.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.PlanetCard)
                {
                    var planet = new MotelyItem(item.Value).GetPlanet();
                    if (planet == clause.PlanetEnum.Value)
                        return true;
                }
            }
            return false;
        }

        private static bool CheckPacksForPlanet(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (clause.Sources?.PackSlots?.Length == 0) return false;

            var packStream = ctx.CreateBoosterPackStream(ante);
            var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 };

            for (int i = 0; i < (ante == 1 ? 4 : 6); i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Celestial)
                {
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                    var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
                    var contents = ctx.GetNextCelestialPackContents(ref planetStream, pack.GetPackSize());
                    for (int j = 0; j < contents.Count; j++)
                    {
                        if (contents[j].Type == (MotelyItemType)clause.PlanetEnum.Value)
                            return true;
                    }
                }
            }
            return false;
        }

        private static bool CheckShopForSpectral(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (clause.Sources?.ShopSlots?.Length == 0) return false;

            var shopStream = ctx.CreateShopItemStream(ante);
            var slots = clause.Sources?.ShopSlots ?? new[] { 0, 1, 2, 3, 4, 5 };
            var slotSet = new HashSet<int>(slots);

            for (int i = 0; i < slots.Max(); i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (slotSet.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                {
                    if (clause.SpectralEnum.HasValue)
                    {
                        var spectral = new MotelyItem(item.Value).GetSpectral();
                        if (spectral == clause.SpectralEnum.Value)
                            return true;
                    }
                    else
                        return true;
                }
            }
            return false;
        }

        private static bool CheckPacksForSpectral(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (clause.Sources?.PackSlots?.Length == 0) return false;

            var packStream = ctx.CreateBoosterPackStream(ante);
            var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 };

            for (int i = 0; i < (ante == 1 ? 4 : 6); i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                    var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
                    var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
                    for (int j = 0; j < contents.Count; j++)
                    {
                        var item = contents[j];
                        if (item.Type == MotelyItemType.Soul || item.Type == MotelyItemType.BlackHole)
                        {
                            if (!clause.SpectralEnum.HasValue ||
                                (item.Type == MotelyItemType.Soul && clause.SpectralEnum == MotelySpectralCard.Soul) ||
                                (item.Type == MotelyItemType.BlackHole && clause.SpectralEnum == MotelySpectralCard.BlackHole))
                                return true;
                        }
                        else if (item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                        {
                            if (!clause.SpectralEnum.HasValue)
                                return true;
                            var spectral = new MotelyItem(item.Value).GetSpectral();
                            if (spectral == clause.SpectralEnum.Value)
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool CheckPacksForPlayingCard(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            var packStream = ctx.CreateBoosterPackStream(ante);
            var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 };

            for (int i = 0; i < (ante == 1 ? 4 : 6); i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Standard)
                {
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                    var cardStream = ctx.CreateStandardPackCardStream(ante);
                    var contents = ctx.GetNextStandardPackContents(ref cardStream, pack.GetPackSize());
                    for (int j = 0; j < contents.Length; j++)
                    {
                        var item = contents[j];
                        if (item.TypeCategory == MotelyItemTypeCategory.PlayingCard &&
                            (!clause.SuitEnum.HasValue || item.PlayingCardSuit == clause.SuitEnum.Value) &&
                            (!clause.RankEnum.HasValue || item.PlayingCardRank == clause.RankEnum.Value) &&
                            (!clause.EnhancementEnum.HasValue || item.Enhancement == clause.EnhancementEnum.Value) &&
                            (!clause.SealEnum.HasValue || item.Seal == clause.SealEnum.Value) &&
                            (!clause.EditionEnum.HasValue || item.Edition == clause.EditionEnum.Value))
                            return true;
                    }
                }
            }
            return false;
        }

        private static int CheckJokerSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            int foundCount = 0;
            var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: false);

            if (clause.Sources?.ShopSlots?.Length > 0)
            {
                var slotSet = new HashSet<int>(clause.Sources.ShopSlots);
                int maxSlot = clause.Sources.ShopSlots.Max();
                for (int i = 0; i <= maxSlot; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (slotSet.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.Joker)
                    {
                        var joker = new MotelyItem(item.Value).GetJoker();
                        var matches = clause.JokerEnum.HasValue ?
                            joker == clause.JokerEnum.Value :
                            CheckWildcardMatch(joker, clause.WildcardEnum);
                        if (matches && CheckEditionAndStickers(item, clause))
                        {
                            foundCount++;
                            if (clause.Min.HasValue && foundCount >= clause.Min.Value)
                                return foundCount;
                        }
                    }
                }
            }

            if (clause.Sources?.PackSlots?.Length > 0)
            {
                var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
                var packSlots = clause.Sources.PackSlots;
                int maxPackSlot = packSlots.Max();
                for (int i = 0; i <= maxPackSlot; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                    {
                        if (clause.Sources.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                        var contents = ctx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize());
                        for (int j = 0; j < contents.Count; j++)
                        {
                            var item = contents[j];
                            var joker = new MotelyItem(item.Value).GetJoker();
                            var matches = clause.JokerEnum.HasValue ?
                                joker == clause.JokerEnum.Value :
                                CheckWildcardMatch(joker, clause.WildcardEnum);
                            if (matches && CheckEditionAndStickers(item, clause))
                            {
                                foundCount++;
                                if (clause.Min.HasValue && foundCount >= clause.Min.Value)
                                    return foundCount;
                            }
                        }
                    }
                }
            }

            return foundCount;
        }

        private static int CheckSoulJokerSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelyRunState runState)
        {
            int foundCount = 0;
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: false);
            var soulStream = ctx.CreateSoulJokerStream(ante, MotelyJokerStreamFlags.Default);
            bool soulStreamInit = false;

            var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 };
            int packCount = packSlots.Length > 0 ? packSlots.Max() + 1 : (ante == 1 ? 4 : 6);

            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (packSlots.Contains(i) && (pack.GetPackType() == MotelyBoosterPackType.Arcana || pack.GetPackType() == MotelyBoosterPackType.Spectral))
                {
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                    bool hasSoul = false;
                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        var tarotStream = ctx.CreateArcanaPackTarotStream(ante, soulOnly: true);
                        var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                        for (int j = 0; j < contents.Count; j++)
                        {
                            if (contents[j] == MotelyItemType.Soul)
                            {
                                hasSoul = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
                        var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
                        for (int j = 0; j < contents.Count; j++)
                        {
                            if (contents[j] == MotelyItemType.Soul)
                            {
                                hasSoul = true;
                                break;
                            }
                        }
                    }

                    if (hasSoul)
                    {
                        if (!soulStreamInit)
                        {
                            soulStreamInit = true;
                        }
                        var soulJoker = ctx.GetNextJoker(ref soulStream);
                        var matches = !clause.JokerEnum.HasValue || soulJoker.Type == new MotelyItem(clause.JokerEnum.Value).Type;
                        if (matches && CheckEditionAndStickers(soulJoker, clause))
                        {
                            foundCount++;
                            if (clause.Min.HasValue && foundCount >= clause.Min.Value)
                                return foundCount;
                        }
                    }
                }
            }

            return foundCount;
        }

        private static bool CheckTarotSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelyRunState runState)
        {
            if (!clause.TarotEnum.HasValue) return false;

            if (clause.Sources?.ShopSlots?.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
                var slotSet = new HashSet<int>(clause.Sources.ShopSlots);
                int maxSlot = clause.Sources.ShopSlots.Max();
                for (int i = 0; i <= maxSlot; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (slotSet.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.TarotCard)
                    {
                        var tarot = new MotelyItem(item.Value).GetTarot();
                        if (tarot == clause.TarotEnum.Value)
                            return true;
                    }
                }
            }

            if (clause.Sources?.PackSlots?.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante, isCached: false);
                var packSlots = clause.Sources.PackSlots;
                int packCount = packSlots.Max() + 1;
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        if (clause.Sources.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                        var tarotStream = ctx.CreateArcanaPackTarotStream(ante);
                        var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                        for (int j = 0; j < contents.Count; j++)
                        {
                            if (contents[j].Type == (MotelyItemType)clause.TarotEnum.Value)
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool CheckPlanetSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!clause.PlanetEnum.HasValue) return false;

            if (clause.Sources?.ShopSlots?.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
                var slotSet = new HashSet<int>(clause.Sources.ShopSlots);
                int maxSlot = clause.Sources.ShopSlots.Max();
                for (int i = 0; i <= maxSlot; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (slotSet.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.PlanetCard)
                    {
                        var planet = new MotelyItem(item.Value).GetPlanet();
                        if (planet == clause.PlanetEnum.Value)
                            return true;
                    }
                }
            }

            if (clause.Sources?.PackSlots?.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante, isCached: false);
                var packSlots = clause.Sources.PackSlots;
                int packCount = packSlots.Max() + 1;
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Celestial)
                    {
                        if (clause.Sources.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                        var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
                        var contents = ctx.GetNextCelestialPackContents(ref planetStream, pack.GetPackSize());
                        for (int j = 0; j < contents.Count; j++)
                        {
                            if (contents[j].Type == (MotelyItemType)clause.PlanetEnum.Value)
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool CheckSpectralSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            bool searchAnySpectral = !clause.SpectralEnum.HasValue;

            if (clause.Sources?.ShopSlots?.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
                var slotSet = new HashSet<int>(clause.Sources.ShopSlots);
                int maxSlot = clause.Sources.ShopSlots.Max();
                for (int i = 0; i <= maxSlot; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (slotSet.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                    {
                        if (searchAnySpectral)
                            return true;
                        var spectral = new MotelyItem(item.Value).GetSpectral();
                        if (spectral == clause.SpectralEnum.Value)
                            return true;
                    }
                }
            }

            if (clause.Sources?.PackSlots?.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante, isCached: false);
                var packSlots = clause.Sources.PackSlots;
                int packCount = packSlots.Max() + 1;
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Spectral)
                    {
                        if (clause.Sources.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                        var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
                        var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
                        for (int j = 0; j < contents.Count; j++)
                        {
                            var item = contents[j];
                            if (item.Type == MotelyItemType.Soul || item.Type == MotelyItemType.BlackHole)
                            {
                                if (searchAnySpectral ||
                                    (item.Type == MotelyItemType.Soul && clause.SpectralEnum == MotelySpectralCard.Soul) ||
                                    (item.Type == MotelyItemType.BlackHole && clause.SpectralEnum == MotelySpectralCard.BlackHole))
                                    return true;
                            }
                            else if (item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                            {
                                if (searchAnySpectral)
                                    return true;
                                var spectral = new MotelyItem(item.Value).GetSpectral();
                                if (spectral == clause.SpectralEnum.Value)
                                    return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool CheckTagSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!clause.TagEnum.HasValue) return false;

            var tagStream = ctx.CreateTagStream(ante);
            var smallTag = ctx.GetNextTag(ref tagStream);
            var bigTag = ctx.GetNextTag(ref tagStream);

            return clause.TagTypeEnum switch
            {
                MotelyTagType.SmallBlind => smallTag == clause.TagEnum.Value,
                MotelyTagType.BigBlind => bigTag == clause.TagEnum.Value,
                _ => smallTag == clause.TagEnum.Value || bigTag == clause.TagEnum.Value
            };
        }

        private static bool CheckVoucherSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelyRunState voucherState)
        {
            if (!clause.VoucherEnum.HasValue) return false;

            if (voucherState.IsVoucherActive(clause.VoucherEnum.Value))
                return true;

            var voucher = ctx.GetAnteFirstVoucher(ante, voucherState);
            if (voucher == clause.VoucherEnum.Value)
            {
                voucherState.ActivateVoucher(voucher);
                return true;
            }

            return false;
        }

        private static bool CheckPlayingCardSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: true);
            var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 };
            int packCount = packSlots.Max() + 1;

            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Standard)
                {
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                    var cardStream = ctx.CreateStandardPackCardStream(ante);
                    var contents = ctx.GetNextStandardPackContents(ref cardStream, pack.GetPackSize());
                    for (int j = 0; j < contents.Count; j++)
                        {
                            var item = contents[j];
                        if (item.TypeCategory == MotelyItemTypeCategory.PlayingCard &&
                            (!clause.SuitEnum.HasValue || item.PlayingCardSuit == clause.SuitEnum.Value) &&
                            (!clause.RankEnum.HasValue || item.PlayingCardRank == clause.RankEnum.Value) &&
                            (!clause.EnhancementEnum.HasValue || item.Enhancement == clause.EnhancementEnum.Value) &&
                            (!clause.SealEnum.HasValue || item.Seal == clause.SealEnum.Value) &&
                            (!clause.EditionEnum.HasValue || item.Edition == clause.EditionEnum.Value))
                            return true;
                    }
                }
            }

            return false;
        }

        private static bool CheckBossSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!clause.BossEnum.HasValue) return false;
            return ctx.GetBossForAnte(ante) == clause.BossEnum.Value;
        }
    }
}
