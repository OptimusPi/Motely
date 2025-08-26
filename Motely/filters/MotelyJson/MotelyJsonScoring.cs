using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters;

/// <summary>
/// Scoring functions for Should clauses - count ALL occurrences for accurate scoring
/// Returns actual counts, no early exit (except when earlyExit parameter is true)
/// </summary>
public static class MotelyJsonScoring
{
    #region Count Functions for Should Clauses
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TarotCardsTally(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState, bool earlyExit = false)
    {
        Debug.Assert(clause.TarotEnum.HasValue, "TarotCardsTally requires TarotEnum");
        Debug.Assert(clause.Sources != null, "TarotCardsTally requires Sources");
        Debug.Assert(clause.Sources.PackSlots != null, "TarotCardsTally requires PackSlots");
        Debug.Assert(clause.Sources.ShopSlots != null, "TarotCardsTally requires ShopSlots");
        int tally = 0;

        // Check shop slots
        if (clause.Sources.ShopSlots.Length > 0)
        {
            var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
            var shopSlots = clause.Sources.ShopSlots;
            int maxSlot = clause.MaxShopSlot ?? (shopSlots.Length > 0 ? shopSlots.Max() : 0);
            
            for (int i = 0; i <= maxSlot; i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (shopSlots.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.TarotCard)
                {
                    var tarot = new MotelyItem(item.Value).GetTarot();
                    if (tarot == clause.TarotEnum.Value)
                    {
                        tally++;
                        if (earlyExit) return tally; // Early exit for filtering
                    }
                }
            }
        }

        // Check pack slots
        if (clause.Sources?.PackSlots?.Length > 0)
        {
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: false);
            var tarotStream = ctx.CreateArcanaPackTarotStream(ante); // Create ONCE before loop
            var packSlots = clause.Sources.PackSlots;
            int packCount = (clause.MaxPackSlot ?? (packSlots.Length > 0 ? packSlots.Max() : 0)) + 1;
            
            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                // Always advance stream for Arcana packs to maintain PRNG sync
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                    
                    // Only score if this slot is in our filter
                    if (packSlots.Contains(i) && 
                        !(clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega))
                    {
                        for (int j = 0; j < contents.Length; j++)
                        {
                            if (contents[j].Type == (MotelyItemType)clause.TarotEnum.Value)
                            {
                                tally++;
                                if (earlyExit) return tally; // Early exit for filtering
                            }
                        }
                    }
                }
            }
        }

        return tally;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountPlanetOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, bool earlyExit = false)
    {
        Debug.Assert(clause.PlanetEnum.HasValue, "CountPlanetOccurrences requires PlanetEnum");
        
        int tally = 0;

        // Check shop slots
        if (clause.Sources?.ShopSlots?.Length > 0)
        {
            var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
            var shopSlots = clause.Sources.ShopSlots;
            int maxSlot = clause.MaxShopSlot ?? (shopSlots.Length > 0 ? shopSlots.Max() : 0);
            
            for (int i = 0; i <= maxSlot; i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (shopSlots.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.PlanetCard)
                {
                    var planet = new MotelyItem(item.Value).GetPlanet();
                    if (planet == clause.PlanetEnum.Value)
                    {
                        tally++;
                        if (earlyExit) return tally;
                    }
                }
            }
        }

        // Check pack slots
        if (clause.Sources?.PackSlots?.Length > 0)
        {
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: false);
            var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
            var packSlots = clause.Sources.PackSlots;
            int packCount = (clause.MaxPackSlot ?? (packSlots.Length > 0 ? packSlots.Max() : 0)) + 1;
            
            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                {
                    var contents = ctx.GetNextCelestialPackContents(ref planetStream, pack.GetPackSize());
                    
                    if (packSlots.Contains(i) && 
                        !(clause.Sources.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega))
                    {
                        for (int j = 0; j < contents.Length; j++)
                        {
                            if (contents[j].Type == (MotelyItemType)clause.PlanetEnum.Value)
                            {
                                tally++;
                                if (earlyExit) return tally;
                            }
                        }
                    }
                }
            }
        }
        
        return tally;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountSpectralOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, bool earlyExit = false)
    {
        bool searchAnySpectral = !clause.SpectralEnum.HasValue;
        Debug.Assert(clause.SpectralEnum.HasValue, "CountSpectralOccurrences requires SpectralEnum");
        int tally = 0;

        // Check shop slots
        if (clause.Sources?.ShopSlots?.Length > 0)
        {
            var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
            var shopSlots = clause.Sources.ShopSlots;
            int maxSlot = clause.MaxShopSlot ?? (shopSlots.Length > 0 ? shopSlots.Max() : 0);
            
            for (int i = 0; i <= maxSlot; i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (shopSlots.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                {
                    if (searchAnySpectral)
                    {
                        tally++;
                        if (earlyExit) return tally;
                    }
                    else
                    {
                        var spectral = new MotelyItem(item.Value).GetSpectral();
                        if (spectral == clause.SpectralEnum.Value)
                        {
                            tally++;
                            if (earlyExit) return tally;
                        }
                    }
                }
            }
        }

        // Check pack slots
        if (clause.Sources?.PackSlots?.Length > 0)
        {
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: false);
            var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
            var packSlots = clause.Sources.PackSlots;
            int packCount = (clause.MaxPackSlot ?? (packSlots.Length > 0 ? packSlots.Max() : 0)) + 1;
            
            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
                    
                    if (packSlots.Contains(i) && 
                        !(clause.Sources.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega))
                    {
                        for (int j = 0; j < contents.Length; j++)
                        {
                            var item = contents[j];
                            if (item.Type == MotelyItemType.Soul || item.Type == MotelyItemType.BlackHole)
                            {
                                if (searchAnySpectral ||
                                    (item.Type == MotelyItemType.Soul && clause.SpectralEnum == MotelySpectralCard.Soul) ||
                                    (item.Type == MotelyItemType.BlackHole && clause.SpectralEnum == MotelySpectralCard.BlackHole))
                                {
                                    tally++;
                                    if (earlyExit) return tally;
                                }
                            }
                            else if (item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                            {
                                if (searchAnySpectral)
                                {
                                    tally++;
                                    if (earlyExit) return tally;
                                }
                                else
                                {
                                    var spectral = new MotelyItem(item.Value).GetSpectral();
                                    if (spectral == clause.SpectralEnum.Value)
                                    {
                                        tally++;
                                        if (earlyExit) return tally;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        return tally;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountPlayingCardOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, bool earlyExit = false)
    {
        Debug.Assert(clause.SuitEnum.HasValue || clause.RankEnum.HasValue || clause.EnhancementEnum.HasValue || clause.SealEnum.HasValue || clause.EditionEnum.HasValue, 
            "CountPlayingCardOccurrences requires at least one filter criteria");
        Debug.Assert(clause.Sources?.PackSlots != null, "CountPlayingCardOccurrences requires PackSlots");
        
        int tally = 0;
        var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: false);
        var cardStream = ctx.CreateStandardPackCardStream(ante); // Create ONCE before loop
        var packSlots = clause.Sources.PackSlots;
        int packCount = (clause.MaxPackSlot ?? packSlots.Max()) + 1;

        for (int i = 0; i < packCount; i++)
        {
            var pack = ctx.GetNextBoosterPack(ref packStream);
            
            // Always advance stream for Standard packs to maintain PRNG sync
            if (pack.GetPackType() == MotelyBoosterPackType.Standard)
            {
                var contents = ctx.GetNextStandardPackContents(ref cardStream, pack.GetPackSize());
                
                // Only score if this slot is in our filter
                if (packSlots.Contains(i) && 
                    !(clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega))
                {
                    for (int j = 0; j < contents.Length; j++)
                    {
                        var item = contents[j];
                        if (item.TypeCategory == MotelyItemTypeCategory.PlayingCard &&
                            (!clause.SuitEnum.HasValue || item.PlayingCardSuit == clause.SuitEnum.Value) &&
                            (!clause.RankEnum.HasValue || item.PlayingCardRank == clause.RankEnum.Value) &&
                            (!clause.EnhancementEnum.HasValue || item.Enhancement == clause.EnhancementEnum.Value) &&
                            (!clause.SealEnum.HasValue || item.Seal == clause.SealEnum.Value) &&
                            (!clause.EditionEnum.HasValue || item.Edition == clause.EditionEnum.Value))
                        {
                            tally++;
                            if (earlyExit) return tally; // Early exit for filtering
                        }
                    }
                }
            }
        }

        return tally;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountJokerOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState, bool earlyExit = false)
    {
        int tally = 0;
        var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
        var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: false);

        if (clause.Sources?.ShopSlots?.Length > 0)
        {
            var shopSlots = clause.Sources.ShopSlots;
            int maxSlot = clause.MaxShopSlot ?? (shopSlots.Length > 0 ? shopSlots.Max() : 0);
            for (int i = 0; i <= maxSlot; i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (shopSlots.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.Joker)
                {
                    var joker = new MotelyItem(item.Value).GetJoker();
                    var matches = clause.JokerEnum.HasValue ?
                        joker == clause.JokerEnum.Value :
                        CheckWildcardMatch(joker, clause.WildcardEnum);
                    if (matches && CheckEditionAndStickers(item, clause))
                    {
                        // TODO: runState.AddOwnedJoker(item);
                        if (item.Type == MotelyItemType.Showman && clause.JokerEnum == MotelyJoker.Showman)
                        {
                            runState.ActivateShowman();
                        }
                        tally++;
                        if (earlyExit) return tally;
                    }
                }
            }
        }

        if (clause.Sources?.PackSlots?.Length > 0)
        {
            var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
            var packSlots = clause.Sources.PackSlots;
            int maxPackSlot = clause.MaxPackSlot ?? (packSlots.Length > 0 ? packSlots.Max() : 0);
            for (int i = 0; i <= maxPackSlot; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                {
                    if (clause.Sources.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                    var contents = ctx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize());
                    for (int j = 0; j < contents.Length; j++)
                    {
                        var item = contents[j];
                        var joker = item.GetJoker();
                        var matches = clause.JokerEnum.HasValue ?
                            joker == clause.JokerEnum.Value :
                            CheckWildcardMatch(joker, clause.WildcardEnum);
                        if (matches && CheckEditionAndStickers(item, clause))
                        {
                            // TODO: runState.AddOwnedJoker(item);
                            if (item.Type == MotelyItemType.Showman && clause.JokerEnum == MotelyJoker.Showman)
                            {
                                runState.ActivateShowman();
                            }
                            tally++;
                            if (earlyExit) return tally;
                        }
                    }
                }
            }
        }

        return tally;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountSoulJokerOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState, bool earlyExit = false)
    {
        int tally = 0;
        var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: false);
        var soulStream = ctx.CreateSoulJokerStream(ante, MotelyJokerStreamFlags.Default);
        bool soulStreamInit = false;

        var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 };
        int packCount = packSlots.Length > 0 ? (clause.MaxPackSlot ?? (packSlots.Length > 0 ? packSlots.Max() : 0)) + 1 : (ante == 1 ? 4 : 6);

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
                    hasSoul = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize());
                }
                else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: true);
                    hasSoul = ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize());
                }

                if (hasSoul)
                {
                    // Check if this soul pack has already been consumed by another clause
                    // TODO: if (runState.IsSoulPackConsumed(ante, i)) continue;

                    if (!soulStreamInit)
                    {
                        soulStreamInit = true;
                    }

                    // Get the soul joker and check if it matches
                    var soulJoker = ctx.GetNextJoker(ref soulStream);

                    if (!clause.JokerEnum.HasValue || soulJoker.Type == new MotelyItem(clause.JokerEnum.Value).Type)
                    {
                        // Check edition and stickers if specified
                        if (CheckEditionAndStickers(soulJoker, clause))
                        {
                            // TODO: runState.MarkSoulPackConsumed(ante, i);
                            // TODO: runState.AddOwnedJoker(soulJoker);

                            if (soulJoker.Type == MotelyItemType.Showman && clause.JokerEnum == MotelyJoker.Showman)
                            {
                                runState.ActivateShowman();
                            }

                            tally++;
                            if (earlyExit) return tally; // Early exit for Must clauses
                        }
                    }
                }
            }
        }

        return tally;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountVoucherOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, ref MotelyRunState voucherState)
    {
        if (!clause.VoucherEnum.HasValue) return 0;

        // Simple: just check if the voucher is active (it was activated during ActivateAllVouchers)
        if (voucherState.IsVoucherActive(clause.VoucherEnum.Value))
        {
            // DebugLogger.Log($"[VoucherScoring] {clause.VoucherEnum.Value} is active, giving 1 point"); // DISABLED FOR PERFORMANCE
            return 1;
        }

        return 0;
    }

    #endregion

    #region Helper Functions

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CheckWildcardMatch(MotelyJoker joker, MotelyJsonConfigWildcards? wildcard)
    {
        if (!wildcard.HasValue) return false;
        if (wildcard == MotelyJsonConfigWildcards.AnyJoker) return true;

        var rarity = (MotelyJokerRarity)((int)joker & Motely.JokerRarityMask);
        return wildcard switch
        {
            MotelyJsonConfigWildcards.AnyCommon => rarity == MotelyJokerRarity.Common,
            MotelyJsonConfigWildcards.AnyUncommon => rarity == MotelyJokerRarity.Uncommon,
            MotelyJsonConfigWildcards.AnyRare => rarity == MotelyJokerRarity.Rare,
            _ => false
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CheckEditionAndStickers(in MotelyItem item, MotelyJsonConfig.MotleyJsonFilterClause clause)
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

    #endregion

    #region Missing Methods - TODO: Implement

    public static void ActivateAllVouchers(ref MotelySingleSearchContext ctx, ref MotelyRunState runState, int maxAnte)
    {
        for (int ante = 1; ante <= maxAnte; ante++)
        {
            var voucher = ctx.GetAnteFirstVoucher(ante, runState);
            runState.ActivateVoucher(voucher);
            // DebugLogger.Log($"[VoucherActivation] Ante {ante}: Activated {voucher}"); // DISABLED FOR PERFORMANCE

            // Special case: Hieroglyph gives a bonus voucher in the SAME ante
            if (voucher == MotelyVoucher.Hieroglyph)
            {
                // Use a voucher stream to get the NEXT voucher (not the first one again)
                var voucherStream = ctx.CreateVoucherStream(ante);
                var bonusVoucher = ctx.GetNextVoucher(ref voucherStream, runState);
                runState.ActivateVoucher(bonusVoucher);
                // DebugLogger.Log($"[VoucherActivation] Ante {ante}: Hieroglyph bonus activated {bonusVoucher}"); // DISABLED FOR PERFORMANCE
            }
        }
    }

    public static bool CheckSingleClause(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, ref MotelyRunState runState)
    {
        Debug.Assert(clause.ItemTypeEnum != MotelyFilterItemType.Voucher, "CheckSingleClause should not be used for Voucher clauses");
        Debug.Assert(clause.EffectiveAntes != null, "CheckSingleClause requires EffectiveAntes");
        Debug.Assert(clause.EffectiveAntes.Length > 0, "CheckSingleClause requires non-empty EffectiveAntes");

        foreach (var ante in clause.EffectiveAntes)
        {
            var found = clause.ItemTypeEnum switch
            {
                MotelyFilterItemType.Joker => CountJokerOccurrences(ref ctx, clause, ante, ref runState, earlyExit: true) > 0,
                MotelyFilterItemType.SoulJoker => CountSoulJokerOccurrences(ref ctx, clause, ante, ref runState, earlyExit: true) > 0,
                MotelyFilterItemType.TarotCard => TarotCardsTally(ref ctx, clause, ante, ref runState, earlyExit: true) > 0,
                MotelyFilterItemType.PlanetCard => CountPlanetOccurrences(ref ctx, clause, ante, earlyExit: true) > 0,
                MotelyFilterItemType.SpectralCard => CountSpectralOccurrences(ref ctx, clause, ante, earlyExit: true) > 0,
                MotelyFilterItemType.SmallBlindTag => CheckTagSingle(ref ctx, clause, ante),
                MotelyFilterItemType.BigBlindTag => CheckTagSingle(ref ctx, clause, ante),
                MotelyFilterItemType.PlayingCard => CountPlayingCardOccurrences(ref ctx, clause, ante, earlyExit: true) > 0,
                MotelyFilterItemType.Boss => throw new NotImplementedException("Boss filtering is not yet implemented. The boss PRNG does not match the actual game behavior."),
                MotelyFilterItemType.Voucher => CheckVoucherSingle(ref ctx, clause, ante, ref runState),
                _ => false
            };

            if (found)
            {
                if (clause.ItemTypeEnum == MotelyFilterItemType.Joker && clause.JokerEnum == MotelyJoker.Showman)
                    runState.ActivateShowman();
                return true;
            }
        }
        return false;
    }
    
    private static bool CheckTagSingle(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante)
    {
        Debug.Assert(clause.TagEnum.HasValue, "CheckTagSingle requires TagEnum");
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

    public static bool CheckVoucherSingle(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState)
    {
        if (!clause.VoucherEnum.HasValue) return false;
        
        // Check if this voucher appears at the specified ante
        var voucher = ctx.GetAnteFirstVoucher(ante, runState);
        if (voucher == clause.VoucherEnum.Value)
        {
            runState.ActivateVoucher(voucher);
            return true;
        }
        
        // Also check if it's already active from a previous ante
        return runState.IsVoucherActive(clause.VoucherEnum.Value);
    }

    public static int CountOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, ref MotelyRunState runState)
    {
        int totalCount = 0;
        foreach (var ante in clause.EffectiveAntes)
        {
            var anteCount = clause.ItemTypeEnum switch
            {
                MotelyFilterItemType.Joker => CountJokerOccurrences(ref ctx, clause, ante, ref runState, earlyExit: false),
                MotelyFilterItemType.SoulJoker => CountSoulJokerOccurrences(ref ctx, clause, ante, ref runState, earlyExit: false),
                MotelyFilterItemType.TarotCard => TarotCardsTally(ref ctx, clause, ante, ref runState, earlyExit: false),
                MotelyFilterItemType.PlanetCard => CountPlanetOccurrences(ref ctx, clause, ante, earlyExit: false),
                MotelyFilterItemType.SpectralCard => CountSpectralOccurrences(ref ctx, clause, ante, earlyExit: false),
                MotelyFilterItemType.SmallBlindTag => CheckTagSingle(ref ctx, clause, ante) ? 1 : 0,
                MotelyFilterItemType.BigBlindTag => CheckTagSingle(ref ctx, clause, ante) ? 1 : 0,
                MotelyFilterItemType.PlayingCard => CountPlayingCardOccurrences(ref ctx, clause, ante, earlyExit: false),
                MotelyFilterItemType.Boss => throw new NotImplementedException("Boss filtering is not yet implemented. The boss PRNG does not match the actual game behavior."),
                MotelyFilterItemType.Voucher => CountVoucherOccurrences(ref ctx, clause, ref runState),
                _ => 0
            };
            totalCount += anteCount;
        }
        return totalCount;
    }

    #endregion
}