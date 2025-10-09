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
        var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3, 4, 5 }; // Default to all 6 pack slots

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
                    if (clause.TarotEnum.HasValue && item.Type == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotEnum.Value))
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
            // IMPORTANT: Ante 0-1 get the guaranteed first Buffoon pack, ante 2+ skip it
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante > 1);
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
                            if (clause.TarotEnum.HasValue && contents[j].Type == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotEnum.Value))
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
    public static int SmallBlindTagTally(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState, bool earlyExit = false)
    {
        Debug.Assert(clause.TagEnum.HasValue, "SmallBlindTagTally requires TagEnum");

        var tagStream = ctx.CreateTagStream(ante);
        var smallBlindTag = ctx.GetNextTag(ref tagStream); // First tag is SmallBlind

        if (clause.TagEnum.HasValue && smallBlindTag == clause.TagEnum.Value)
        {
            return 1; // Found matching SmallBlind tag
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BigBlindTagTally(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState, bool earlyExit = false)
    {
        Debug.Assert(clause.TagEnum.HasValue, "BigBlindTagTally requires TagEnum");

        var tagStream = ctx.CreateTagStream(ante);
        ctx.GetNextTag(ref tagStream); // Skip SmallBlind tag
        var bigBlindTag = ctx.GetNextTag(ref tagStream); // Second tag is BigBlind

        if (clause.TagEnum.HasValue && bigBlindTag == clause.TagEnum.Value)
        {
            return 1; // Found matching BigBlind tag
        }

        return 0;
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
            // JSON slot indices are 0-based, use directly for loop bounds
            int maxSlot = clause.MaxShopSlot.HasValue ? clause.MaxShopSlot.Value : ArrayMax(shopSlots);

            for (int i = 0; i <= maxSlot; i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (ArrayContains(shopSlots, i) && item.TypeCategory == MotelyItemTypeCategory.PlanetCard)
                {
                    if (clause.PlanetEnum.HasValue && item.Type == (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)clause.PlanetEnum.Value))
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
            // IMPORTANT: Ante 0-1 get the guaranteed first Buffoon pack, ante 2+ skip it
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante > 1);
            var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
            var packSlots = clause.Sources.PackSlots;
            int maxPackSlot = ArrayMax(packSlots);
            int packCount = (clause.MaxPackSlot ?? maxPackSlot) + 1;

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
                            if (clause.PlanetEnum.HasValue && contents[j].Type == (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)clause.PlanetEnum.Value))
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
        int tally = 0;

        // Check shop slots
        if (clause.Sources?.ShopSlots?.Length > 0)
        {
            var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
            var shopSlots = clause.Sources.ShopSlots;
            // JSON slot indices are 0-based, use directly for loop bounds
            int maxSlot = clause.MaxShopSlot.HasValue ? clause.MaxShopSlot.Value : ArrayMax(shopSlots);

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
                        if (clause.SpectralEnum.HasValue && item.Type == (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)clause.SpectralEnum.Value))
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
            // IMPORTANT: Ante 0-1 get the guaranteed first Buffoon pack, ante 2+ skip it
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante > 1);
            var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
            var packSlots = clause.Sources.PackSlots;
            int maxPackSlot = ArrayMax(packSlots);
            int packCount = (clause.MaxPackSlot ?? maxPackSlot) + 1;

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
                                    if (clause.SpectralEnum.HasValue && item.Type == (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)clause.SpectralEnum.Value))
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

        // Safety check: StandardCard clauses must have Sources.PackSlots defined
        if (clause.Sources?.PackSlots == null || clause.Sources.PackSlots.Length == 0)
        {
            return 0; // Invalid clause configuration - no pack slots to check
        }

        int tally = 0;
        // IMPORTANT: For ante 2+, we need generatedFirstPack: true to skip the phantom first Buffoon pack!
        var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);
        var cardStream = ctx.CreateStandardPackCardStream(ante); // Create ONCE before loop
        var packSlots = clause.Sources.PackSlots;
        int maxPackSlot = ArrayMax(packSlots);
        int packCount = (clause.MaxPackSlot ?? maxPackSlot) + 1;

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

    /// <summary>
    /// Get default shop slots to check for an ante when user doesn't specify sources
    /// </summary>
    public static int GetDefaultShopSlotsForAnte(int ante)
    {
        // pifreak's rules: ante-based shop slot limits (as players progress they can afford more rerolls)
        if (ante == 0) return 4; // Ante 0: 4 shop slots
        if (ante == 1) return 4; // Ante 1: 4 shop slots
        // Ante 2+: 6 + ante (scaling with player's economic growth)
        return 6 + ante;
    }

    /// <summary>
    /// Get default pack slots to check for an ante when user doesn't specify sources
    /// </summary>
    public static int GetDefaultPackSlotsForAnte(int ante)
    {
        // Standard ante-based pack slot limits (matches ante == 1 ? 4 : 6 pattern used throughout codebase)
        return (ante == 0 || ante == 1) ? 4 : 6; // Antes 0-1: 4 slots [0,1,2,3], Ante 2+: 6 slots [0,1,2,3,4,5]
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountJokerOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonJokerFilterClause clause, int ante, ref MotelyRunState runState, bool earlyExit = false, MotelyJsonConfig.MotleyJsonFilterClause? originalClause = null)
    {
        int tally = 0;
        var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
        // IMPORTANT: For ante 2+, we need generatedFirstPack: true to skip the phantom first Buffoon pack!
        var packStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);

        // Determine if user specified slots or if we should use ante-based defaults
        bool hasShopSlots = clause.WantedShopSlots.Any(s => s);
        bool hasPackSlots = clause.WantedPackSlots != null && clause.WantedPackSlots.Any(x => x);
        bool useDefaults = !hasShopSlots && !hasPackSlots;

        // Check shop slots if any are wanted OR if using defaults
        if (hasShopSlots || useDefaults)
        {
            // Determine max slot to check
            int maxSlot;
            if (useDefaults)
            {
                // Use ante-based default
                maxSlot = GetDefaultShopSlotsForAnte(ante);
            }
            else
            {
                // Find the highest slot user specified
                maxSlot = 0;
                for (int i = clause.WantedShopSlots.Length - 1; i >= 0; i--)
                {
                    if (clause.WantedShopSlots[i])
                    {
                        maxSlot = i + 1;
                        break;
                    }
                }
            }

            // Process shop slots - must read ALL slots up to max to keep stream in sync
            for (int i = 0; i < maxSlot; i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                // Check if this slot should be checked (either explicitly wanted OR using defaults)
                bool checkThisSlot = useDefaults || clause.WantedShopSlots[i];
                if (checkThisSlot && item.TypeCategory == MotelyItemTypeCategory.Joker)
                {
                    // FIXED: Handle both single joker AND multi-value joker arrays
                    bool matches = false;
                    
                    if (!clause.IsWildcard)
                    {
                        if (clause.JokerTypes != null && clause.JokerTypes.Count > 0)
                        {
                            // Multi-value: Check if item matches ANY of the specified jokers
                            foreach (var jokerType in clause.JokerTypes)
                            {
                                var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)jokerType);
                                if (item.Type == targetType)
                                {
                                    matches = true;
                                    break;
                                }
                            }
                        }
                        else if (clause.JokerType.HasValue)
                        {
                            // Single value: original logic
                            matches = item.Type == (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)clause.JokerType.Value);
                        }
                    }
                    else
                    {
                        // Wildcard matching
                        matches = CheckWildcardMatch((MotelyJoker)item.Type, originalClause?.WildcardEnum ?? clause.WildcardEnum);
                    }
                    // FIXED: Check edition using the clause directly which now has EditionEnum and StickerEnums
                    if (matches && CheckEditionAndStickers(item, clause))
                    {
                        runState.AddOwnedJoker((MotelyJoker)item.Type);
                        if (item.Type == MotelyItemType.Showman && clause.JokerType.HasValue && clause.JokerType.Value == MotelyJoker.Showman)
                        {
                            runState.ActivateShowman();
                        }
                        tally++;
                        if (earlyExit) return tally;
                    }
                }
            }
        }

        // Use array for pack slot checking (check if explicitly wanted OR using defaults)
        if ((clause.WantedPackSlots != null && clause.WantedPackSlots.Any(x => x)) || useDefaults)
        {
            var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
            Debug.Assert(!buffoonStream.RarityPrngStream.IsInvalid, $"BuffoonStream RarityPrng should be valid for ante {ante}");

            // Determine max pack slots to check
            int maxPackSlots = useDefaults ? GetDefaultPackSlotsForAnte(ante) : 6;

            // Process pack slots using simple array lookup
            for (int i = 0; i < maxPackSlots; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                // Check if this pack slot should be checked
                bool checkThisPack = useDefaults || (clause.WantedPackSlots != null && clause.WantedPackSlots[i]);
                if (checkThisPack && pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                {
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                    var contents = ctx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize());
                    for (int j = 0; j < contents.Length; j++)
                    {
                        var item = contents[j];
                        // FIXED: Handle both single joker AND multi-value joker arrays (pack slots)
                        bool matches = false;
                        
                        if (!clause.IsWildcard)
                        {
                            if (clause.JokerTypes != null && clause.JokerTypes.Count > 0)
                            {
                                // Multi-value: Check if item matches ANY of the specified jokers
                                foreach (var jokerType in clause.JokerTypes)
                                {
                                    var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)jokerType);
                                    if (item.Type == targetType)
                                    {
                                        matches = true;
                                        break;
                                    }
                                }
                            }
                            else if (clause.JokerType.HasValue)
                            {
                                // Single value: original logic
                                matches = item.Type == (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)clause.JokerType.Value);
                            }
                        }
                        else
                        {
                            // Wildcard matching
                            matches = CheckWildcardMatch((MotelyJoker)item.Type, originalClause?.WildcardEnum ?? clause.WildcardEnum);
                        }
                        // FIXED: Check edition using the clause directly which now has EditionEnum and StickerEnums
                        if (matches && CheckEditionAndStickers(item, clause))
                        {
                            runState.AddOwnedJoker((MotelyJoker)item.Type);
                            if (item.Type == MotelyItemType.Showman && clause.JokerType.HasValue && clause.JokerType.Value == MotelyJoker.Showman)
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
    public static int CountVoucherOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, ref MotelyRunState voucherState)
    {
        if (!clause.VoucherEnum.HasValue) return 0;

        // Build voucher state progressively - simulate consuming vouchers as we check each ante
        // This allows Petroglyph to appear in later antes when Hieroglyph was consumed in ante 1
        var progressiveState = new MotelyRunState();
        int count = 0;

        foreach (var ante in clause.EffectiveAntes)
        {
            var voucherAtAnte = ctx.GetAnteFirstVoucher(ante, progressiveState);
            if (voucherAtAnte == clause.VoucherEnum.Value)
            {
                count++;
                // DebugLogger.Log($"[VoucherScoring] {clause.VoucherEnum.Value} found at ante {ante}, count={count}"); // DISABLED FOR PERFORMANCE
            }

            // Consume the voucher to update state for next ante check
            progressiveState.ActivateVoucher(voucherAtAnte);

            // Handle Hieroglyph bonus voucher
            if (voucherAtAnte == MotelyVoucher.Hieroglyph)
            {
                var voucherStream = ctx.CreateVoucherStream(ante);
                var bonusVoucher = ctx.GetNextVoucher(ref voucherStream, progressiveState);
                progressiveState.ActivateVoucher(bonusVoucher);
            }
        }

        return count;
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
        // Special case for AND - all nested clauses must match
        if (clause.ItemTypeEnum == MotelyFilterItemType.And)
        {
            if (clause.Clauses == null || clause.Clauses.Count == 0)
                return false; // Empty And clause fails

            // Check if ALL nested clauses match (early exit if any fails)
            foreach (var nestedClause in clause.Clauses)
            {
                if (!CheckSingleClause(ref ctx, nestedClause, ref runState))
                    return false; // One clause failed, entire And fails
            }

            // All clauses passed
            return true;
        }

        // Special case for OR - at least one nested clause must match
        if (clause.ItemTypeEnum == MotelyFilterItemType.Or)
        {
            if (clause.Clauses == null || clause.Clauses.Count == 0)
                return false; // Empty Or clause fails

            // Check if ANY nested clause matches (early exit on first match)
            foreach (var nestedClause in clause.Clauses)
            {
                if (CheckSingleClause(ref ctx, nestedClause, ref runState))
                    return true; // One clause succeeded, entire Or succeeds
            }

            // No clauses passed
            return false;
        }

        // Vouchers are handled by CheckVoucherSingle in the switch statement below
        Debug.Assert(clause.EffectiveAntes != null, "CheckSingleClause requires EffectiveAntes");
        Debug.Assert(clause.EffectiveAntes.Length > 0, "CheckSingleClause requires non-empty EffectiveAntes");

        foreach (var ante in clause.EffectiveAntes)
        {
            // Use the SAME logic as CountClause but with earlyExit optimization for MUST
            var count = clause.ItemTypeEnum switch
            {
                MotelyFilterItemType.Joker => CountJokerOccurrences(ref ctx, MotelyJsonJokerFilterClause.FromJsonClause(clause), ante, ref runState, earlyExit: true, originalClause: clause),
                MotelyFilterItemType.SoulJoker => CheckSoulJokerForSeed(new List<MotelyJsonSoulJokerFilterClause> { MotelyJsonSoulJokerFilterClause.FromJsonClause(clause) }, ref ctx, earlyExit: true) ? 1 : 0,
                MotelyFilterItemType.TarotCard => TarotCardsTally(ref ctx, clause, ante, ref runState, earlyExit: false),
                MotelyFilterItemType.PlanetCard => CountPlanetOccurrences(ref ctx, clause, ante, earlyExit: false),
                MotelyFilterItemType.SpectralCard => CountSpectralOccurrences(ref ctx, clause, ante, earlyExit: false),
                MotelyFilterItemType.SmallBlindTag => CountTagOccurrences(ref ctx, clause, ante),
                MotelyFilterItemType.BigBlindTag => CountTagOccurrences(ref ctx, clause, ante),
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

    /// <summary>
    /// COUNTS how many times a tag appears (0, 1, or 2 for generic "tag" type)
    /// CRITICAL FIX: Generic "tag" type should tally BOTH small and big blind!
    /// </summary>
    public static int CountTagOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante)
    {
        Debug.Assert(clause.TagEnum.HasValue || (clause.TagEnums != null && clause.TagEnums.Count > 0), "CountTagOccurrences requires TagEnum or TagEnums");
        var tagStream = ctx.CreateTagStream(ante);
        var smallTag = ctx.GetNextTag(ref tagStream);
        var bigTag = ctx.GetNextTag(ref tagStream);

        int count = 0;

        // Handle multi-value OR logic - still returns 0 or 1 for multi-value
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
                if (matches) return 1; // Found at least one match
            }
            return 0;
        }
        // Handle single value - COUNT occurrences!
        else if (clause.TagEnum.HasValue)
        {
            // For specific SmallBlind or BigBlind type, only check that slot
            if (clause.TagTypeEnum == MotelyTagType.SmallBlind)
            {
                return smallTag == clause.TagEnum.Value ? 1 : 0;
            }
            else if (clause.TagTypeEnum == MotelyTagType.BigBlind)
            {
                return bigTag == clause.TagEnum.Value ? 1 : 0;
            }
            else // TagTypeEnum.Any - CHECK BOTH!
            {
                if (smallTag == clause.TagEnum.Value) count++;
                if (bigTag == clause.TagEnum.Value) count++;
                return count; // Can be 0, 1, or 2!
            }
        }

        return 0;
    }

    public static bool CheckBossSingle(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState)
    {
        if (!clause.BossEnum.HasValue) return false;

        try
        {
            // Use cached bosses if available (for scoring)
            if (runState.CachedBosses != null && ante >= 0 && ante < runState.CachedBosses.Length)
            {
                return runState.CachedBosses[ante] == clause.BossEnum.Value;
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

        // Special case for AND - gates with nested scoring
        // Example: Tag (score=0, acts as gate) + Jokers (score=100 each)
        // Ante 2: Tag ✅ + 3 Jokers → 3 × 100 = 300 points
        // Ante 5: Tag ✅ + 2 Jokers → 2 × 100 = 200 points
        // Total: 500 points (Tag just gates, Joker scoring propagates through)
        if (clause.ItemTypeEnum == MotelyFilterItemType.And)
        {
            if (clause.Clauses == null || clause.Clauses.Count == 0)
                return 0; // Empty And clause scores 0

            // Get the union of all antes from nested clauses
            var allAntes = new HashSet<int>();
            foreach (var nestedClause in clause.Clauses)
            {
                if (nestedClause.EffectiveAntes != null)
                {
                    foreach (var ante in nestedClause.EffectiveAntes)
                        allAntes.Add(ante);
                }
            }

            if (allAntes.Count == 0)
                return 0; // No antes to check

            // For each ante, check if ALL nested clauses match
            // If they do, sum their WEIGHTED counts (count × score)
            int andTotalCount = 0;
            foreach (var ante in allAntes)
            {
                bool allMatch = true;
                var anteCounts = new List<(int count, int score)>();

                // CRITICAL: Create ONE soul stream for this ante to share across all soul joker clauses
                MotelySingleJokerFixedRarityStream? sharedSoulStream = null;
                bool soulStreamCreated = false;

                // Check each nested clause for THIS SPECIFIC ante
                foreach (var nestedClause in clause.Clauses)
                {
                    // Skip if this clause doesn't apply to this ante
                    if (nestedClause.EffectiveAntes == null || !nestedClause.EffectiveAntes.Contains(ante))
                    {
                        allMatch = false;
                        break;
                    }

                    // Check if this clause matches in this specific ante and get the COUNT
                    int anteCount;

                    if (nestedClause.ItemTypeEnum == MotelyFilterItemType.SoulJoker)
                    {
                        // For soul jokers in AND clauses, use shared stream
                        if (!soulStreamCreated)
                        {
                            sharedSoulStream = ctx.CreateSoulJokerStream(ante);
                            soulStreamCreated = true;
                        }
                        var stream = sharedSoulStream!.Value;
                        anteCount = CountSoulJokerOccurrencesForAnteWithStream(ref ctx, ref stream, nestedClause, ante, ref runState);
                        sharedSoulStream = stream; // Update the nullable with modified stream
                    }
                    else
                    {
                        anteCount = nestedClause.ItemTypeEnum switch
                        {
                            MotelyFilterItemType.Joker => CountJokerOccurrences(ref ctx, MotelyJsonJokerFilterClause.FromJsonClause(nestedClause), ante, ref runState, earlyExit: false, originalClause: nestedClause),
                            MotelyFilterItemType.TarotCard => TarotCardsTally(ref ctx, nestedClause, ante, ref runState, earlyExit: false),
                            MotelyFilterItemType.PlanetCard => CountPlanetOccurrences(ref ctx, nestedClause, ante, earlyExit: false),
                            MotelyFilterItemType.SpectralCard => CountSpectralOccurrences(ref ctx, nestedClause, ante, earlyExit: false),
                            MotelyFilterItemType.SmallBlindTag => CountTagOccurrences(ref ctx, nestedClause, ante),
                            MotelyFilterItemType.BigBlindTag => CountTagOccurrences(ref ctx, nestedClause, ante),
                            MotelyFilterItemType.PlayingCard => CountPlayingCardOccurrences(ref ctx, nestedClause, ante, earlyExit: false),
                            MotelyFilterItemType.Boss => CheckBossSingle(ref ctx, nestedClause, ante, ref runState) ? 1 : 0,
                            MotelyFilterItemType.Voucher => CheckVoucherSingle(ref ctx, nestedClause, ante, ref runState) ? 1 : 0,
                            MotelyFilterItemType.And or MotelyFilterItemType.Or => CountOccurrences(ref ctx, nestedClause, ref runState), // Recursive for nested And/Or
                            _ => 0
                        };
                    }

                    if (anteCount == 0)
                    {
                        allMatch = false;
                        break; // This nested clause failed for this ante - gate closed
                    }

                    // Store count and score for this nested clause
                    anteCounts.Add((anteCount, nestedClause.Score));
                }

                // If ALL nested clauses matched for this ante, get max tally
                if (allMatch)
                {
                    // Get max tally from non-gate clauses (score > 0)
                    int maxTally = 0;
                    foreach (var (count, score) in anteCounts)
                    {
                        if (score > 0 && count > maxTally) maxTally = count;
                    }
                    andTotalCount += maxTally;
                }
            }

            // Apply Min threshold if specified - only count if we meet the minimum
            if (clause.Min.HasValue && andTotalCount < clause.Min.Value)
                return 0; // Count doesn't meet minimum threshold

            return andTotalCount;
        }

        // Special case for OR - at least one nested clause must match
        // Returns the MAXIMUM count across all nested clauses (best option)
        if (clause.ItemTypeEnum == MotelyFilterItemType.Or)
        {
            if (clause.Clauses == null || clause.Clauses.Count == 0)
                return 0; // Empty Or clause scores 0

            int maxCount = 0;

            // Check if ANY nested clause matches and find the maximum count
            foreach (var nestedClause in clause.Clauses)
            {
                int nestedCount = CountOccurrences(ref ctx, nestedClause, ref runState);
                maxCount = Math.Max(maxCount, nestedCount);
            }

            // Apply Min threshold if specified - only count if we meet the minimum
            if (clause.Min.HasValue && maxCount < clause.Min.Value)
                return 0; // Count doesn't meet minimum threshold

            return maxCount;
        }

        // And/Or should have already been handled above - this should never happen
        if (clause.ItemTypeEnum == MotelyFilterItemType.And || clause.ItemTypeEnum == MotelyFilterItemType.Or)
        {
            throw new InvalidOperationException("And/Or clauses should be handled earlier in CountOccurrences");
        }

        // EffectiveAntes can be null for some clause types (e.g., StandardCard in should[])
        if (clause.EffectiveAntes == null || clause.EffectiveAntes.Length == 0)
        {
            return 0; // No antes to check
        }

        int totalCount = 0;

        // Soul jokers are special - they need to be counted across ALL antes with ONE stream
        if (clause.ItemTypeEnum == MotelyFilterItemType.SoulJoker)
        {
            totalCount = CountSoulJokerOccurrencesForAllAntes(ref ctx, clause, ref runState);
        }
        else
        {
            // For all other items, count per ante
            foreach (var ante in clause.EffectiveAntes)
            {
                var anteCount = clause.ItemTypeEnum switch
                {
                    MotelyFilterItemType.Joker => CountJokerOccurrences(ref ctx, MotelyJsonJokerFilterClause.FromJsonClause(clause), ante, ref runState, earlyExit: false, originalClause: clause),
                    MotelyFilterItemType.TarotCard => TarotCardsTally(ref ctx, clause, ante, ref runState, earlyExit: false),
                    MotelyFilterItemType.PlanetCard => CountPlanetOccurrences(ref ctx, clause, ante, earlyExit: false),
                    MotelyFilterItemType.SpectralCard => CountSpectralOccurrences(ref ctx, clause, ante, earlyExit: false),
                    MotelyFilterItemType.SmallBlindTag => CountTagOccurrences(ref ctx, clause, ante),
                    MotelyFilterItemType.BigBlindTag => CountTagOccurrences(ref ctx, clause, ante),
                    MotelyFilterItemType.PlayingCard => CountPlayingCardOccurrences(ref ctx, clause, ante, earlyExit: false),
                    MotelyFilterItemType.Boss => CheckBossSingle(ref ctx, clause, ante, ref runState) ? 1 : 0,
                    _ => 0
                };
                totalCount += anteCount;
            }
        }

        // Apply Min threshold if specified - only count if we meet the minimum
        if (clause.Min.HasValue && totalCount < clause.Min.Value)
            return 0; // Count doesn't meet minimum threshold

        return totalCount;
    }
    
    /// <summary>
    /// Count soul joker occurrences - can find multiple soul jokers in a single seed
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountSoulJokerOccurrences(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, ref MotelyRunState runState)
    {
        var soulClause = MotelyJsonSoulJokerFilterClause.FromJsonClause(clause);
        int totalCount = 0;

        // CRITICAL: Soul joker stream is GLOBAL - create it ONCE and walk through ALL antes!
        // The stream is NOT tied to a specific ante
        // As you find Soul cards across all antes, you advance the stream sequentially

        // Sort antes to walk them in order
        var antes = (clause.EffectiveAntes ?? Array.Empty<int>()).OrderBy(a => a).ToArray();
        int minAnte = antes.Length > 0 ? antes[0] : 1;
        var soulStream = ctx.CreateSoulJokerStream(minAnte);

        foreach (var ante in antes)
        {
            totalCount += CountSoulJokerInPacksForAnteWithStream(ref ctx, soulClause, ref soulStream, ante, ref runState);
        }

        return totalCount;
    }

    /// <summary>
    /// Count how many soul jokers match the criteria in packs for a specific ante
    /// CRITICAL: Advances the soul joker stream for EACH Soul card found (sequence!)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountSoulJokerInPacksForAnteWithStream(ref MotelySingleSearchContext ctx, MotelyJsonSoulJokerFilterClause soulClause, ref MotelySingleJokerFixedRarityStream soulStream, int ante, ref MotelyRunState runState)
    {
        // Create pack streams
        var boosterPackStream = ctx.CreateBoosterPackStream(ante, ante > 1, false);
        var tarotStream = ctx.CreateArcanaPackTarotStream(ante, false);
        var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, false);

        bool tarotStreamInit = false, spectralStreamInit = false;
        int matchCount = 0;

        // Calculate max pack slot
        int maxPackSlot = soulClause.MaxPackSlot.HasValue ? (soulClause.MaxPackSlot.Value + 1) : (ante == 0 || ante == 1) ? 4 : 6;

        // Walk through each pack slot and count Soul cards that match
        for (int packIndex = 0; packIndex < maxPackSlot; packIndex++)
        {
            var pack = ctx.GetNextBoosterPack(ref boosterPackStream);

            // Check if The Soul card exists in this pack
            bool hasSoul = false;
            if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
            {
                if (!tarotStreamInit)
                {
                    tarotStreamInit = true;
                    tarotStream = ctx.CreateArcanaPackTarotStream(ante, true);
                }
                hasSoul = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize());
            }
            else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
            {
                if (!spectralStreamInit)
                {
                    spectralStreamInit = true;
                    spectralStream = ctx.CreateSpectralPackSpectralStream(ante, true);
                }
                hasSoul = ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize());
            }

            // If this pack has The Soul, get the NEXT soul joker from the stream
            if (hasSoul)
            {
                // CRITICAL FIX: Get the soul joker for THIS specific Soul card (advances stream)
                var soulJoker = ctx.GetNextJoker(ref soulStream);


                // Check if clause wants this pack slot
                if (soulClause.WantedPackSlots != null && soulClause.WantedPackSlots.Any(x => x) && (packIndex >= soulClause.WantedPackSlots.Length || !soulClause.WantedPackSlots[packIndex]))
                    continue;

                // Check mega requirement
                if (soulClause.RequireMega && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                    continue;

                // Check joker type - handle both single value and values array
                if (!soulClause.IsWildcard)
                {
                    bool jokerMatches = false;

                    // Check single value
                    if (soulClause.JokerType.HasValue)
                    {
                        var expectedType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)soulClause.JokerType.Value);
                        jokerMatches = soulJoker.Type == expectedType;
                    }
                    // Check values array (for "values": ["Perkeo", "Triboulet"])
                    else if (soulClause.JokerTypes != null && soulClause.JokerTypes.Count > 0)
                    {
                        foreach (var jokerEnum in soulClause.JokerTypes)
                        {
                            var expectedType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)jokerEnum);
                            if (soulJoker.Type == expectedType)
                            {
                                jokerMatches = true;
                                break;
                            }
                        }
                    }

                    if (!jokerMatches)
                        continue;
                }
                // If IsWildcard is true (e.g., "Any"), accept any soul joker

                // Check edition
                if (soulClause.EditionEnum.HasValue && soulJoker.Edition != soulClause.EditionEnum.Value)
                    continue;

                // All requirements met - count this Soul card!
                matchCount++;
            }
        }

        return matchCount;
    }

    /// <summary>
    /// Count soul joker occurrences across ALL antes with proper stream walking
    /// </summary>
    private static int CountSoulJokerOccurrencesForAllAntes(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, ref MotelyRunState runState)
    {
        var soulClause = MotelyJsonSoulJokerFilterClause.FromJsonClause(clause);

        // Get all antes this clause cares about
        var effectiveAntes = clause.EffectiveAntes ?? Array.Empty<int>();
        if (effectiveAntes.Length == 0) return 0;

        int totalCount = 0;

        // Process each ante independently (edition is ante-dependent!)
        foreach (int ante in effectiveAntes)
        {
            // Create soul stream for THIS SPECIFIC ANTE
            var soulStream = ctx.CreateSoulJokerStream(ante);

            totalCount += CountSoulJokerInPacksForAnteWithStream(ref ctx, soulClause, ref soulStream, ante, ref runState);
        }

        return totalCount;
    }

    /// <summary>
    /// Count soul joker occurrences - delegates to proper implementation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountSoulJokerOccurrencesForAnte(ref MotelySingleSearchContext ctx, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState)
    {
        // Soul jokers are special - the stream is GLOBAL across all antes
        // We can't count them per-ante, we need to count across ALL antes
        return CountSoulJokerOccurrencesForAllAntes(ref ctx, clause, ref runState);
    }

    /// <summary>
    /// Count soul joker occurrences using an existing soul joker stream - for AND clauses
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountSoulJokerOccurrencesForAnteWithStream(ref MotelySingleSearchContext ctx, ref MotelySingleJokerFixedRarityStream soulStream, MotelyJsonConfig.MotleyJsonFilterClause clause, int ante, ref MotelyRunState runState)
    {
        var soulClause = MotelyJsonSoulJokerFilterClause.FromJsonClause(clause);

        // Use the PROVIDED stream instead of creating a new one
        return CountSoulJokerInPacksForAnteWithStream(ref ctx, soulClause, ref soulStream, ante, ref runState);
    }
    
    /// <summary>
    /// Check for soul joker in a specific ante only (not all antes)
    /// </summary>
    private static bool CheckSoulJokerForSpecificAnte(ref MotelySingleSearchContext ctx, MotelyJsonSoulJokerFilterClause clause, int targetAnte, ref MotelyRunState runState)
    {
        // Use the existing CheckSoulJokerForSeed logic but limit to single ante
        var singleAnteClause = new MotelyJsonSoulJokerFilterClause
        {
            JokerType = clause.JokerType,
            JokerItemType = clause.JokerItemType,
            IsWildcard = clause.IsWildcard,
            EditionEnum = clause.EditionEnum,
            RequireMega = clause.RequireMega,
            WantedAntes = new bool[40], // Only the target ante
            WantedPackSlots = clause.WantedPackSlots
        };
        
        // Set only the target ante as wanted
        if (targetAnte >= 0 && targetAnte < singleAnteClause.WantedAntes.Length)
            singleAnteClause.WantedAntes[targetAnte] = true;
            
        // Use the existing robust soul joker checking logic
        return CheckSoulJokerForSeed(new List<MotelyJsonSoulJokerFilterClause> { singleAnteClause }, ref ctx, earlyExit: true);
    }
    
            /// <summary>
        /// SHARED FUNCTION - used by both filter (with earlyExit=true) and scoring (with earlyExit=false)
        /// FIXED: Uses the CORRECT order like PerkeoObservatory - check Soul Joker result FIRST, then verify Soul card exists
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool CheckSoulJokerForSeed(List<MotelyJsonSoulJokerFilterClause> clauses, ref MotelySingleSearchContext searchContext, bool earlyExit = true)
        {
            if (clauses == null || clauses.Count == 0)
                return true; // No clauses to check means success

            try
            {
                int matchedClauses = 0;
                bool[] clauseSatisfied = new bool[clauses.Count];

            // Calculate ante range from clauses (don't hard-code!)
            int minAnte = int.MaxValue, maxAnte = -1;
            foreach (var clause in clauses)
            {
                if (clause?.WantedAntes != null)
                {
                    for (int i = 0; i < clause.WantedAntes.Length; i++)
                    {
                        if (clause.WantedAntes[i])
                        {
                            minAnte = Math.Min(minAnte, i);
                            maxAnte = Math.Max(maxAnte, i);
                        }
                    }
                }
            }

            // If no antes wanted, no requirements to check
            if (minAnte > maxAnte) return true;

            // Loop ANTES first - create streams ONCE per ante, check ALL clauses
            for (int ante = minAnte; ante <= maxAnte; ante++)
            {
                // Skip antes that no clause cares about
                bool anteNeeded = false;
                foreach (var clause in clauses)
                {
                    if (clause?.WantedAntes != null && ante < clause?.WantedAntes?.Length && clause.WantedAntes[ante])
                    {
                        anteNeeded = true;
                        break;
                    }
                }
                if (!anteNeeded) continue;
                
                // CORRECT ORDER: Check Soul Joker result FIRST (like PerkeoObservatory does)
                var soulStream = searchContext.CreateSoulJokerStream(ante);
                var soulJoker = searchContext.GetNextJoker(ref soulStream);
                
                // FIXED: Create pack streams ONCE per ante, OUTSIDE clause loop (like PerkeoObservatory)
                // IMPORTANT: Ante 0-1 get the guaranteed first Buffoon pack, ante 2+ skip it
                var boosterPackStream = searchContext.CreateBoosterPackStream(ante, ante > 1, false);
                var tarotStream = searchContext.CreateArcanaPackTarotStream(ante, false);
                var spectralStream = searchContext.CreateSpectralPackSpectralStream(ante, false);

                // Calculate max pack slot - use the highest MaxPackSlot from all clauses, or default
                int maxPackSlot = ante == 1 ? 4 : 6;
                foreach (var clause in clauses)
                {
                    if (clause?.MaxPackSlot.HasValue == true)
                    {
                        maxPackSlot = Math.Max(maxPackSlot, clause.MaxPackSlot.Value + 1);
                    }
                }
                bool tarotStreamInit = false, spectralStreamInit = false;
                
                // Walk through each pack slot ONCE, checking ALL clauses against each pack
                for (int packIndex = 0; packIndex < maxPackSlot; packIndex++)
                {
                    var pack = searchContext.GetNextBoosterPack(ref boosterPackStream);
                    
                    // Check if The Soul card exists in this pack (shared across all clauses)
                    bool hasSoul = false;
                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        if (!tarotStreamInit)
                        {
                            tarotStreamInit = true;
                            tarotStream = searchContext.CreateArcanaPackTarotStream(ante, true);
                        }
                        hasSoul = searchContext.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize());
                    }
                    else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                    {
                        if (!spectralStreamInit)
                        {
                            spectralStreamInit = true;
                            spectralStream = searchContext.CreateSpectralPackSpectralStream(ante, true);
                        }
                        hasSoul = searchContext.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize());
                    }
                    
                    // If this pack has The Soul, check ALL clauses that want this pack slot
                    if (hasSoul)
                    {
                        for (int clauseIdx = 0; clauseIdx < clauses.Count; clauseIdx++)
                        {
                            var clause = clauses[clauseIdx];
                            
                            // Skip if clause doesn't care about this ante
                            if (ante >= clause.WantedAntes.Length || !clause.WantedAntes[ante]) continue;
                            
                            // Skip if already satisfied
                            if (clauseSatisfied[clauseIdx]) continue;
                            
                            // Check if this clause wants this pack slot
                            // DEFENSIVE: WantedPackSlots should never be null per FromJsonClause, but add check anyway
                            if (clause.WantedPackSlots != null && clause.WantedPackSlots.Any(x => x) && (packIndex >= clause.WantedPackSlots.Length || !clause.WantedPackSlots[packIndex]))
                                continue;
                            
                            // Check mega requirement  
                            if (clause.RequireMega && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                                continue;
                            
                            // Check joker type - handle both single value and values array
                            if (!clause.IsWildcard)
                            {
                                bool jokerMatches = false;

                                // Check single value
                                if (clause.JokerType.HasValue)
                                {
                                    var expectedType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)clause.JokerType.Value);
                                    jokerMatches = soulJoker.Type == expectedType;
                                }
                                // Check values array (for "values": ["Perkeo", "Triboulet"])
                                else if (clause.JokerTypes != null && clause.JokerTypes.Count > 0)
                                {
                                    foreach (var jokerEnum in clause.JokerTypes)
                                    {
                                        var expectedType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)jokerEnum);
                                        if (soulJoker.Type == expectedType)
                                        {
                                            jokerMatches = true;
                                            break;
                                        }
                                    }
                                }

                                if (!jokerMatches)
                                    continue;
                            }
                            // If IsWildcard is true (e.g., "Any"), accept any soul joker

                            // Check edition
                            if (clause.EditionEnum.HasValue && soulJoker.Edition != clause.EditionEnum.Value)
                                continue;
                            
                            // All requirements met! This clause is satisfied!
                            clauseSatisfied[clauseIdx] = true;
                            matchedClauses++;

                            if (earlyExit && matchedClauses == clauses.Count)
                                return true; // All clauses satisfied - early exit for filter
                        }
                    }
                }
            }
            
            // For filter: return true only if ALL clauses satisfied
            // For scoring: return count of satisfied clauses (but we return bool for now)
            return matchedClauses == clauses.Count;
            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine($"[DEBUG] NullRef INSIDE CheckSoulJokerForSeed: {ex.Message}");
                Console.WriteLine($"[DEBUG] Full Stack: {ex.StackTrace}");
                Console.WriteLine($"[DEBUG] Clauses count: {clauses?.Count}");
                Console.WriteLine($"[DEBUG] SearchContext is ref struct - can't check null easily");
                throw;
            }
        }


    #endregion
}