using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on spectral card criteria from JSON configuration.
/// </summary>
public struct MotelyJsonSpectralCardFilterDesc(MotelyJsonSpectralFilterCriteria criteria)
    : IMotelySeedFilterDesc<MotelyJsonSpectralCardFilterDesc.MotelyJsonSpectralCardFilter>
{
    private readonly MotelyJsonSpectralFilterCriteria _criteria = criteria;

    public MotelyJsonSpectralCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Use pre-calculated values from criteria
        int minAnte = _criteria.MinAnte;
        int maxAnte = _criteria.MaxAnte;

        // Cache streams for all antes we'll check
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheBoosterPackStream(ante);
            ctx.CacheShopStream(ante);
        }

        // SINGLE clause only - caller must chain multiple filters for multiple clauses
        if (_criteria.Clauses.Count != 1)
            throw new ArgumentException($"MotelyJsonSpectralCardFilter expects exactly 1 clause, got {_criteria.Clauses.Count}");

        return new MotelyJsonSpectralCardFilter(
            _criteria.Clauses[0],
            minAnte,
            maxAnte,
            _criteria.MaxShopSlotsNeeded
        );
    }

    public struct MotelyJsonSpectralCardFilter(
        MotelyJsonSpectralFilterClause clause,
        int minAnte,
        int maxAnte,
        int maxShopSlotsNeeded
    ) : IMotelySeedFilter
    {
        private readonly MotelyJsonSpectralFilterClause Clause = clause;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;
        private readonly int _maxShopSlotsNeeded = maxShopSlotsNeeded;
        private readonly int _minThreshold = clause.Min ?? 1;

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var clause = Clause;

            // SINGLE clause - track if it matched across all antes
            VectorMask clauseMask = VectorMask.NoBitsSet;

            // Initialize run state for voucher calculations
            var runState = ctx.Deck.GetDefaultRunState();

            // Walk each ante and check the clause
            for (int ante = _minAnte; ante <= _maxAnte; ante++)
            {
                // Skip ante if not wanted
                if (!clause.WantedAntes[ante])
                    continue;

                bool hasShop = HasShopSlots(clause.WantedShopSlots);
                bool hasPack = HasPackSlots(clause.WantedPackSlots);
                bool useDefaults = !hasShop && !hasPack;

                int maxShopSlots = 0;
                int maxPackSlots = 0;

                if (hasShop || useDefaults)
                {
                    maxShopSlots = hasShop
                        ? FindMaxSlotIndex(clause.WantedShopSlots) + 1
                        : MotelyJsonScoring.GetDefaultShopSlotsForAnte(ante);
                }
                if (hasPack || useDefaults)
                {
                    maxPackSlots = hasPack
                        ? FindMaxSlotIndex(clause.WantedPackSlots) + 1
                        : MotelyJsonScoring.GetDefaultPackSlotsForAnte(ante);
                }

                // Create streams ONCE for this ante
                var shopSpectralStream =
                    maxShopSlots > 0 ? ctx.CreateShopSpectralStreamNew(ante) : default;

                // Walk shop slots
                for (int slot = 0; slot < maxShopSlots; slot++)
                {
                    var spectralItem = shopSpectralStream.GetNext(ref ctx);

                    // Check if it's an actual spectral (not excluded) - PURE SIMD!
                    var excludedValue = Vector256.Create(
                        (int)MotelyItemType.SpectralExcludedByStream
                    );
                    VectorMask isActualSpectral = ~Vector256.Equals(
                        spectralItem.Value,
                        excludedValue
                    );

                    if (!isActualSpectral.IsAllFalse())
                    {
                        bool wantsSlot = !hasShop || clause.WantedShopSlots[slot];
                        if (wantsSlot)
                        {
                            // Check type match - PURE SIMD!
                            VectorMask typeMatches = VectorMask.AllBitsSet;
                            if (clause.SpectralTypes?.Count > 0)
                            {
                                VectorMask anyTypeMatch = VectorMask.NoBitsSet;
                                foreach (var spectralType in clause.SpectralTypes)
                                {
                                    var targetType = (MotelyItemType)(
                                        (int)MotelyItemTypeCategory.SpectralCard
                                        | (int)spectralType
                                    );
                                    anyTypeMatch |= VectorEnum256.Equals(
                                        spectralItem.Type,
                                        targetType
                                    );
                                }
                                typeMatches = anyTypeMatch;
                            }
                            else if (clause.SpectralType.HasValue)
                            {
                                var targetType = (MotelyItemType)(
                                    (int)MotelyItemTypeCategory.SpectralCard
                                    | (int)clause.SpectralType.Value
                                );
                                typeMatches = VectorEnum256.Equals(
                                    spectralItem.Type,
                                    targetType
                                );
                            }
                            else
                            {
                                // Wildcard - match any spectral card
                                typeMatches = VectorEnum256.Equals(
                                    spectralItem.TypeCategory,
                                    MotelyItemTypeCategory.SpectralCard
                                );
                            }

                            // Check edition match - PURE SIMD!
                            VectorMask editionMatches = VectorMask.AllBitsSet;
                            if (clause.EditionEnum.HasValue)
                            {
                                editionMatches = VectorEnum256.Equals(
                                    spectralItem.Edition,
                                    clause.EditionEnum.Value
                                );
                            }

                            // Combine: must be actual spectral AND match type AND match edition
                            VectorMask matches =
                                isActualSpectral & typeMatches & editionMatches;
                            clauseMask |= matches;
                        }
                    }
                }

                // Walk packs
                if (maxPackSlots > 0)
                {
                    clauseMask |= CheckPacksVectorized(clause, ctx, ante);
                }
            }

            // SINGLE clause - if it found nothing, fail
            if (clauseMask.IsAllFalse())
            {
                return VectorMask.NoBitsSet;
            }

            // USE THE SHARED FUNCTION - same logic as scoring!
            int minThreshold = _minThreshold; // Use pre-calculated value
            return ctx.SearchIndividualSeeds(
                clauseMask,
                (ref MotelySingleSearchContext singleCtx) =>
                {
                    // Count total occurrences across ALL wanted antes
                    int clauseCount = 0;
                    for (int ante = 0; ante < clause.WantedAntes.Length; ante++)
                    {
                        if (!clause.WantedAntes[ante])
                            continue;

                        int anteCount = MotelyJsonScoring.CountSpectralOccurrences(
                            ref singleCtx,
                            clause,
                            ante,
                            earlyExit: false
                        );
                        clauseCount += anteCount;
                    }

                    // Check Min threshold (pre-calculated value!)
                    return clauseCount >= minThreshold;
                }
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckShopVectorizedPrecomputed(
            MotelyJsonSpectralFilterClause clause,
            MotelyItemVector[] shopItems
        )
        {
            VectorMask shopResult = VectorMask.NoBitsSet;

            // Check each shop slot specified in the bitmask
            for (int shopSlot = 0; shopSlot < shopItems.Length; shopSlot++)
            {
                ulong shopSlotBit = 1UL << shopSlot;
                if (HasShopSlots(clause.WantedShopSlots) && !clause.WantedShopSlots[shopSlot])
                    continue;

                var shopItem = shopItems[shopSlot];

                // Check type match
                VectorMask typeMatches = VectorMask.AllBitsSet;
                if (clause.SpectralType.HasValue)
                {
                    // First check if it's a spectral card category
                    VectorMask isSpectralCard = VectorEnum256.Equals(
                        shopItem.TypeCategory,
                        MotelyItemTypeCategory.SpectralCard
                    );

                    // Construct the correct MotelyItemType for the spectral card
                    var targetSpectralType = (MotelyItemType)(
                        (int)MotelyItemTypeCategory.SpectralCard | (int)clause.SpectralType.Value
                    );
                    VectorMask correctSpectralType = VectorEnum256.Equals(
                        shopItem.Type,
                        targetSpectralType
                    );

                    typeMatches = isSpectralCard & correctSpectralType;
                }
                else
                {
                    // Wildcard match - any spectral card
                    typeMatches = VectorEnum256.Equals(
                        shopItem.TypeCategory,
                        MotelyItemTypeCategory.SpectralCard
                    );
                }

                // Check edition match
                VectorMask editionMatches = VectorMask.AllBitsSet;
                if (clause.EditionEnum.HasValue)
                {
                    editionMatches = VectorEnum256.Equals(
                        shopItem.Edition,
                        clause.EditionEnum.Value
                    );
                }

                // Combine type and edition
                VectorMask slotMatches = typeMatches & editionMatches;
                shopResult |= slotMatches;
            }

            return shopResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasShopSlots(bool[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i])
                    return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindMaxSlotIndex(bool[] slots)
        {
            for (int i = slots.Length - 1; i >= 0; i--)
                if (slots[i])
                    return i;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasPackSlots(bool[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i])
                    return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopSpectralVectorizedNew(
            MotelyJsonSpectralFilterClause clause,
            MotelyVectorSearchContext ctx,
            ref MotelyVectorShopSpectralStream shopSpectralStream,
            int ante
        )
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;

            // Calculate max slot we need to check based on ante
            int maxSlot;
            if (!HasShopSlots(clause.WantedShopSlots))
            {
                // No slots specified - use ante-based defaults
                maxSlot = MotelyJsonScoring.GetDefaultShopSlotsForAnte(ante);
            }
            else
            {
                // User specified slots - find the highest wanted slot
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

            // Check each shop slot using the self-contained stream
            for (int slot = 0; slot < maxSlot; slot++)
            {
                // ALWAYS get spectral for this slot to maintain stream synchronization!
                var spectralItem = shopSpectralStream.GetNext(ref ctx);

                // Only SCORE/MATCH if this slot is wanted (no slots = check all slots)
                if (HasShopSlots(clause.WantedShopSlots) && !clause.WantedShopSlots[slot])
                    continue; // Don't score this slot, but we already consumed from stream

                // Check if item is SpectralExcludedByStream (not a spectral slot) using SIMD
                var excludedValue = Vector256.Create((int)MotelyItemType.SpectralExcludedByStream);
                var isNotExcluded = ~Vector256.Equals(spectralItem.Value, excludedValue);
                VectorMask isActualSpectral = isNotExcluded;

                if (isActualSpectral.IsPartiallyTrue())
                {
                    // Check if the spectral matches our clause criteria
                    VectorMask typeMatches = VectorMask.AllBitsSet;
                    if (clause.SpectralType.HasValue)
                    {
                        // FIX: Properly construct the MotelyItemType by combining category and spectral type
                        var targetSpectralType = (MotelyItemType)(
                            (int)MotelyItemTypeCategory.SpectralCard
                            | (int)clause.SpectralType.Value
                        );
                        typeMatches = VectorEnum256.Equals(spectralItem.Type, targetSpectralType);
                    }

                    VectorMask editionMatches = VectorMask.AllBitsSet;
                    if (clause.EditionEnum.HasValue)
                    {
                        editionMatches = VectorEnum256.Equals(
                            spectralItem.Edition,
                            clause.EditionEnum.Value
                        );
                    }

                    VectorMask matches = typeMatches & editionMatches;
                    foundInShop |= (isActualSpectral & matches);
                }
            }

            return foundInShop;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckPacksVectorized(
            MotelyJsonSpectralFilterClause clause,
            MotelyVectorSearchContext ctx,
            int ante
        )
        {
            VectorMask foundInPacks = VectorMask.NoBitsSet;

            // Create pack streams
            var packStream = ctx.CreateBoosterPackStream(ante);
            var spectralStream = ctx.CreateSpectralPackSpectralStream(ante);

            // Determine max pack slot to check - use config if provided
            bool hasSpecificSlots = HasPackSlots(clause.WantedPackSlots);
            int maxPackSlot = clause.MaxPackSlot.HasValue
                ? clause.MaxPackSlot.Value + 1
                : (ante == 1 ? 4 : 6);

            for (int packSlot = 0; packSlot < maxPackSlot; packSlot++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);

                // Check if this pack slot should be evaluated
                bool shouldCheckThisSlot = !hasSpecificSlots || clause.WantedPackSlots[packSlot];

                var packType = pack.GetPackType();

                // Check Spectral packs with vectorized method
                VectorMask isSpectralPack = VectorEnum256.Equals(
                    packType,
                    MotelyBoosterPackType.Spectral
                );
                // ALWAYS consume spectral stream if it's a spectral pack to maintain sync
                if (isSpectralPack.IsPartiallyTrue())
                {
                    // Get pack sizes for proper stream consumption per lane
                    var packSizes = pack.GetPackSize();

                    // Use the new lane-aware version that consumes correct number per lane!
                    var contents = ctx.GetNextSpectralPackContentsPerLane(
                        ref spectralStream,
                        packSizes,
                        isSpectralPack
                    );

                    // Only evaluate if we should check this slot
                    if (!shouldCheckThisSlot)
                        continue;

                    // Check requireMega constraint if specified
                    VectorMask packSizeOk = VectorMask.AllBitsSet;
                    if (clause.Sources?.RequireMega == true)
                    {
                        packSizeOk = VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Mega);
                    }

                    // Check each card in the pack (up to max possible)
                    for (int cardIndex = 0; cardIndex < MotelyVectorItemSet.MaxLength; cardIndex++)
                    {
                        var card = contents[cardIndex];

                        // Create mask for lanes where this card position actually exists in the pack
                        // We still need this check, but simpler: just check if the cardIndex is within pack size
                        VectorMask cardExistsInPack = cardIndex switch
                        {
                            0 or 1 => VectorMask.AllBitsSet, // All packs have at least 2 cards
                            2 => ~VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Normal), // Jumbo and Mega have 3rd card
                            3 or 4 => VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Mega), // Only Mega has 4th and 5th cards
                            _ => VectorMask.NoBitsSet,
                        };

                        // Check if this is a spectral card that matches our clause
                        VectorMask isSpectralCard = VectorEnum256.Equals(
                            card.TypeCategory,
                            MotelyItemTypeCategory.SpectralCard
                        );

                        if (isSpectralCard.IsPartiallyTrue())
                        {
                            VectorMask typeMatches = VectorMask.AllBitsSet;
                            if (clause.SpectralTypes?.Count > 0)
                            {
                                VectorMask anyTypeMatch = VectorMask.NoBitsSet;
                                foreach (var spectralType in clause.SpectralTypes)
                                {
                                    var targetType = (MotelyItemType)(
                                        (int)MotelyItemTypeCategory.SpectralCard | (int)spectralType
                                    );
                                    anyTypeMatch |= VectorEnum256.Equals(card.Type, targetType);
                                }
                                typeMatches = anyTypeMatch;
                            }
                            else if (clause.SpectralType.HasValue)
                            {
                                var targetSpectralType = (MotelyItemType)(
                                    (int)MotelyItemTypeCategory.SpectralCard
                                    | (int)clause.SpectralType.Value
                                );
                                typeMatches = VectorEnum256.Equals(card.Type, targetSpectralType);
                            }

                            VectorMask editionMatches = VectorMask.AllBitsSet;
                            if (clause.EditionEnum.HasValue)
                            {
                                editionMatches = VectorEnum256.Equals(
                                    card.Edition,
                                    clause.EditionEnum.Value
                                );
                            }

                            // Only match if card exists in this pack size AND matches our criteria
                            VectorMask matches = (
                                isSpectralPack
                                & packSizeOk
                                & cardExistsInPack
                                & isSpectralCard
                                & typeMatches
                                & editionMatches
                            );
                            foundInPacks |= matches;
                        }
                    }
                }
            }

            return foundInPacks;
        }

        private static bool CheckSpectralIndividualStatic(
            ref MotelySingleSearchContext ctx,
            List<MotelyJsonSpectralFilterClause> clauses
        )
        {
            // Check each clause - all must be satisfied
            foreach (var clause in clauses)
            {
                bool clauseSatisfied = false;

                // Check all antes in the clause's bitmask (up to array size)
                for (int ante = 1; ante < clause.WantedAntes.Length; ante++)
                {
                    if (!clause.WantedAntes[ante])
                        continue;

                    // Check shops only if we have shop slots to check
                    if (HasShopSlots(clause.WantedShopSlots))
                    {
                        var shopSpectralStream = ctx.CreateShopSpectralStream(ante);
                        if (CheckShopSpectralsSingle(ref ctx, ref shopSpectralStream, clause))
                        {
                            clauseSatisfied = true;
                            break;
                        }
                    }

                    // Check packs only if we have pack slots to check
                    if (HasPackSlots(clause.WantedPackSlots))
                    {
                        if (CheckPackSpectralsSingle(ref ctx, ante, clause))
                        {
                            clauseSatisfied = true;
                            break;
                        }
                    }
                }

                if (!clauseSatisfied)
                    return false; // This clause wasn't satisfied
            }

            return true; // All clauses satisfied
        }

        private static bool CheckShopSpectralsSingle(
            ref MotelySingleSearchContext ctx,
            ref MotelySingleSpectralStream stream,
            MotelyJsonSpectralFilterClause clause
        )
        {
            // Calculate max slot to check
            int maxSlot;
            if (!HasShopSlots(clause.WantedShopSlots))
            {
                maxSlot = 16;
            }
            else
            {
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

            for (int slot = 0; slot < maxSlot; slot++)
            {
                // ALWAYS get spectral to maintain stream synchronization!
                var spectral = ctx.GetNextSpectral(ref stream);

                // Only SCORE/MATCH if this slot is wanted (no slots = check all)
                if (HasShopSlots(clause.WantedShopSlots) && !clause.WantedShopSlots[slot])
                    continue; // Don't score this slot, but we already consumed from stream

                // Skip if not a spectral slot
                if (spectral.Type == MotelyItemType.SpectralExcludedByStream)
                    continue;

                // Check if it matches our criteria
                bool matches = true;

                // Check type
                if (clause.SpectralTypes?.Count > 0)
                {
                    bool typeMatch = false;
                    foreach (var spectralType in clause.SpectralTypes)
                    {
                        if (
                            spectral.Type
                            == (MotelyItemType)(
                                (int)MotelyItemTypeCategory.SpectralCard | (int)spectralType
                            )
                        )
                        {
                            typeMatch = true;
                            break;
                        }
                    }
                    matches &= typeMatch;
                }
                else if (clause.SpectralType.HasValue)
                {
                    matches &=
                        spectral.Type
                        == (MotelyItemType)(
                            (int)MotelyItemTypeCategory.SpectralCard
                            | (int)clause.SpectralType.Value
                        );
                }

                // Check edition
                if (clause.EditionEnum.HasValue)
                {
                    matches &= spectral.Edition == clause.EditionEnum.Value;
                }

                if (matches)
                    return true;
            }

            return false;
        }

        private static bool CheckPackSpectralsSingle(
            ref MotelySingleSearchContext ctx,
            int ante,
            MotelyJsonSpectralFilterClause clause
        )
        {
            var packStream = ctx.CreateBoosterPackStream(ante);
            var spectralStream = ctx.CreateSpectralPackSpectralStream(ante);

            // Determine max pack slot to check - use config if provided
            bool hasSpecificSlots = HasPackSlots(clause.WantedPackSlots);
            int maxPackSlot = clause.MaxPackSlot.HasValue
                ? clause.MaxPackSlot.Value + 1
                : (ante == 1 ? 4 : 6);

            for (int packSlot = 0; packSlot < maxPackSlot; packSlot++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);

                // Check if this pack slot should be evaluated - simple array lookup!
                bool shouldCheckThisSlot = !hasSpecificSlots || clause.WantedPackSlots[packSlot];

                // Check if it's a Spectral pack
                bool isSpectralPack = pack.GetPackType() == MotelyBoosterPackType.Spectral;

                // ALWAYS consume spectral stream if it's a spectral pack to maintain sync
                if (isSpectralPack)
                {
                    // Get the actual pack size for this individual seed
                    var packSize = pack.GetPackSize();

                    // Always consume max size (5) to maintain consistency with vectorized path
                    var contents = ctx.GetNextSpectralPackContents(
                        ref spectralStream,
                        MotelyBoosterPackSize.Mega
                    );

                    // Only evaluate if we should check this slot
                    if (!shouldCheckThisSlot)
                        continue;

                    // Check requireMega if specified in sources
                    if (
                        clause.Sources?.RequireMega == true
                        && packSize != MotelyBoosterPackSize.Mega
                    )
                        continue; // Skip non-Mega packs if Mega is required

                    int actualPackSize = packSize switch
                    {
                        MotelyBoosterPackSize.Normal => 2,
                        MotelyBoosterPackSize.Jumbo => 3,
                        MotelyBoosterPackSize.Mega => 5,
                        _ => 2,
                    };

                    // Check each card in the pack (only up to actual size)
                    for (int cardIndex = 0; cardIndex < actualPackSize; cardIndex++)
                    {
                        var card = contents[cardIndex];

                        if (card.TypeCategory != MotelyItemTypeCategory.SpectralCard)
                            continue;

                        bool matches = true;

                        // Check type
                        if (clause.SpectralTypes?.Count > 0)
                        {
                            bool typeMatch = false;
                            foreach (var spectralType in clause.SpectralTypes)
                            {
                                if (
                                    card.Type
                                    == (MotelyItemType)(
                                        (int)MotelyItemTypeCategory.SpectralCard | (int)spectralType
                                    )
                                )
                                {
                                    typeMatch = true;
                                    break;
                                }
                            }
                            matches &= typeMatch;
                        }
                        else if (clause.SpectralType.HasValue)
                        {
                            matches &=
                                card.Type
                                == (MotelyItemType)(
                                    (int)MotelyItemTypeCategory.SpectralCard
                                    | (int)clause.SpectralType.Value
                                );
                        }

                        // Check edition
                        if (clause.EditionEnum.HasValue)
                        {
                            matches &= card.Edition == clause.EditionEnum.Value;
                        }

                        if (matches)
                            return true;
                    }
                } // Close the if (isSpectralPack) block
            }

            return false;
        }
    }
}
