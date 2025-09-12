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
    #region Helper Methods for Performance
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ArrayMax(int[] array)
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
    
    #endregion
    
    #region Count Functions for Should Clauses
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TarotCardsTally(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState, bool earlyExit = false)
    {
        Debug.Assert(clause.TarotEnum.HasValue, "TarotCardsTally requires TarotEnum");
        int tally = 0;
        
        // Default sources if not specified
        var shopSlots = clause.Sources?.ShopSlots ?? Array.Empty<int>();
        var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 }; // Default to first 4 pack slots

        // Check shop slots
        if (shopSlots.Length > 0)
        {
            var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
            // JSON slot indices are 0-based, so use them directly for loop bounds
            int maxSlot = clause.MaxShopSlot.HasValue ? clause.MaxShopSlot.Value : ArrayMax(shopSlots);
            
            for (int i = 0; i <= maxSlot; i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (ArrayContains(shopSlots, i) && item.TypeCategory == MotelyItemTypeCategory.TarotCard)
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
        if (packSlots.Length > 0)
        {
            // IMPORTANT: For ante 2+, we need generatedFirstPack: true to skip the phantom first Buffoon pack!
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);
            var tarotStream = ctx.CreateArcanaPackTarotStream(ante); // Create ONCE before loop
            // When no specific slots specified, check more packs to find arcana/spectral packs
            int maxPackSlot = packSlots.Length > 0 ? ArrayMax(packSlots) : 5; // Check up to 6 packs by default
            int packCount = (clause.MaxPackSlot ?? maxPackSlot) + 1;
            
            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                // Always advance stream for Arcana packs to maintain PRNG sync
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                    
                    // Only score if this slot is in our filter
                    if (ArrayContains(packSlots, i) && 
                        !(clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega))
                    {
                        for (int j = 0; j < contents.Length; j++)
                        {
                            if (contents[j].GetTarot() == clause.TarotEnum.Value)
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
            // Convert 1-based JSON slot indices to 0-based for loop bounds
            int maxSlot = clause.MaxShopSlot.HasValue ? (clause.MaxShopSlot.Value - 1) : (ArrayMax(shopSlots) - 1);
            
            for (int i = 0; i <= maxSlot; i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (ArrayContains(shopSlots, i) && item.TypeCategory == MotelyItemTypeCategory.PlanetCard)
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
            // IMPORTANT: For ante 2+, we need generatedFirstPack: true to skip the phantom first Buffoon pack!
        var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);
            var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
            var packSlots = clause.Sources.PackSlots;
            int packCount = (clause.MaxPackSlot ?? ArrayMax(packSlots)) + 1;
            
            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                {
                    var contents = ctx.GetNextCelestialPackContents(ref planetStream, pack.GetPackSize());
                    
                    if (ArrayContains(packSlots, i) && 
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
            // Convert 1-based JSON slot indices to 0-based for loop bounds
            int maxSlot = clause.MaxShopSlot.HasValue ? (clause.MaxShopSlot.Value - 1) : (ArrayMax(shopSlots) - 1);
            
            for (int i = 0; i <= maxSlot; i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (ArrayContains(shopSlots, i) && item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
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
            // IMPORTANT: For ante 2+, we need generatedFirstPack: true to skip the phantom first Buffoon pack!
        var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);
            var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
            var packSlots = clause.Sources.PackSlots;
            int packCount = (clause.MaxPackSlot ?? ArrayMax(packSlots)) + 1;
            
            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
                    
                    if (ArrayContains(packSlots, i) && 
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
        // IMPORTANT: For ante 2+, we need generatedFirstPack: true to skip the phantom first Buffoon pack!
        var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);
        var cardStream = ctx.CreateStandardPackCardStream(ante); // Create ONCE before loop
        var packSlots = clause.Sources.PackSlots;
        int packCount = (clause.MaxPackSlot ?? ArrayMax(packSlots)) + 1;

        for (int i = 0; i < packCount; i++)
        {
            var pack = ctx.GetNextBoosterPack(ref packStream);
            
            // Always advance stream for Standard packs to maintain PRNG sync
            if (pack.GetPackType() == MotelyBoosterPackType.Standard)
            {
                var contents = ctx.GetNextStandardPackContents(ref cardStream, pack.GetPackSize());
                
                // Only score if this slot is in our filter
                if (ArrayContains(packSlots, i) && 
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
    public static int CountJokerOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonJokerFilterClause clause, int ante, ref MotelyRunState runState, bool earlyExit = false, MotelyJsonConfig.MotleyJsonFilterClause? originalClause = null)
    {
        int tally = 0;
        var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
        // IMPORTANT: For ante 2+, we need generatedFirstPack: true to skip the phantom first Buffoon pack!
        var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);

        // Use bitmask for efficient shop slot checking
        var shopSlotBitmask = clause.ShopSlotBitmask;
        
        if (shopSlotBitmask != 0)
        {
            // Need to find the highest bit set to know how many slots to check
            int maxSlot = 64 - System.Numerics.BitOperations.LeadingZeroCount(shopSlotBitmask);
            
            // Process shop slots - must read ALL slots up to max to keep stream in sync
            for (int i = 0; i < maxSlot; i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                // Check if this slot is in our bitmask
                if (((shopSlotBitmask >> i) & 1) != 0 && item.TypeCategory == MotelyItemTypeCategory.Joker)
                {
                    var joker = new MotelyItem(item.Value).GetJoker();
                    var matches = !clause.IsWildcard ?
                        joker == clause.JokerType :
                        CheckWildcardMatch(joker, originalClause?.WildcardEnum ?? clause.WildcardEnum);
                    // FIXED: Check edition using the clause directly which now has EditionEnum and StickerEnums
                    if (matches && CheckEditionAndStickers(item, clause))
                    {
                        runState.AddOwnedJoker((MotelyJoker)item.Type);
                        if (item.Type == MotelyItemType.Showman && clause.JokerType == MotelyJoker.Showman)
                        {
                            runState.ActivateShowman();
                        }
                        tally++;
                        if (earlyExit) return tally;
                    }
                }
            }
        }

        // Use bitmask for efficient pack slot checking
        var packSlotBitmask = clause.PackSlotBitmask;
        
        if (packSlotBitmask != 0)
        {
            var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
            Debug.Assert(!buffoonStream.RarityPrngStream.IsInvalid, $"BuffoonStream RarityPrng should be valid for ante {ante}");
            // Process pack slots using bitmask for efficiency
            for (int i = 0; i < 64 && packSlotBitmask != 0; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (((packSlotBitmask >> i) & 1) != 0 && pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                {
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                    var contents = ctx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize());
                    for (int j = 0; j < contents.Length; j++)
                    {
                        var item = contents[j];
                        var joker = item.GetJoker();
                        var matches = !clause.IsWildcard ?
                            joker == clause.JokerType :
                            CheckWildcardMatch(joker, originalClause?.WildcardEnum ?? clause.WildcardEnum);
                        // FIXED: Check edition using the clause directly which now has EditionEnum and StickerEnums
                        if (matches && CheckEditionAndStickers(item, clause))
                        {
                            runState.AddOwnedJoker((MotelyJoker)item.Type);
                            if (item.Type == MotelyItemType.Showman && clause.JokerType == MotelyJoker.Showman)
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
        // Simple approach for scoring: just iterate and count matches
        // No fancy stream management - we know very few seeds get here
        int tally = 0;
        // IMPORTANT: For ante 2+, we need generatedFirstPack: true to skip the phantom first Buffoon pack!
        var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);
        // Don't cache soul stream - we might check multiple packs and need to track position correctly
        var soulStream = ctx.CreateSoulJokerStream(ante, MotelyJokerStreamFlags.Default);

        // Just check the packs we care about
        var packSlots = clause.Sources?.PackSlots ?? (
                ante == 1 ? new[] { 0, 1, 2, 3 } : new[] { 0, 1, 2, 3, 4, 5 }
        ); // Default to first 4 packs for ante 1, first 6 for ante 2+
        int maxPacks = ante == 1 ? 4 : 6;
        
        // Create the tarot stream ONCE for checking all Arcana packs
        var tarotStream = ctx.CreateArcanaPackTarotStream(ante, soulOnly: true, isCached: false);
        var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: true, isCached: false);

        for (int i = 0; i < maxPacks; i++)
        {
            var pack = ctx.GetNextBoosterPack(ref packStream);
            
            // Check cheap conditions first
            bool isPackSlotWeCareAbout = ArrayContains(packSlots, i);
            bool isCorrectPackType = pack.GetPackType() == MotelyBoosterPackType.Arcana || pack.GetPackType() == MotelyBoosterPackType.Spectral;
            
            // We need to check ALL Arcana/Spectral packs to advance the stream properly
            bool hasSoul = false;
            if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
            {
                // Always check to advance the stream properly
                hasSoul = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize());
            }
            else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
            {
                // Always check to advance the stream properly
                hasSoul = ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize());
            }
            
            // Only process packs we care about (cheap checks first, expensive hasSoul check last)
            if (isPackSlotWeCareAbout && isCorrectPackType && hasSoul)
            {
                if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;
                
                {
                    // CRITICAL: Only consume from soul stream when we actually have The Soul card!
                    // Get the soul joker
                    var soulJoker = ctx.GetNextJoker(ref soulStream);
                    
                    // Handle duplicates if Showman isn't active
                    // Soul jokers are legendary, so they reroll when duplicate found
                    if (!runState.ShowmanActive && runState.OwnedJokers.Contains((MotelyJoker)soulJoker.Type))
                    {
                        // Keep getting next soul joker until we find non-duplicate (max 5 attempts)
                        for (int rerollCount = 0; rerollCount < 5 && runState.OwnedJokers.Contains((MotelyJoker)soulJoker.Type); rerollCount++)
                        {
                            soulJoker = ctx.GetNextJoker(ref soulStream);
                        }
                    }
                    
                    #if DEBUG
                    System.Console.WriteLine($"[DEBUG] SoulJoker check - Joker from stream: {soulJoker.GetJoker()}, Clause JokerEnum: {clause.JokerEnum}, Match: {(!clause.JokerEnum.HasValue || soulJoker.GetJoker() == clause.JokerEnum.Value)}");
                    #endif
                    
                    if (!clause.JokerEnum.HasValue || soulJoker.GetJoker() == clause.JokerEnum.Value)
                    {
                        // Check edition and stickers if specified
                        if (CheckEditionAndStickers(soulJoker, clause))
                        {
                            // TODO: runState.MarkSoulPackConsumed(ante, i);
                            runState.AddOwnedJoker((MotelyJoker)soulJoker.Type);

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
    
    /// <summary>
    /// Overload for MotelyJsonJokerFilterClause to properly check edition and stickers
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CheckEditionAndStickers(in MotelyItem item, MotelyJsonJokerFilterClause clause)
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

    #region Helper Methods

    public static void ActivateAllVouchers(ref MotelySingleSearchContext ctx, ref MotelyRunState runState, int maxAnte)
    {
#if DEBUG
        DebugLogger.Log($"[VoucherActivation] Starting activation for maxAnte: {maxAnte}");
#endif
        for (int ante = 1; ante <= maxAnte; ante++)
        {
            var voucher = ctx.GetAnteFirstVoucher(ante, runState);
            runState.ActivateVoucher(voucher);
    #if DEBUG
        DebugLogger.Log($"[VoucherActivation] Ante {ante}: Found {voucher}, activated");
#endif

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
        // Vouchers are handled by CheckVoucherSingle in the switch statement below
        Debug.Assert(clause.EffectiveAntes != null, "CheckSingleClause requires EffectiveAntes");
        Debug.Assert(clause.EffectiveAntes.Length > 0, "CheckSingleClause requires non-empty EffectiveAntes");

        foreach (var ante in clause.EffectiveAntes)
        {
            // Use the SAME logic as CountClause but with earlyExit optimization for MUST
            var count = clause.ItemTypeEnum switch
            {
                MotelyFilterItemType.Joker => CountJokerOccurrences(ref ctx, MotelyJsonJokerFilterClause.FromJsonClause(clause), ante, ref runState, earlyExit: true, originalClause: clause),
                MotelyFilterItemType.SoulJoker => CountSoulJokerOccurrences(ref ctx, clause, ante, ref runState, earlyExit: false),
                MotelyFilterItemType.TarotCard => TarotCardsTally(ref ctx, clause, ante, ref runState, earlyExit: false),
                MotelyFilterItemType.PlanetCard => CountPlanetOccurrences(ref ctx, clause, ante, earlyExit: false),
                MotelyFilterItemType.SpectralCard => CountSpectralOccurrences(ref ctx, clause, ante, earlyExit: false),
                MotelyFilterItemType.SmallBlindTag => CheckTagSingle(ref ctx, clause, ante) ? 1 : 0,
                MotelyFilterItemType.BigBlindTag => CheckTagSingle(ref ctx, clause, ante) ? 1 : 0,
                MotelyFilterItemType.PlayingCard => CountPlayingCardOccurrences(ref ctx, clause, ante, earlyExit: false),
                MotelyFilterItemType.Boss => CheckBossSingle(ref ctx, clause, ante, ref runState) ? 1 : 0,
                MotelyFilterItemType.Voucher => CheckVoucherSingle(ref ctx, clause, ante, ref runState) ? 1 : 0,
                _ => 0
            };
            
            var found = count > 0;

            if (found)
            {
                if (clause.ItemTypeEnum == MotelyFilterItemType.Joker && clause.JokerEnum == MotelyJoker.Showman)
                    runState.ActivateShowman();
                return true;
            }
        }
        return false;
    }
    
    public static bool CheckTagSingle(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante)
    {
        Debug.Assert(clause.TagEnum.HasValue || (clause.TagEnums != null && clause.TagEnums.Count > 0), "CheckTagSingle requires TagEnum or TagEnums");
        var tagStream = ctx.CreateTagStream(ante);
        var smallTag = ctx.GetNextTag(ref tagStream);
        var bigTag = ctx.GetNextTag(ref tagStream);

        // Handle multi-value OR logic
        if (clause.TagEnums != null && clause.TagEnums.Count > 0)
        {
            foreach (var tagEnum in clause.TagEnums)
            {
                bool matches = clause.TagTypeEnum switch
                {
                    MotelyTagType.SmallBlind => smallTag == tagEnum,
                    MotelyTagType.BigBlind => bigTag == tagEnum,
                    _ => smallTag == tagEnum || bigTag == tagEnum
                };
                if (matches) return true;
            }
            return false;
        }
        // Handle single value
        else if (clause.TagEnum.HasValue)
        {
            return clause.TagTypeEnum switch
            {
                MotelyTagType.SmallBlind => smallTag == clause.TagEnum.Value,
                MotelyTagType.BigBlind => bigTag == clause.TagEnum.Value,
                _ => smallTag == clause.TagEnum.Value || bigTag == clause.TagEnum.Value
            };
        }
        
        return false;
    }

    public static bool CheckBossSingle(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState)
    {
        if (!clause.BossEnum.HasValue) return false;
            
        try
        {
            // Use cached bosses if available (for scoring)
            if (runState.CachedBosses != null && ante > 0 && ante <= runState.CachedBosses.Length)
            {
                return runState.CachedBosses[ante - 1] == clause.BossEnum.Value;
            }
            
            // Fallback to generating bosses (for filtering)
            var bossStream = ctx.CreateBossStream();
            MotelyBossBlind boss = ctx.GetBossForAnte(ref bossStream, ante, ref runState);
            return boss == clause.BossEnum.Value;
        }
        catch
        {
            // Boss generation can fail for some seeds
            return false;
        }
    }

    public static bool CheckVoucherSingle(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState)
    {
        if (!clause.VoucherEnum.HasValue) return false;
        
        // IMPORTANT: Check if it's already active from ActivateAllVouchers
        if (runState.IsVoucherActive(clause.VoucherEnum.Value))
            return true;
            
        // Check if this voucher appears at the specified ante
        var voucher = ctx.GetAnteFirstVoucher(ante, runState);
#if DEBUG
        DebugLogger.Log($"[CheckVoucherSingle] Ante {ante}: Looking for {clause.VoucherEnum.Value}, found {voucher}");
#endif
        if (voucher == clause.VoucherEnum.Value)
        {
            runState.ActivateVoucher(voucher);
            return true;
        }
        
        return false;
    }

    public static int CountOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, ref MotelyRunState runState)
    {
        // Special case for vouchers - they're not ante-specific, just check once
        if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher)
        {
            return CountVoucherOccurrences(ref ctx, clause, ref runState);
        }
        
        int totalCount = 0;
        foreach (var ante in clause.EffectiveAntes)
        {
            var anteCount = clause.ItemTypeEnum switch
            {
                MotelyFilterItemType.Joker => CountJokerOccurrences(ref ctx, MotelyJsonJokerFilterClause.FromJsonClause(clause), ante, ref runState, earlyExit: false, originalClause: clause),
                MotelyFilterItemType.SoulJoker => CountSoulJokerOccurrences(ref ctx, clause, ante, ref runState, earlyExit: false),
                MotelyFilterItemType.TarotCard => TarotCardsTally(ref ctx, clause, ante, ref runState, earlyExit: false),
                MotelyFilterItemType.PlanetCard => CountPlanetOccurrences(ref ctx, clause, ante, earlyExit: false),
                MotelyFilterItemType.SpectralCard => CountSpectralOccurrences(ref ctx, clause, ante, earlyExit: false),
                MotelyFilterItemType.SmallBlindTag => CheckTagSingle(ref ctx, clause, ante) ? 1 : 0,
                MotelyFilterItemType.BigBlindTag => CheckTagSingle(ref ctx, clause, ante) ? 1 : 0,
                MotelyFilterItemType.PlayingCard => CountPlayingCardOccurrences(ref ctx, clause, ante, earlyExit: false),
                MotelyFilterItemType.Boss => CheckBossSingle(ref ctx, clause, ante, ref runState) ? 1 : 0,
                _ => 0
            };
            totalCount += anteCount;
        }
        return totalCount;
    }

    #endregion
}