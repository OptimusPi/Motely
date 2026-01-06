using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on tarot card criteria from JSON configuration.
/// REVERTED: Simple version that compiles - shop detection removed for now
/// </summary>
public partial struct MotelyJsonTarotCardFilterDesc(MotelyJsonTarotFilterCriteria criteria)
    : IMotelySeedFilterDesc<MotelyJsonTarotCardFilterDesc.MotelyJsonTarotCardFilter>
{
    private readonly MotelyJsonTarotFilterCriteria _criteria = criteria;

    public MotelyJsonTarotCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Use pre-calculated values from criteria
        int minAnte = _criteria.MinAnte;
        int maxAnte = _criteria.MaxAnte;

        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }

        // SINGLE clause only - caller must chain multiple filters for multiple clauses
        if (_criteria.Clauses.Count != 1)
            throw new ArgumentException($"MotelyJsonTarotCardFilter expects exactly 1 clause, got {_criteria.Clauses.Count}");

        return new MotelyJsonTarotCardFilter(
            _criteria.Clauses[0],
            minAnte,
            maxAnte,
            _criteria.MaxShopSlotsNeeded
        );
    }

    public struct MotelyJsonTarotCardFilter(
        MotelyJsonTarotFilterClause clause,
        int minAnte,
        int maxAnte,
        int maxShopSlotsNeeded
    ) : IMotelySeedFilter
    {
        private readonly MotelyJsonTarotFilterClause Clause = clause;
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
                bool hasTarotStreamSources =
                    clause.Sources?.PurpleSealOrEightBall is { Length: > 0 }
                    || clause.Sources?.Emperor is { Length: > 0 };
                bool useDefaults = !hasShop && !hasPack && !hasTarotStreamSources;

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
                var shopTarotStream =
                    maxShopSlots > 0 ? ctx.CreateShopTarotStreamNew(ante) : default;

                // Walk shop slots
                for (int slot = 0; slot < maxShopSlots; slot++)
                {
                    var tarotItem = shopTarotStream.GetNext(ref ctx);

                    // Check if it's an actual tarot (not excluded) - PURE SIMD!
                    var excludedValue = Vector256.Create((int)MotelyItemType.TarotExcludedByStream);
                    VectorMask isActualTarot = ~Vector256.Equals(tarotItem.Value, excludedValue);

                    if (!isActualTarot.IsAllFalse())
                    {
                        bool wantsSlot = !hasShop || clause.WantedShopSlots[slot];
                        if (wantsSlot)
                        {
                            VectorMask matches = CheckTarotMatchesClause(
                                tarotItem,
                                clause,
                                ref ctx
                            );
                            clauseMask |= (isActualTarot & matches);
                        }
                    }
                }

                // Walk packs
                if (maxPackSlots > 0)
                {
                    clauseMask |= CheckPacksVectorized(clause, ctx, ante);
                }

                // Check Purple Seal / 8Ball tarot sources
                if (clause.Sources?.PurpleSealOrEightBall != null && clause.Sources.PurpleSealOrEightBall.Length > 0)
                {
                    var purpleSealStream = ctx.CreatePurpleSealTarotStream(ante);
                    var rollIndices = clause.Sources.PurpleSealOrEightBall;

                    // rollIndices are normalized at config load time (sorted, unique, non-negative)
                    int maxRollIndex = rollIndices[rollIndices.Length - 1];
                    int pos = 0;
                    int nextWanted = rollIndices[0];
                    var excludedValue = Vector256.Create((int)MotelyItemType.TarotExcludedByStream);

                    for (int r = 0; r <= maxRollIndex; r++)
                    {
                        var tarotItem = ctx.GetNextTarot(ref purpleSealStream);
                        if (r != nextWanted)
                            continue;

                        var isNotExcluded = ~Vector256.Equals(tarotItem.Value, excludedValue);
                        VectorMask isActualTarot = isNotExcluded;

                        if (!isActualTarot.IsAllFalse())
                        {
                            // Check type match
                            VectorMask typeMatches = VectorMask.AllBitsSet;
                            if (clause.TarotType.HasValue)
                            {
                                var targetTarotType = (MotelyItemType)(
                                    (int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value
                                );
                                typeMatches = VectorEnum256.Equals(tarotItem.Type, targetTarotType);
                            }

                            // Check edition match
                            VectorMask editionMatches = VectorMask.AllBitsSet;
                            if (clause.EditionEnum.HasValue)
                            {
                                editionMatches = VectorEnum256.Equals(
                                    tarotItem.Edition,
                                    clause.EditionEnum.Value
                                );
                            }

                            VectorMask matches = isActualTarot & typeMatches & editionMatches;
                            clauseMask |= matches;
                        }

                        pos++;
                        if (pos >= rollIndices.Length)
                            break;
                        nextWanted = rollIndices[pos];
                    }
                }

                // Check Emperor tarot sources
                if (clause.Sources?.Emperor != null && clause.Sources.Emperor.Length > 0)
                {
                    var emperorStream = ctx.CreateEmperorTarotStream(ante);
                    var rollIndices = clause.Sources.Emperor;

                    // rollIndices are normalized at config load time (sorted, unique, non-negative)
                    int maxRollIndex = rollIndices[rollIndices.Length - 1];
                    int pos = 0;
                    int nextWanted = rollIndices[0];
                    var excludedValue = Vector256.Create((int)MotelyItemType.TarotExcludedByStream);

                    for (int r = 0; r <= maxRollIndex; r++)
                    {
                        // Emperor gives 2 tarot cards - we need to check both
                        var emperorTarots = ctx.GetNextEmperorTarots(ref emperorStream);
                        if (r != nextWanted)
                            continue;

                        // Check both tarot cards from Emperor
                        var firstTarot = emperorTarots[0];
                        var secondTarot = emperorTarots[1];

                        var isNotExcludedFirst = ~Vector256.Equals(firstTarot.Value, excludedValue);
                        var isNotExcludedSecond = ~Vector256.Equals(secondTarot.Value, excludedValue);
                        VectorMask isActualTarotFirst = isNotExcludedFirst;
                        VectorMask isActualTarotSecond = isNotExcludedSecond;

                        if (!isActualTarotFirst.IsAllFalse() || !isActualTarotSecond.IsAllFalse())
                        {
                            // Check type match for first tarot
                            VectorMask typeMatchesFirst = VectorMask.AllBitsSet;
                            if (clause.TarotType.HasValue)
                            {
                                var targetTarotType = (MotelyItemType)(
                                    (int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value
                                );
                                typeMatchesFirst = VectorEnum256.Equals(firstTarot.Type, targetTarotType);
                            }

                            // Check edition match for first tarot
                            VectorMask editionMatchesFirst = VectorMask.AllBitsSet;
                            if (clause.EditionEnum.HasValue)
                            {
                                editionMatchesFirst = VectorEnum256.Equals(
                                    firstTarot.Edition,
                                    clause.EditionEnum.Value
                                );
                            }

                            // Check type match for second tarot
                            VectorMask typeMatchesSecond = VectorMask.AllBitsSet;
                            if (clause.TarotType.HasValue)
                            {
                                var targetTarotType = (MotelyItemType)(
                                    (int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value
                                );
                                typeMatchesSecond = VectorEnum256.Equals(secondTarot.Type, targetTarotType);
                            }

                            // Check edition match for second tarot
                            VectorMask editionMatchesSecond = VectorMask.AllBitsSet;
                            if (clause.EditionEnum.HasValue)
                            {
                                editionMatchesSecond = VectorEnum256.Equals(
                                    secondTarot.Edition,
                                    clause.EditionEnum.Value
                                );
                            }

                            // Match if either tarot matches
                            VectorMask matches = (isActualTarotFirst & typeMatchesFirst & editionMatchesFirst)
                                              | (isActualTarotSecond & typeMatchesSecond & editionMatchesSecond);
                            clauseMask |= matches;
                        }

                        pos++;
                        if (pos >= rollIndices.Length)
                            break;
                        nextWanted = rollIndices[pos];
                    }
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
                    var state = new MotelyRunState();

                    // Count total occurrences across ALL wanted antes
                    int clauseCount = 0;
                    for (int ante = 0; ante < clause.WantedAntes.Length; ante++)
                    {
                        if (!clause.WantedAntes[ante])
                            continue;

                        int anteCount = MotelyJsonScoring.TarotCardsTally(
                            ref singleCtx,
                            clause,
                            ante,
                            ref state,
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
        private VectorMask CheckShopVectorized(
            ref MotelyVectorSearchContext ctx,
            int ante,
            MotelyJsonTarotFilterClause clause,
            ref MotelyVectorShopItemStream shopStream
        )
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;

            // Check each shop slot based on the bitmask
            for (int slot = 0; slot < 64; slot++) // Check up to 64 slots (bitmask size)
            {
                // Skip if this slot isn't in the wanted slots
                if (!clause.WantedShopSlots[slot])
                    continue;

                // Get the shop item using the shared tarot-only stream
                var item = ctx.GetNextShopItem(ref shopStream);

                // Check if this slot has a tarot
                var isTarot = VectorEnum256.Equals(
                    item.TypeCategory,
                    MotelyItemTypeCategory.TarotCard
                );

                // Check if any lanes have tarots (result is -1 for true, 0 for false) - ONLY CHECK VALID LANES!
                uint tarotMask = 0;
                for (int i = 0; i < 8; i++)
                    if (ctx.IsLaneValid(i) && isTarot[i] == -1)
                        tarotMask |= (1u << i);

                if (tarotMask != 0) // Any lanes have tarots
                {
                    // Check if it matches our clause
                    VectorMask matches = CheckTarotMatchesClause(item, clause, ref ctx);
                    foundInShop |= matches;
                }
            }

            return foundInShop;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopVectorizedPrecomputed(
            MotelyJsonTarotFilterClause clause,
            MotelyItemVector[] shopItems,
            ref MotelyVectorSearchContext ctx
        )
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;

            if (!HasShopSlots(clause.WantedShopSlots))
            {
                // No slot restrictions - check all available slots
                for (int slot = 0; slot < shopItems.Length; slot++)
                {
                    var item = shopItems[slot];
                    DebugLogger.Log(
                        $"[TAROT VECTORIZED] Checking shop slot {slot}: item type category={item.TypeCategory}"
                    );

                    // Check if this slot has a tarot
                    var isTarot = VectorEnum256.Equals(
                        item.TypeCategory,
                        MotelyItemTypeCategory.TarotCard
                    );

                    // Check if any lanes have tarots - ONLY CHECK VALID LANES!
                    uint tarotMask = 0;
                    for (int i = 0; i < 8; i++)
                        if (ctx.IsLaneValid(i) && isTarot[i] == -1)
                            tarotMask |= (1u << i);

                    if (tarotMask != 0) // Any lanes have tarots
                    {
                        DebugLogger.Log(
                            $"[TAROT VECTORIZED] Found tarot at shop slot {slot}: {item.Type[0]}, expecting: {clause.TarotType}"
                        );
                        // Check if it matches our clause
                        VectorMask matches = CheckTarotMatchesClause(item, clause, ref ctx);
                        DebugLogger.Log($"[TAROT VECTORIZED] Matches mask={matches.Value:X}");
                        foundInShop |= matches;
                        if (!foundInShop.IsAllFalse())
                            break; // Found a match, can stop
                    }
                }
            }
            else
            {
                // Calculate the highest slot we need to check
                int maxSlot = 0;
                for (int i = clause.WantedShopSlots.Length - 1; i >= 0; i--)
                {
                    if (clause.WantedShopSlots[i])
                    {
                        maxSlot = i + 1;
                        break;
                    }
                }

                // Check only the slots we precomputed
                for (int slot = 0; slot < Math.Min(maxSlot, shopItems.Length); slot++)
                {
                    // Check if this slot is wanted
                    if (clause.WantedShopSlots[slot])
                    {
                        var item = shopItems[slot];
                        DebugLogger.Log(
                            $"[TAROT VECTORIZED] Checking shop slot {slot}: item type category={item.TypeCategory}"
                        );

                        // Check if this slot has a tarot
                        var isTarot = VectorEnum256.Equals(
                            item.TypeCategory,
                            MotelyItemTypeCategory.TarotCard
                        );

                        // Check if any lanes have tarots - ONLY CHECK VALID LANES!
                        uint tarotMask = 0;
                        for (int i = 0; i < 8; i++)
                            if (ctx.IsLaneValid(i) && isTarot[i] == -1)
                                tarotMask |= (1u << i);

                        if (tarotMask != 0) // Any lanes have tarots
                        {
                            DebugLogger.Log(
                                $"[TAROT VECTORIZED] Found tarot at shop slot {slot}: {item.Type[0]}, expecting: {clause.TarotType}"
                            );
                            // Check if it matches our clause
                            VectorMask matches = CheckTarotMatchesClause(item, clause, ref ctx);
                            DebugLogger.Log($"[TAROT VECTORIZED] Matches mask={matches.Value:X}");
                            foundInShop |= matches;
                        }
                    }
                }
            }

            return foundInShop;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckTarotMatchesClause(
            MotelyItemVector item,
            MotelyJsonTarotFilterClause clause,
            ref MotelyVectorSearchContext ctx
        )
        {
            VectorMask matches = VectorMask.AllBitsSet;

            // Check type if specified
            if (clause.TarotTypes != null && clause.TarotTypes.Count > 0)
            {
                VectorMask typeMatch = VectorMask.NoBitsSet;
                foreach (var tarotType in clause.TarotTypes)
                {
                    var targetType = (MotelyItemType)(
                        (int)MotelyItemTypeCategory.TarotCard | (int)tarotType
                    );
                    typeMatch |= VectorEnum256.Equals(item.Type, targetType);
                }
                matches &= typeMatch;
            }
            else if (clause.TarotType.HasValue)
            {
                var targetType = (MotelyItemType)(
                    (int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value
                );
                matches &= VectorEnum256.Equals(item.Type, targetType);
            }
            else
            {
                // Match any tarot
                matches &= VectorEnum256.Equals(
                    item.TypeCategory,
                    MotelyItemTypeCategory.TarotCard
                );
            }

            // Check edition if specified
            if (clause.EditionEnum.HasValue)
            {
                matches &= VectorEnum256.Equals(item.Edition, clause.EditionEnum.Value);
            }

            return matches;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckTarotTypeMatch(MotelyItem item, MotelyJsonTarotFilterClause clause)
        {
            if (clause.TarotTypes?.Count > 0)
            {
                foreach (var tarotType in clause.TarotTypes)
                {
                    if (
                        item.Type
                        == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)tarotType)
                    )
                    {
                        return true;
                    }
                }
                return false;
            }
            else if (clause.TarotType.HasValue)
            {
                return item.Type
                    == (MotelyItemType)(
                        (int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value
                    );
            }
            else
            {
                return item.TypeCategory == MotelyItemTypeCategory.TarotCard;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckPacksVectorized(
            MotelyJsonTarotFilterClause clause,
            MotelyVectorSearchContext ctx,
            int ante
        )
        {
            VectorMask foundInPacks = VectorMask.NoBitsSet;

            // Create pack streams
            var packStream = ctx.CreateBoosterPackStream(ante);
            var arcanaStream = ctx.CreateArcanaPackTarotStream(ante);

            // Determine max pack slot to check - use config if provided
            bool hasSpecificSlots = HasPackSlots(clause.WantedPackSlots);
            int maxPackSlot = clause.MaxPackSlot.HasValue
                ? clause.MaxPackSlot.Value + 1
                : (ante == 1 ? 4 : 6);
            
            // OPTIMIZATION: If we have specific slots, only check up to the max wanted slot
            // This avoids checking unnecessary slots when only early slots are needed
            if (hasSpecificSlots)
            {
                int maxWantedSlot = FindMaxSlotIndex(clause.WantedPackSlots);
                maxPackSlot = Math.Min(maxPackSlot, maxWantedSlot + 1);
            }

            for (int packSlot = 0; packSlot < maxPackSlot; packSlot++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);

                // Check if this pack slot should be evaluated for scoring
                bool shouldEvaluateThisSlot = !hasSpecificSlots || clause.WantedPackSlots[packSlot];

                var packType = pack.GetPackType();

                // Check Arcana packs with vectorized method
                VectorMask isArcanaPack = VectorEnum256.Equals(
                    packType,
                    MotelyBoosterPackType.Arcana
                );
                if (isArcanaPack.IsPartiallyTrue())
                {
                    // FIXED: Always consume maximum pack size (5) to avoid stream desync
                    var contents = ctx.GetNextArcanaPackContents(
                        ref arcanaStream,
                        MotelyBoosterPackSize.Mega
                    );

                    // Only evaluate/score if this slot should be checked
                    if (!shouldEvaluateThisSlot)
                        continue;

                    // Check each card in the pack
                    for (int cardIndex = 0; cardIndex < contents.Length; cardIndex++)
                    {
                        var card = contents[cardIndex];

                        // Check if this is a tarot card that matches our clause
                        VectorMask isTarotCard = VectorEnum256.Equals(
                            card.TypeCategory,
                            MotelyItemTypeCategory.TarotCard
                        );

                        if (isTarotCard.IsPartiallyTrue())
                        {
                            VectorMask typeMatches = VectorMask.AllBitsSet;
                            if (clause.TarotType.HasValue)
                            {
                                var targetTarotType = (MotelyItemType)(
                                    (int)MotelyItemTypeCategory.TarotCard
                                    | (int)clause.TarotType.Value
                                );
                                typeMatches = VectorEnum256.Equals(card.Type, targetTarotType);
                            }

                            VectorMask editionMatches = VectorMask.AllBitsSet;
                            if (clause.EditionEnum.HasValue)
                            {
                                editionMatches = VectorEnum256.Equals(
                                    card.Edition,
                                    clause.EditionEnum.Value
                                );
                            }

                            VectorMask matches = (
                                isArcanaPack & isTarotCard & typeMatches & editionMatches
                            );
                            foundInPacks |= matches;
                        }
                    }
                }
            }

            return foundInPacks;
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
        private VectorMask CheckShopTarotVectorizedNew(
            MotelyJsonTarotFilterClause clause,
            MotelyVectorSearchContext ctx,
            ref MotelyVectorShopTarotStream shopTarotStream,
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
                // Get tarot for this slot using self-contained stream - handles slot types internally!
                var tarotItem = shopTarotStream.GetNext(ref ctx);

                // Skip if this slot isn't wanted (no slots = check all slots)
                if (HasShopSlots(clause.WantedShopSlots) && !clause.WantedShopSlots[slot])
                    continue;

                // Check if item is TarotExcludedByStream (not a tarot slot) - PURE SIMD!
                var excludedValue = Vector256.Create((int)MotelyItemType.TarotExcludedByStream);
                VectorMask isActualTarot = ~Vector256.Equals(tarotItem.Value, excludedValue);

                if (!isActualTarot.IsAllFalse())
                {
                    // Check if the tarot matches our clause criteria
                    VectorMask matches = CheckTarotMatchesClause(tarotItem, clause, ref ctx);
                    foundInShop |= (isActualTarot & matches);
                }
            }

            return foundInShop;
        }

        private static bool CheckTarotIndividualStatic(
            ref MotelySingleSearchContext ctx,
            List<MotelyJsonTarotFilterClause> clauses
        )
        {
            // Check each clause - all must be satisfied
            foreach (var clause in clauses)
            {
                bool clauseSatisfied = false;

                // Check all antes in the clause's bitmask
                for (int ante = 1; ante <= 64; ante++)
                {
                    if (!clause.WantedAntes[ante])
                        continue;

                    // Check shops if specified
                    if (HasShopSlots(clause.WantedShopSlots))
                    {
                        var shopTarotStream = ctx.CreateShopTarotStream(ante);
                        if (CheckShopTarotsSingle(ref ctx, ref shopTarotStream, clause))
                        {
                            clauseSatisfied = true;
                            break;
                        }
                    }

                    // Check packs if specified
                    if (HasPackSlots(clause.WantedPackSlots))
                    {
                        if (CheckPackTarotsSingle(ref ctx, ante, clause))
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

        private static bool CheckShopTarotsSingle(
            ref MotelySingleSearchContext ctx,
            ref MotelySingleTarotStream stream,
            MotelyJsonTarotFilterClause clause
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
                // Skip if this slot isn't wanted (no slots = check all)
                if (HasShopSlots(clause.WantedShopSlots) && !clause.WantedShopSlots[slot])
                    continue;

                var tarot = ctx.GetNextTarot(ref stream);

                // Skip if not a tarot slot
                if (tarot.Type == MotelyItemType.TarotExcludedByStream)
                    continue;

                // Check if it matches our criteria
                bool matches = true;

                // Check type
                if (clause.TarotTypes?.Count > 0)
                {
                    bool typeMatch = false;
                    foreach (var tarotType in clause.TarotTypes)
                    {
                        if (
                            tarot.Type
                            == (MotelyItemType)(
                                (int)MotelyItemTypeCategory.TarotCard | (int)tarotType
                            )
                        )
                        {
                            typeMatch = true;
                            break;
                        }
                    }
                    matches &= typeMatch;
                }
                else if (clause.TarotType.HasValue)
                {
                    matches &=
                        tarot.Type
                        == (MotelyItemType)(
                            (int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value
                        );
                }

                // Check edition
                if (clause.EditionEnum.HasValue)
                {
                    matches &= tarot.Edition == clause.EditionEnum.Value;
                }

                if (matches)
                    return true;
            }

            return false;
        }

        private static bool CheckPackTarotsSingle(
            ref MotelySingleSearchContext ctx,
            int ante,
            MotelyJsonTarotFilterClause clause
        )
        {
            var packStream = ctx.CreateBoosterPackStream(ante);
            var arcanaStream = ctx.CreateArcanaPackTarotStream(ante);

            // Determine max pack slot to check - use config if provided
            bool hasSpecificSlots = HasPackSlots(clause.WantedPackSlots);
            int maxPackSlot = clause.MaxPackSlot.HasValue
                ? clause.MaxPackSlot.Value + 1
                : (ante == 1 ? 4 : 6);

            for (int packSlot = 0; packSlot < maxPackSlot; packSlot++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);

                // Check if this pack slot should be evaluated for scoring
                bool shouldEvaluateThisSlot = !hasSpecificSlots || clause.WantedPackSlots[packSlot];

                // Check if it's an Arcana pack
                bool isArcanaPack = pack.GetPackType() == MotelyBoosterPackType.Arcana;

                // ALWAYS consume arcana stream if it's an arcana pack to maintain sync
                if (isArcanaPack)
                {
                    // Get the actual pack size for this individual seed
                    var packSize = pack.GetPackSize();
                    var contents = ctx.GetNextArcanaPackContents(ref arcanaStream, packSize);

                    // Only evaluate/score if this slot should be checked
                    if (!shouldEvaluateThisSlot)
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

                    // Check each card in the pack
                    for (int cardIndex = 0; cardIndex < actualPackSize; cardIndex++)
                    {
                        var card = contents[cardIndex];

                        if (card.TypeCategory != MotelyItemTypeCategory.TarotCard)
                            continue;

                        bool matches = true;

                        // Check type
                        if (clause.TarotTypes?.Count > 0)
                        {
                            bool typeMatch = false;
                            foreach (var tarotType in clause.TarotTypes)
                            {
                                if (
                                    card.Type
                                    == (MotelyItemType)(
                                        (int)MotelyItemTypeCategory.TarotCard | (int)tarotType
                                    )
                                )
                                {
                                    typeMatch = true;
                                    break;
                                }
                            }
                            matches &= typeMatch;
                        }
                        else if (clause.TarotType.HasValue)
                        {
                            matches &=
                                card.Type
                                == (MotelyItemType)(
                                    (int)MotelyItemTypeCategory.TarotCard
                                    | (int)clause.TarotType.Value
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
                } // Close the if (isArcanaPack) block
            }

            return false;
        }
    }
}
