using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters;

/// <summary>
/// Scoring functions for JAML Should clauses - counts ALL occurrences for accurate scoring
/// Returns actual counts, no early exit.
/// </summary>
public static class JamlScoring
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ArrayMax(int[] array)
    {
        if (array.Length == 0) return 0;
        int max = array[0];
        for (int i = 1; i < array.Length; i++)
        {
            if (array[i] > max) max = array[i];
        }
        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ArrayContains(int[] array, int value)
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == value) return true;
        }
        return false;
    }

    public static int GetDefaultShopSlotsForAnte(int ante)
    {
        if (ante == 0) return 4;
        if (ante == 1) return 4;
        return 6 + ante;
    }

    public static int GetDefaultPackSlotsForAnte(int ante)
    {
        return (ante == 0 || ante == 1) ? 4 : 6;
    }

    #region Boss Scoring
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountBossOccurrences(ref MotelySingleSearchContext ctx, BossClause clause, ref MotelyRunState runState)
    {
        int count = 0;
        // Bosses were already generated up to max required ante and cached in runState
        if (runState.CachedBosses == null) return 0;
        
        foreach (var ante in clause.Antes)
        {
            if (ante < 1 || ante >= runState.CachedBosses.Length) continue;
            
            var bossForAnte = runState.CachedBosses[ante];
            for (int i = 0; i < clause.Bosses.Length; i++)
            {
                if (clause.Bosses[i] == bossForAnte)
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }
    
    #endregion
    
    #region Standard Card Scoring
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountStandardCardOccurrences(ref MotelySingleSearchContext ctx, StandardCardClause clause, ref MotelyRunState runState)
    {
        int count = 0;
        var shopItems = clause.Sources.ShopItems;
        var boosterPacks = clause.Sources.BoosterPacks;
        
        int maxShopItem = ArrayMax(shopItems);
        int maxBoosterPack = ArrayMax(boosterPacks);

        foreach (var ante in clause.Antes)
        {
            // ── Shop items ──
            if (shopItems.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante);

                for (int slot = 0; slot <= maxShopItem; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (ArrayContains(shopItems, slot) && 
                        item.TypeCategory == MotelyItemTypeCategory.PlayingCard && 
                        MatchesStandardCard(item, clause))
                    {
                        count++;
                    }
                }
            }

            // ── Standard packs ──
            if (boosterPacks.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                var cardStream = ctx.CreateStandardPackCardStream(ante);

                for (int p = 0; p <= maxBoosterPack; p++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    
                    if (pack.GetPackType() == MotelyBoosterPackType.Standard)
                    {
                        var contents = ctx.GetNextStandardPackContents(ref cardStream, pack.GetPackSize());
                        if (ArrayContains(boosterPacks, p))
                        {
                            for (int i = 0; i < contents.Length; i++)
                            {
                                if (MatchesStandardCard(contents[i], clause))
                                    count++;
                            }
                        }
                    }
                }
            }
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesStandardCard(MotelyItem item, StandardCardClause clause)
    {
        if (clause.Rank.HasValue && item.PlayingCardRank != clause.Rank.Value) return false;
        if (clause.Suit.HasValue && item.PlayingCardSuit != clause.Suit.Value) return false;
        if (clause.Enhancement.HasValue && item.Enhancement != clause.Enhancement.Value) return false;
        if (clause.Seal.HasValue && item.Seal != clause.Seal.Value) return false;
        if (clause.Edition.HasValue && item.Edition != clause.Edition.Value) return false;
        return true;
    }
    
    #endregion
    
    #region Tarot Scoring
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountTarotCardOccurrences(ref MotelySingleSearchContext ctx, TarotCardClause clause, MotelyItemType[] targetTypes, ref MotelyRunState runState)
    {
        int count = 0;
        var shopIndices = clause.Sources.ShopItems;
        var boosterPacks = clause.Sources.BoosterPacks;
        var emperorRolls = clause.Sources.Emperor;
        var sealRolls = clause.Sources.PurpleSealOrEightBall;

        int maxShopItem = ArrayMax(shopIndices);
        int maxBoosterPack = ArrayMax(boosterPacks);
        int maxEmperor = ArrayMax(emperorRolls);
        int maxPurpleSeal = ArrayMax(sealRolls);

        foreach (var ante in clause.Antes)
        {
            // ── Shop items ──
            if (shopIndices.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante);

                for (int slot = 0; slot <= maxShopItem; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (ArrayContains(shopIndices, slot) && item.TypeCategory == MotelyItemTypeCategory.TarotCard)
                    {
                        if (MatchesType(item, targetTypes))
                            count++;
                    }
                }
            }

            // ── Arcana packs ──
            if (boosterPacks.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                var tarotStream = ctx.CreateArcanaPackTarotStream(ante);

                for (int p = 0; p <= maxBoosterPack; p++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);

                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                        if (ArrayContains(boosterPacks, p))
                        {
                            for (int i = 0; i < contents.Length; i++)
                            {
                                if (MatchesType(contents[i], targetTypes))
                                    count++;
                            }
                        }
                    }
                }
            }

            // ── Emperor ──
            if (emperorRolls.Length > 0)
            {
                var emperorStream = ctx.CreateEmperorTarotStream(ante);

                for (int roll = 0; roll <= maxEmperor; roll++)
                {
                    var (t1, t2) = ctx.GetNextEmperorTarots(ref emperorStream);
                    if (ArrayContains(emperorRolls, roll))
                    {
                        if (MatchesType(t1, targetTypes)) count++;
                        if (MatchesType(t2, targetTypes)) count++;
                    }
                }
            }

            // ── Purple Seal ──
            if (sealRolls.Length > 0)
            {
                var purpleSealStream = ctx.CreatePurpleSealTarotStream(ante);

                for (int roll = 0; roll <= maxPurpleSeal; roll++)
                {
                    var item = ctx.GetNextTarot(ref purpleSealStream);
                    if (ArrayContains(sealRolls, roll))
                    {
                        if (MatchesType(item, targetTypes))
                            count++;
                    }
                }
            }
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesType(MotelyItem item, MotelyItemType[] targetTypes)
    {
        for (int i = 0; i < targetTypes.Length; i++)
        {
            if (item.Type == targetTypes[i])
            {
                return true;
            }
        }
        return false;
    }
    
    #endregion
    
    #region Joker Scoring
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountJokerOccurrences(ref MotelySingleSearchContext ctx, JokerClause clause, MotelyItemType[] targetTypes, ref MotelyRunState runState)
    {
        int count = 0;
        var shopIndices = clause.Sources.ShopItems;
        var boosterPacks = clause.Sources.BoosterPacks;

        int maxShopItem = ArrayMax(shopIndices);
        int maxBoosterPack = ArrayMax(boosterPacks);

        foreach (var ante in clause.Antes)
        {
            // ── Shop items ──
            if (shopIndices.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante);

                for (int slot = 0; slot <= maxShopItem; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (ArrayContains(shopIndices, slot) && item.TypeCategory == MotelyItemTypeCategory.Joker)
                    {
                        if (MatchesJoker(item, targetTypes, clause))
                        {
                            runState.AddOwnedJoker((MotelyJoker)item.Type);
                            // Not checking for Showman logic implicitly for scoring as it may duplicate what the filter did
                            count++;
                        }
                    }
                }
            }

            // ── Buffoon packs ──
            if (boosterPacks.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                var jokerStream = ctx.CreateBuffoonPackJokerStream(ante);

                for (int p = 0; p <= maxBoosterPack; p++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);

                    if (pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                    {
                        var contents = ctx.GetNextBuffoonPackContents(ref jokerStream, pack.GetPackSize());
                        if (ArrayContains(boosterPacks, p))
                        {
                            for (int i = 0; i < contents.Length; i++)
                            {
                                if (MatchesJoker(contents[i], targetTypes, clause))
                                {
                                    runState.AddOwnedJoker((MotelyJoker)contents[i].Type);
                                    count++;
                                }
                            }
                        }
                    }
                }
            }
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesJoker(MotelyItem item, MotelyItemType[] targetTypes, JokerClause clause)
    {
        bool typeMatches = false;
        for (int i = 0; i < targetTypes.Length; i++)
        {
            if (item.Type == targetTypes[i])
            {
                typeMatches = true;
                break;
            }
        }

        if (!typeMatches) return false;

        if (clause.Edition.HasValue && item.Edition != clause.Edition.Value)
            return false;

        if (clause.Stickers.Length > 0)
        {
            for (int s = 0; s < clause.Stickers.Length; s++)
            {
                var hasSticker = clause.Stickers[s] switch
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
    
    #endregion
    
    #region Planet Scoring
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountPlanetCardOccurrences(ref MotelySingleSearchContext ctx, PlanetCardClause clause, MotelyItemType[] targetTypes, ref MotelyRunState runState)
    {
        int count = 0;
        var shopIndices = clause.Sources.ShopItems;
        var boosterPacks = clause.Sources.BoosterPacks;

        int maxShopItem = ArrayMax(shopIndices);
        int maxBoosterPack = ArrayMax(boosterPacks);

        foreach (var ante in clause.Antes)
        {
            // ── Shop items ──
            if (shopIndices.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante);

                for (int slot = 0; slot <= maxShopItem; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (ArrayContains(shopIndices, slot) && item.TypeCategory == MotelyItemTypeCategory.PlanetCard)
                    {
                        if (MatchesType(item, targetTypes))
                            count++;
                    }
                }
            }

            // ── Celestial packs ──
            if (boosterPacks.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                var planetStream = ctx.CreateCelestialPackPlanetStream(ante);

                for (int p = 0; p <= maxBoosterPack; p++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);

                    if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                    {
                        var contents = ctx.GetNextCelestialPackContents(ref planetStream, pack.GetPackSize());
                        if (ArrayContains(boosterPacks, p))
                        {
                            for (int i = 0; i < contents.Length; i++)
                            {
                                if (MatchesType(contents[i], targetTypes))
                                    count++;
                            }
                        }
                    }
                }
            }
        }

        return count;
    }
    
    #endregion
    
    #region Spectral Scoring
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountSpectralCardOccurrences(ref MotelySingleSearchContext ctx, SpectralCardClause clause, MotelyItemType[] targetTypes, ref MotelyRunState runState)
    {
        int count = 0;
        var shopIndices = clause.Sources.ShopItems;
        var boosterPacks = clause.Sources.BoosterPacks;
        var sixthSenseRolls = clause.Sources.SixthSense;
        var seanceRolls = clause.Sources.Seance;

        int maxShopItem = ArrayMax(shopIndices);
        int maxBoosterPack = ArrayMax(boosterPacks);
        int maxSixthSense = ArrayMax(sixthSenseRolls);
        int maxSeance = ArrayMax(seanceRolls);

        foreach (var ante in clause.Antes)
        {
            // ── Shop items ──
            if (shopIndices.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante);

                for (int slot = 0; slot <= maxShopItem; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (ArrayContains(shopIndices, slot) && item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                    {
                        if (MatchesType(item, targetTypes))
                            count++;
                    }
                }
            }

            // ── Spectral packs ──
            if (boosterPacks.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                var spectralStream = ctx.CreateSpectralPackSpectralStream(ante);

                for (int p = 0; p <= maxBoosterPack; p++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);

                    if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                    {
                        var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
                        if (ArrayContains(boosterPacks, p))
                        {
                            for (int i = 0; i < contents.Length; i++)
                            {
                                if (MatchesType(contents[i], targetTypes))
                                    count++;
                            }
                        }
                    }
                }
            }

            // ── Sixth Sense ──
            if (sixthSenseRolls.Length > 0)
            {
                var sixthSenseStream = ctx.CreateSixthSenseSpectralStream(ante);

                for (int roll = 0; roll <= maxSixthSense; roll++)
                {
                    var spectral = ctx.GetNextSpectral(ref sixthSenseStream);
                    if (ArrayContains(sixthSenseRolls, roll))
                    {
                        if (MatchesType(spectral, targetTypes))
                            count++;
                    }
                }
            }

            // ── Seance ──
            if (seanceRolls.Length > 0)
            {
                var seanceStream = ctx.CreateSeanceSpectralStream(ante);

                for (int roll = 0; roll <= maxSeance; roll++)
                {
                    var spectral = ctx.GetNextSpectral(ref seanceStream);
                    if (ArrayContains(seanceRolls, roll))
                    {
                        if (MatchesType(spectral, targetTypes))
                            count++;
                    }
                }
            }
        }

        return count;
    }
    
    #endregion
    
    #region Voucher Scoring
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountVoucherOccurrences(ref MotelySingleSearchContext ctx, VoucherClause clause, ref MotelyRunState voucherState)
    {
        var localVoucherState = voucherState; 
        int count = 0;

        int minAnte = clause.Antes.Length > 0 ? clause.Antes[0] : 1;
        int maxAnte = clause.Antes.Length > 0 ? clause.Antes[clause.Antes.Length - 1] : 1;

        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            var voucherAtAnte = ctx.GetAnteFirstVoucher(ante, localVoucherState);

            bool anteWanted = false;
            for (int i = 0; i < clause.Antes.Length; i++)
            {
                if (clause.Antes[i] == ante)
                {
                    anteWanted = true;
                    break;
                }
            }

            if (anteWanted)
            {
                bool matches = false;
                for (int i = 0; i < clause.Vouchers.Length; i++)
                {
                    if (voucherAtAnte == clause.Vouchers[i])
                    {
                        matches = true;
                        break;
                    }
                }

                if (matches) count++;
            }

            localVoucherState.ActivateVoucher(voucherAtAnte);

            if (voucherAtAnte == MotelyVoucher.Hieroglyph)
            {
                var voucherStream = ctx.CreateVoucherStream(ante);
                var bonusVoucher = ctx.GetNextVoucher(ref voucherStream, localVoucherState);

                if (anteWanted)
                {
                    bool bonusMatches = false;
                    for (int i = 0; i < clause.Vouchers.Length; i++)
                    {
                        if (bonusVoucher == clause.Vouchers[i])
                        {
                            bonusMatches = true;
                            break;
                        }
                    }

                    if (bonusMatches) count++;
                }

                localVoucherState.ActivateVoucher(bonusVoucher);
            }
        }

        return count;
    }
    
    #endregion
}
