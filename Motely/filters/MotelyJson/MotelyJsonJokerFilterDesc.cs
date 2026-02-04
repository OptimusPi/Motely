using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on joker criteria from JSON configuration.
/// </summary>
public partial struct MotelyJsonJokerFilterDesc(MotelyJsonJokerFilterCriteria criteria)
    : IMotelySeedFilterDesc<MotelyJsonJokerFilterDesc.MotelyJsonJokerFilter>
{
    private readonly MotelyJsonJokerFilterCriteria _criteria = criteria;

    public MotelyJsonJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Use pre-calculated values from criteria
        int minAnte = _criteria.MinAnte;
        int maxAnte = _criteria.MaxAnte;

        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }

        // Support multiple clauses - all checked in single shop simulation pass for efficiency
        return new MotelyJsonJokerFilter(_criteria.Clauses, minAnte, maxAnte);
    }

    public struct MotelyJsonJokerFilter(
        List<MotelyJsonJokerFilterClause> clauses,
        int minAnte,
        int maxAnte
    ) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonJokerFilterClause> Clauses = clauses;
        private readonly int MinAnte = minAnte;
        private readonly int MaxAnte = maxAnte;
        private readonly int[] _lastWantedAntes = clauses.Select(CalculateLastWantedAnte).ToArray();

        private static int CalculateLastWantedAnte(MotelyJsonJokerFilterClause clause)
        {
            for (int a = clause.WantedAntes.Length - 1; a >= 0; a--)
            {
                if (clause.WantedAntes[a])
                    return a;
            }
            return -1;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(
                Clauses != null && Clauses.Count > 0,
                "Joker filter created with empty clauses - this is a programming error!"
            );

            int _minAnte = MinAnte;
            int _maxAnte = MaxAnte;

            // MULTIPLE clauses - track if each matched across all antes
            VectorMask[] clauseMasks = new VectorMask[Clauses.Count];
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                clauseMasks[i] = VectorMask.NoBitsSet;
            }

            // Initialize run state for voucher calculations
            var runState = ctx.Deck.GetDefaultRunState();

            // Walk each ante and check all clauses
            for (int ante = _minAnte; ante <= _maxAnte; ante++)
            {
                // CRITICAL: Early exit if any clause has NO matches yet and we're past its last wanted ante
                bool canEarlyExit = true;
                for (int c = 0; c < Clauses.Count; c++)
                {
                    if (
                        _lastWantedAntes[c] >= 0
                        && ante > _lastWantedAntes[c]
                        && clauseMasks[c].IsAllFalse()
                    )
                    {
                        return VectorMask.NoBitsSet;
                    }
                    if (ante < Clauses[c].WantedAntes.Length && Clauses[c].WantedAntes[ante])
                    {
                        canEarlyExit = false;
                    }
                }
                if (canEarlyExit)
                    continue;

                // Calculate max slots needed across ALL clauses for this ante
                int maxShopSlots = 0;
                int maxPackSlots = 0;
                bool anyHasShop = false;
                bool anyHasPack = false;
                bool anyHasJokerStreamSources = false;

                for (int c = 0; c < Clauses.Count; c++)
                {
                    var clause = Clauses[c];
                    if (ante >= clause.WantedAntes.Length || !clause.WantedAntes[ante])
                        continue;

                    bool hasShop = HasShopSlots(clause.WantedShopSlots);
                    bool hasPack = HasPackSlots(clause.WantedPackSlots);
                    bool hasJokerStreamSources =
                        clause.Sources?.Judgement is { Length: > 0 }
                        || clause.Sources?.Wraith is { Length: > 0 }
                        || clause.Sources?.RareTag is { Length: > 0 }
                        || clause.Sources?.UncommonTag is { Length: > 0 }
                        || clause.Sources?.RiffRaff is { Length: > 0 }
                        || clause.Sources?.UncommonShopJokers is { Length: > 0 }
                        || clause.Sources?.RareShopJokers is { Length: > 0 }
                        || clause.Sources?.CommonShopJokers is { Length: > 0 };

                    if (hasShop)
                        anyHasShop = true;
                    if (hasPack)
                        anyHasPack = true;
                    if (hasJokerStreamSources)
                        anyHasJokerStreamSources = true;

                    if (hasShop)
                    {
                        int clauseMaxShop = FindMaxSlotIndex(clause.WantedShopSlots) + 1;
                        if (clauseMaxShop > maxShopSlots)
                            maxShopSlots = clauseMaxShop;
                    }
                    if (hasPack)
                    {
                        int clauseMaxPack = (clause.MaxPackSlot ?? 5) + 1;
                        if (clauseMaxPack > maxPackSlots)
                            maxPackSlots = clauseMaxPack;
                    }
                }

                // Use defaults if no clause specifies slots
                bool useDefaults = !anyHasShop && !anyHasPack && !anyHasJokerStreamSources;
                if (useDefaults || (!anyHasShop && maxShopSlots == 0))
                {
                    maxShopSlots = MotelyJsonScoring.GetDefaultShopSlotsForAnte(ante);
                }
                if (useDefaults || (!anyHasPack && maxPackSlots == 0))
                {
                    maxPackSlots = MotelyJsonScoring.GetDefaultPackSlotsForAnte(ante);
                }

                // Create streams ONCE for this ante
                var shopJokerStream =
                    maxShopSlots > 0 ? ctx.CreateShopJokerStreamNew(ante) : default;
                var packStream =
                    maxPackSlots > 0
                        ? ctx.CreateBoosterPackStream(
                            ante,
                            isCached: false,
                            generatedFirstPack: ante > 1
                        )
                        : default;
                var buffoonStream =
                    maxPackSlots > 0 ? ctx.CreateBuffoonPackJokerStream(ante) : default;

                // Walk shops, check ALL clauses
                for (int slot = 0; slot < maxShopSlots; slot++)
                {
                    var jokerItem = shopJokerStream.GetNext(ref ctx);

                    // Check if it's an actual joker
                    var excludedValue = Vector256.Create((int)MotelyItemType.JokerExcludedByStream);
                    var isNotExcluded = ~Vector256.Equals(jokerItem.Value, excludedValue);
                    VectorMask isActualJoker = isNotExcluded;

                    if (!isActualJoker.IsAllFalse())
                    {
                        // Check this joker against ALL clauses
                        for (int c = 0; c < Clauses.Count; c++)
                        {
                            var clause = Clauses[c];
                            if (ante >= clause.WantedAntes.Length || !clause.WantedAntes[ante])
                                continue;

                            bool hasShop = HasShopSlots(clause.WantedShopSlots);
                            bool wantsSlot =
                                useDefaults
                                || !hasShop
                                || (
                                    slot < clause.WantedShopSlots.Length
                                    && clause.WantedShopSlots[slot]
                                );
                            if (wantsSlot)
                            {
                                VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                                clauseMasks[c] |= (isActualJoker & matches);
                            }
                        }
                    }
                }

                // Walk packs, check ALL clauses
                if (maxPackSlots > 0)
                {
                    for (int packSlot = 0; packSlot < maxPackSlots; packSlot++)
                    {
                        var pack = ctx.GetNextBoosterPack(ref packStream);
                        VectorMask isBuffoonPack = VectorEnum256.Equals(
                            pack.GetPackType(),
                            MotelyBoosterPackType.Buffoon
                        );

                        if (!isBuffoonPack.IsAllFalse())
                        {
                            int maxPackSize = 5;
                            // ALWAYS get pack contents to maintain stream sync
                            var packContents = ctx.GetNextBuffoonPackContents(
                                ref buffoonStream,
                                maxPackSize
                            );

                            // Check each joker in the pack against ALL clauses
                            for (
                                int packJokerIndex = 0;
                                packJokerIndex < maxPackSize;
                                packJokerIndex++
                            )
                            {
                                var joker = packContents[packJokerIndex];
                                for (int c = 0; c < Clauses.Count; c++)
                                {
                                    var clause = Clauses[c];
                                    if (
                                        ante >= clause.WantedAntes.Length
                                        || !clause.WantedAntes[ante]
                                    )
                                        continue;

                                    bool hasPack = HasPackSlots(clause.WantedPackSlots);
                                    bool wantsSlot =
                                        useDefaults
                                        || !hasPack
                                        || (
                                            packSlot < clause.WantedPackSlots.Length
                                            && clause.WantedPackSlots[packSlot]
                                        );
                                    if (wantsSlot)
                                    {
                                        VectorMask matches = CheckJokerMatchesClause(joker, clause);
                                        clauseMasks[c] |= (isBuffoonPack & matches);
                                    }
                                }
                            }
                        }
                    }
                }

                // Check Judgement/Tag sources for ALL clauses
                for (int c = 0; c < Clauses.Count; c++)
                {
                    var clause = Clauses[c];
                    if (ante >= clause.WantedAntes.Length || !clause.WantedAntes[ante])
                        continue;
                    if (clause.Sources == null)
                        continue;

                    // Check Judgement tarot joker sources
                    if (clause.Sources.Judgement != null && clause.Sources.Judgement.Length > 0)
                    {
                        var judgementStream = ctx.CreateJudgementJokerStream(ante);
                        var rollIndices = clause.Sources.Judgement;

                        int maxRollIndex = rollIndices[rollIndices.Length - 1];
                        int pos = 0;
                        int nextWanted = rollIndices[0];
                        var excludedValue = Vector256.Create(
                            (int)MotelyItemType.JokerExcludedByStream
                        );

                        for (int r = 0; r <= maxRollIndex; r++)
                        {
                            var jokerItem = ctx.GetNextJoker(ref judgementStream);
                            if (r != nextWanted)
                                continue;

                            var isNotExcluded = ~Vector256.Equals(jokerItem.Value, excludedValue);
                            VectorMask isActualJoker = isNotExcluded;

                            if (!isActualJoker.IsAllFalse())
                            {
                                VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                                clauseMasks[c] |= (isActualJoker & matches);
                            }

                            pos++;
                            if (pos >= rollIndices.Length)
                                break;
                            nextWanted = rollIndices[pos];
                        }
                    }

                    // Check Wraith spectral joker sources
                    if (clause.Sources.Wraith != null && clause.Sources.Wraith.Length > 0)
                    {
                        var wraithStream = ctx.CreateWraithJokerStream(ante);
                        var rollIndices = clause.Sources.Wraith;

                        int maxRollIndex = rollIndices[rollIndices.Length - 1];
                        int pos = 0;
                        int nextWanted = rollIndices[0];
                        var excludedValue = Vector256.Create(
                            (int)MotelyItemType.JokerExcludedByStream
                        );

                        for (int r = 0; r <= maxRollIndex; r++)
                        {
                            var jokerItem = ctx.GetNextJoker(ref wraithStream);
                            if (r != nextWanted)
                                continue;

                            var isNotExcluded = ~Vector256.Equals(jokerItem.Value, excludedValue);
                            VectorMask isActualJoker = isNotExcluded;

                            if (!isActualJoker.IsAllFalse())
                            {
                                VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                                clauseMasks[c] |= (isActualJoker & matches);
                            }

                            pos++;
                            if (pos >= rollIndices.Length)
                                break;
                            nextWanted = rollIndices[pos];
                        }
                    }

                    // Check Rare Tag joker sources
                    if (clause.Sources.RareTag != null && clause.Sources.RareTag.Length > 0)
                    {
                        var rareTagStream = ctx.CreateRareTagJokerStream(ante);
                        var rollIndices = clause.Sources.RareTag;

                        int maxRollIndex = rollIndices[rollIndices.Length - 1];
                        int pos = 0;
                        int nextWanted = rollIndices[0];
                        var excludedValue = Vector256.Create(
                            (int)MotelyItemType.JokerExcludedByStream
                        );

                        for (int r = 0; r <= maxRollIndex; r++)
                        {
                            var jokerItem = ctx.GetNextJoker(ref rareTagStream);
                            if (r != nextWanted)
                                continue;

                            var isNotExcluded = ~Vector256.Equals(jokerItem.Value, excludedValue);
                            VectorMask isActualJoker = isNotExcluded;

                            if (!isActualJoker.IsAllFalse())
                            {
                                VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                                clauseMasks[c] |= (isActualJoker & matches);
                            }

                            pos++;
                            if (pos >= rollIndices.Length)
                                break;
                            nextWanted = rollIndices[pos];
                        }
                    }

                    // Check Uncommon Tag joker sources
                    if (clause.Sources.UncommonTag != null && clause.Sources.UncommonTag.Length > 0)
                    {
                        var uncommonTagStream = ctx.CreateUncommonTagJokerStream(ante);
                        var rollIndices = clause.Sources.UncommonTag;

                        int maxRollIndex = rollIndices[rollIndices.Length - 1];
                        int pos = 0;
                        int nextWanted = rollIndices[0];
                        var excludedValue = Vector256.Create(
                            (int)MotelyItemType.JokerExcludedByStream
                        );

                        for (int r = 0; r <= maxRollIndex; r++)
                        {
                            var jokerItem = ctx.GetNextJoker(ref uncommonTagStream);
                            if (r != nextWanted)
                                continue;

                            var isNotExcluded = ~Vector256.Equals(jokerItem.Value, excludedValue);
                            VectorMask isActualJoker = isNotExcluded;

                            if (!isActualJoker.IsAllFalse())
                            {
                                VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                                clauseMasks[c] |= (isActualJoker & matches);
                            }

                            pos++;
                            if (pos >= rollIndices.Length)
                                break;
                            nextWanted = rollIndices[pos];
                        }
                    }

                    // Check RiffRaff joker sources
                    if (clause.Sources.RiffRaff != null && clause.Sources.RiffRaff.Length > 0)
                    {
                        var riffRaffStream = ctx.CreateRiffRaffJokerStream(ante);
                        var rollIndices = clause.Sources.RiffRaff;

                        int maxRollIndex = rollIndices[rollIndices.Length - 1];
                        int pos = 0;
                        int nextWanted = rollIndices[0];
                        var excludedValue = Vector256.Create(
                            (int)MotelyItemType.JokerExcludedByStream
                        );

                        for (int r = 0; r <= maxRollIndex; r++)
                        {
                            var jokerItem = ctx.GetNextJoker(ref riffRaffStream);
                            if (r != nextWanted)
                                continue;

                            var isNotExcluded = ~Vector256.Equals(jokerItem.Value, excludedValue);
                            VectorMask isActualJoker = isNotExcluded;

                            if (!isActualJoker.IsAllFalse())
                            {
                                VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                                clauseMasks[c] |= (isActualJoker & matches);
                            }

                            pos++;
                            if (pos >= rollIndices.Length)
                                break;
                            nextWanted = rollIndices[pos];
                        }
                    }

                    // ========== RAW SHOP JOKER STREAMS (FAST PRE-FILTER) ==========
                    // Check Uncommon Shop Joker sources (direct stream access - no shop item type check)
                    if (
                        clause.Sources.UncommonShopJokers != null
                        && clause.Sources.UncommonShopJokers.Length > 0
                    )
                    {
                        var uncommonShopStream = ctx.CreateUncommonShopJokerStream(ante);
                        var rollIndices = clause.Sources.UncommonShopJokers;

                        int maxRollIndex = rollIndices[rollIndices.Length - 1];
                        int pos = 0;
                        int nextWanted = rollIndices[0];

                        for (int r = 0; r <= maxRollIndex; r++)
                        {
                            var jokerItem = ctx.GetNextJoker(ref uncommonShopStream);
                            if (r != nextWanted)
                                continue;

                            VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                            clauseMasks[c] |= matches;

                            pos++;
                            if (pos >= rollIndices.Length)
                                break;
                            nextWanted = rollIndices[pos];
                        }
                    }

                    // Check Rare Shop Joker sources (direct stream access - no shop item type check)
                    if (
                        clause.Sources.RareShopJokers != null
                        && clause.Sources.RareShopJokers.Length > 0
                    )
                    {
                        var rareShopStream = ctx.CreateRareShopJokerStream(ante);
                        var rollIndices = clause.Sources.RareShopJokers;

                        int maxRollIndex = rollIndices[rollIndices.Length - 1];
                        int pos = 0;
                        int nextWanted = rollIndices[0];

                        for (int r = 0; r <= maxRollIndex; r++)
                        {
                            var jokerItem = ctx.GetNextJoker(ref rareShopStream);
                            if (r != nextWanted)
                                continue;

                            VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                            clauseMasks[c] |= matches;

                            pos++;
                            if (pos >= rollIndices.Length)
                                break;
                            nextWanted = rollIndices[pos];
                        }
                    }

                    // Check Common Shop Joker sources (direct stream access - no shop item type check)
                    if (
                        clause.Sources.CommonShopJokers != null
                        && clause.Sources.CommonShopJokers.Length > 0
                    )
                    {
                        var commonShopStream = ctx.CreateCommonShopJokerStream(ante);
                        var rollIndices = clause.Sources.CommonShopJokers;

                        int maxRollIndex = rollIndices[rollIndices.Length - 1];
                        int pos = 0;
                        int nextWanted = rollIndices[0];

                        for (int r = 0; r <= maxRollIndex; r++)
                        {
                            var jokerItem = ctx.GetNextJoker(ref commonShopStream);
                            if (r != nextWanted)
                                continue;

                            VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                            clauseMasks[c] |= matches;

                            pos++;
                            if (pos >= rollIndices.Length)
                                break;
                            nextWanted = rollIndices[pos];
                        }
                    }
                }
            }

            // ALL clauses must match - combine with AND
            VectorMask finalMask = clauseMasks[0];
            for (int c = 1; c < clauseMasks.Length; c++)
            {
                finalMask &= clauseMasks[c];
            }

            // If any clause found nothing, fail
            if (finalMask.IsAllFalse())
            {
                DebugLogger.Log($"[JOKER VECTORIZED] Not all clauses matched - failing all seeds");
                return VectorMask.NoBitsSet;
            }

            DebugLogger.Log($"[JOKER VECTORIZED] Final result mask: {finalMask.Value:X}");

            // Check if any clause has Min > 1, if not, return boolean result directly
            bool hasMinThreshold = Clauses.Any(c => c.Min.HasValue && c.Min.Value > 1);
            if (!hasMinThreshold)
            {
                return finalMask;
            }

            // For Min thresholds, we need to count actual occurrences
            // This is slower but necessary for accuracy
            DebugLogger.Log($"[JOKER VECTORIZED] Checking Min thresholds");
            // Copy struct fields to local variables for lambda (required for struct members)
            var clauses = Clauses;
            var minAnte = MinAnte;
            var maxAnte = MaxAnte;

            return ctx.SearchIndividualSeeds(
                finalMask,
                (ref MotelySingleSearchContext singleCtx) =>
                {
                    // Check each clause's Min threshold
                    foreach (var clause in clauses)
                    {
                        // Skip clauses without Min requirement or Min <= 1
                        if (!clause.Min.HasValue || clause.Min.Value <= 1)
                            continue;

                        // Count total joker occurrences across ALL wanted antes and sources
                        int totalCount = 0;

                        for (int ante = minAnte; ante <= maxAnte; ante++)
                        {
                            if (ante >= clause.WantedAntes.Length || !clause.WantedAntes[ante])
                                continue; // Skip antes not wanted by this clause

                            // Count jokers from all sources for this ante
                            int anteCount = CountJokerOccurrences(ref singleCtx, clause, ante);
                            totalCount += anteCount;

                            // Early exit if we already exceed the threshold
                            if (totalCount >= clause.Min.Value)
                                break;
                        }

                        // Check Min threshold
                        if (totalCount < clause.Min.Value)
                            return false; // Doesn't meet minimum count
                    }

                    return true; // All Min thresholds satisfied
                }
            );
        }

        private static int CountJokerOccurrences(
            ref MotelySingleSearchContext ctx,
            MotelyJsonJokerFilterClause clause,
            int ante
        )
        {
            int count = 0;

            // Check Judgement sources
            if (clause.Sources?.Judgement != null && clause.Sources.Judgement.Length > 0)
            {
                var judgementStream = ctx.CreateJudgementJokerStream(ante);
                var rollIndices = clause.Sources.Judgement;

                int maxRollIndex = rollIndices[rollIndices.Length - 1];
                int pos = 0;
                int nextWanted = rollIndices[0];

                for (int r = 0; r <= maxRollIndex; r++)
                {
                    var jokerItem = ctx.GetNextJoker(ref judgementStream);
                    if (r != nextWanted)
                        continue;

                    if (jokerItem.TypeCategory == MotelyItemTypeCategory.Joker)
                    {
                        if (CheckJokerMatchesClause(jokerItem, clause))
                            count++;
                    }

                    pos++;
                    if (pos >= rollIndices.Length)
                        break;
                    nextWanted = rollIndices[pos];
                }
            }

            // Check Wraith sources
            if (clause.Sources?.Wraith != null && clause.Sources.Wraith.Length > 0)
            {
                var wraithStream = ctx.CreateWraithJokerStream(ante);
                var rollIndices = clause.Sources.Wraith;

                int maxRollIndex = rollIndices[rollIndices.Length - 1];
                int pos = 0;
                int nextWanted = rollIndices[0];

                for (int r = 0; r <= maxRollIndex; r++)
                {
                    var jokerItem = ctx.GetNextJoker(ref wraithStream);
                    if (r != nextWanted)
                        continue;

                    if (jokerItem.TypeCategory == MotelyItemTypeCategory.Joker)
                    {
                        if (CheckJokerMatchesClause(jokerItem, clause))
                            count++;
                    }

                    pos++;
                    if (pos >= rollIndices.Length)
                        break;
                    nextWanted = rollIndices[pos];
                }
            }

            // Check Rare Tag sources
            if (clause.Sources?.RareTag != null && clause.Sources.RareTag.Length > 0)
            {
                var rareTagStream = ctx.CreateRareTagJokerStream(ante);
                var rollIndices = clause.Sources.RareTag;

                int maxRollIndex = rollIndices[rollIndices.Length - 1];
                int pos = 0;
                int nextWanted = rollIndices[0];

                for (int r = 0; r <= maxRollIndex; r++)
                {
                    var jokerItem = ctx.GetNextJoker(ref rareTagStream);
                    if (r != nextWanted)
                        continue;

                    if (jokerItem.TypeCategory == MotelyItemTypeCategory.Joker)
                    {
                        if (CheckJokerMatchesClause(jokerItem, clause))
                            count++;
                    }

                    pos++;
                    if (pos >= rollIndices.Length)
                        break;
                    nextWanted = rollIndices[pos];
                }
            }

            // Check Shop sources
            if (clause.Sources?.ShopSlots != null && clause.Sources.ShopSlots.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante);

                for (int slot = 0; slot < clause.Sources.ShopSlots.Length; slot++)
                {
                    if (clause.Sources.ShopSlots[slot] == 0)
                        continue;

                    // Skip to the wanted slot
                    for (int skip = 0; skip < slot; skip++)
                    {
                        ctx.GetNextShopItem(ref shopStream);
                    }

                    var shopItem = ctx.GetNextShopItem(ref shopStream);
                    if (shopItem.TypeCategory == MotelyItemTypeCategory.Joker)
                    {
                        if (CheckJokerMatchesClause(shopItem, clause))
                            count++;
                    }
                }
            }

            // Check Pack sources
            if (clause.Sources?.PackSlots != null && clause.Sources.PackSlots.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);

                for (int packIndex = 0; packIndex < clause.Sources.PackSlots.Length; packIndex++)
                {
                    if (clause.Sources.PackSlots[packIndex] == 0)
                        continue;

                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                    {
                        var packSize = (int)pack.GetPackSize();
                        for (int i = 0; i < packSize; i++)
                        {
                            var item = ctx.GetNextJoker(ref buffoonStream);
                            if (item.TypeCategory == MotelyItemTypeCategory.Joker)
                            {
                                if (CheckJokerMatchesClause(item, clause))
                                    count++;
                            }
                        }
                    }
                }
            }

            return count;
        }

        private static bool CheckJokerMatchesClause(
            MotelyItem item,
            MotelyJsonJokerFilterClause clause
        )
        {
            // Check joker type match
            if (clause.JokerType.HasValue && item.Value != (int)clause.JokerType.Value)
                return false;

            // Check edition match
            if (clause.EditionEnum.HasValue && item.Edition != clause.EditionEnum.Value)
                return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopVectorized(
            ref MotelyVectorSearchContext ctx,
            int ante,
            MotelyJsonJokerFilterClause clause,
            ref MotelyVectorShopItemStream shopStream,
            MotelyRunState runState
        )
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;

            // Use min/max shop slot range (supports unlimited slots like 0-1000)
            int minSlot = clause.MinShopSlot ?? 0;
            int maxSlot =
                clause.MaxShopSlot
                ?? (
                    clause.MaxShopSlotsNeeded > 0 ? clause.MaxShopSlotsNeeded : (ante == 1 ? 4 : 6)
                );

            // Skip to minSlot by consuming unwanted items
            for (int skip = 0; skip < minSlot; skip++)
            {
                ctx.GetNextShopItem(ref shopStream);
            }

            // Check slots from minSlot to maxSlot (inclusive)
            for (int slot = minSlot; slot <= maxSlot; slot++)
            {
                // Get the shop item - the stream handles all rate calculations internally
                var item = ctx.GetNextShopItem(ref shopStream);

                // Check if this slot is in the bitmask
                // This check is now handled by the continue statement above
                {
                    DebugLogger.Log(
                        $"[JOKER VECTORIZED] Checking shop slot {slot}: item type category={item.TypeCategory}"
                    );

                    // Check if this slot has a joker
                    var isJoker = VectorEnum256.Equals(
                        item.TypeCategory,
                        MotelyItemTypeCategory.Joker
                    );

                    // Check if any lanes have jokers - ONLY CHECK VALID LANES!
                    uint jokerMask = 0;
                    for (int i = 0; i < 8; i++)
                        if (ctx.IsLaneValid(i) && isJoker[i] == -1)
                            jokerMask |= (1u << i);

                    if (jokerMask != 0) // Any lanes have jokers
                    {
                        DebugLogger.Log(
                            $"[JOKER VECTORIZED] Found joker at shop slot {slot}: {item.Type[0]}, expecting: {clause.JokerType}"
                        );
                        // Check if it matches our clause
                        VectorMask matches = CheckJokerMatchesClause(item, clause);
                        DebugLogger.Log($"[JOKER VECTORIZED] Matches mask={matches.Value:X}");
                        foundInShop |= matches;
                    }
                }
            }

            return foundInShop;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopJokerVectorizedNew(
            MotelyJsonJokerFilterClause clause,
            MotelyVectorSearchContext ctx,
            ref MotelyVectorShopJokerStream shopJokerStream,
            int ante
        )
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;

            // Calculate max slots based on ante (dynamic per-ante calculation)
            int maxSlot;
            if (HasShopSlots(clause.WantedShopSlots))
            {
                // User specified slots - find the highest wanted slot
                maxSlot = 0;
                for (int i = 0; i < clause.WantedShopSlots.Length; i++)
                    if (clause.WantedShopSlots[i])
                        maxSlot = i;
                maxSlot++; // Convert index to count
            }
            else
            {
                // No slots specified - use ante-based defaults
                maxSlot = MotelyJsonScoring.GetDefaultShopSlotsForAnte(ante);
            }

            string jokerDbg =
                clause.JokerTypes?.Count > 0
                    ? string.Join("|", clause.JokerTypes)
                    : clause.JokerType?.ToString() ?? "?";
            DebugLogger.Log($"[SHOP CHECK] Ante {ante}, {jokerDbg}: checking {maxSlot} shop slots");

            // Check each shop slot using the self-contained stream
            for (int slot = 0; slot < maxSlot; slot++)
            {
                // ALWAYS get the next item to maintain stream synchronization
                var jokerItem = shopJokerStream.GetNext(ref ctx);

                // Only check/score if this slot is wanted (or if no specific slots wanted, check all)
                if (!HasShopSlots(clause.WantedShopSlots) || clause.WantedShopSlots[slot])
                {
                    // PURE VECTORIZED CHECK - no per-lane loops!
                    // Check if it's not JokerExcludedByStream using SIMD compare
                    var excludedValue = Vector256.Create((int)MotelyItemType.JokerExcludedByStream);
                    var isNotExcluded = ~Vector256.Equals(jokerItem.Value, excludedValue);
                    VectorMask isActualJoker = isNotExcluded;

                    if (!isActualJoker.IsAllFalse())
                    {
                        // Check if the joker matches our clause criteria
                        VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                        foundInShop |= (isActualJoker & matches);
                    }
                }
            }

            return foundInShop;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckPackJokersVectorized(
            MotelyJsonJokerFilterClause clause,
            MotelyVectorSearchContext ctx,
            ref MotelyVectorBoosterPackStream packStream,
            ref MotelyVectorJokerStream buffoonStream,
            int ante
        )
        {
            VectorMask foundInPack = VectorMask.NoBitsSet;

            // Use config if provided, otherwise use default pack limits
            int actualPackLimit = clause.MaxPackSlot.HasValue
                ? clause.MaxPackSlot.Value + 1
                : MotelyJsonScoring.GetDefaultPackSlotsForAnte(ante);

            // Check enough packs to cover the slots, but never exceed actual pack limit
            bool hasSpecificSlots = HasPackSlots(clause.WantedPackSlots);
            int maxPacksToCheck = hasSpecificSlots ? actualPackLimit : actualPackLimit;

            for (int packIndex = 0; packIndex < maxPacksToCheck; packIndex++)
            {
                // Always get next pack to maintain stream sync
                var pack = ctx.GetNextBoosterPack(ref packStream);

                // Check if it's a Buffoon pack - we MUST consume jokers if it is!
                VectorMask isBuffoonPack = VectorEnum256.Equals(
                    pack.GetPackType(),
                    MotelyBoosterPackType.Buffoon
                );

                if (!isBuffoonPack.IsAllFalse())
                {
                    // FIXED: Handle different pack sizes properly per-lane
                    // Get pack sizes for each lane that has a Buffoon pack
                    var packSizes = pack.GetPackSize();

                    // Determine max pack size across all lanes to ensure we consume enough from stream
                    // Standard = 2, Jumbo = 3, Mega = 4-5 (let's use 5 to be safe)
                    int maxPackSize = 5; // Maximum possible pack size

                    // Get ALL jokers from the pack (up to max size) to keep stream in sync
                    var packContents = ctx.GetNextBuffoonPackContents(
                        ref buffoonStream,
                        maxPackSize
                    );

                    // Only SCORE if this pack slot is wanted
                    if (!hasSpecificSlots || clause.WantedPackSlots[packIndex])
                    {
                        // Check if it's a Mega pack if required
                        if (clause.Sources?.RequireMega == true)
                        {
                            VectorMask isMegaPack = VectorEnum256.Equals(
                                packSizes,
                                MotelyBoosterPackSize.Mega
                            );
                            isBuffoonPack &= isMegaPack;
                        }

                        // Check each joker in the pack for scoring
                        // We check all positions but only count valid ones based on actual pack size
                        for (int i = 0; i < maxPackSize; i++)
                        {
                            // Create mask for lanes where this card position is valid
                            // Standard (2): positions 0,1 valid
                            // Jumbo (3): positions 0,1,2 valid
                            // Mega (4-5): positions 0,1,2,3,4 valid
                            VectorMask isValidPosition = i switch
                            {
                                0 or 1 => VectorMask.AllBitsSet, // All packs have at least 2 cards
                                2 => ~VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Normal),
                                3 or 4 => VectorEnum256.Equals(
                                    packSizes,
                                    MotelyBoosterPackSize.Mega
                                ),
                                _ => VectorMask.NoBitsSet,
                            };

                            var jokerItem = packContents[i];
                            VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                            foundInPack |= (isBuffoonPack & isValidPosition & matches);
                        }
                    }
                }
            }

            return foundInPack;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckJokerMatchesClause(
            MotelyItemVector item,
            MotelyJsonJokerFilterClause clause
        )
        {
            VectorMask matches = VectorMask.AllBitsSet;

            // Check type if specified - PURE SIMD, no loops over lanes!
            if (clause.JokerTypes != null && clause.JokerTypes.Count > 0)
            {
                VectorMask typeMatch = VectorMask.NoBitsSet;
                foreach (var jokerType in clause.JokerTypes)
                {
                    var targetType = (MotelyItemType)(
                        (int)MotelyItemTypeCategory.Joker | (int)jokerType
                    );
                    typeMatch |= VectorEnum256.Equals(item.Type, targetType);
                }
                matches &= typeMatch;
            }
            else if (clause.JokerType != null)
            {
                var targetType = (MotelyItemType)(
                    (int)MotelyItemTypeCategory.Joker | (int)clause.JokerType
                );
                matches &= VectorEnum256.Equals(item.Type, targetType);
            }
            else
            {
                // Match any joker
                matches &= VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.Joker);
            }

            // Check edition if specified - PURE SIMD!
            if (clause.EditionEnum.HasValue)
            {
                VectorMask editionMatches = VectorEnum256.Equals(
                    item.Edition,
                    clause.EditionEnum.Value
                );
                DebugLogger.Log(
                    $"[JOKER EDITION CHECK] Required: {clause.EditionEnum.Value}, Item editions: {item.Edition[0]},{item.Edition[1]},{item.Edition[2]},{item.Edition[3]},{item.Edition[4]},{item.Edition[5]},{item.Edition[6]},{item.Edition[7]}"
                );
                DebugLogger.Log(
                    $"[JOKER EDITION CHECK] Edition matches mask: {editionMatches.Value:X}"
                );
                matches &= editionMatches;
            }

            return matches;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetMaxShopSlot(ulong bitmask, int ante)
        {
            if (bitmask == 0)
                return ante == 1 ? 4 : 6; // Default 4/6 concept

            // Find highest bit + 1, but ensure minimum of 6 for ante 2+ to handle extended slots
            int maxSpecified = 64 - System.Numerics.BitOperations.LeadingZeroCount(bitmask);
            return ante == 1 ? Math.Max(4, maxSpecified) : Math.Max(6, maxSpecified);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckJokerTypeMatch(
            MotelyJoker joker,
            MotelyJsonJokerFilterClause clause
        )
        {
            if (!clause.IsWildcard)
            {
                if (clause.JokerTypes?.Count > 0)
                {
                    return clause.JokerTypes.Contains(joker);
                }
                else
                {
                    return clause.JokerType.HasValue && joker == clause.JokerType.Value;
                }
            }
            else
            {
                return CheckWildcardMatch(joker, clause.WildcardEnum);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckWildcardMatch(
            MotelyJoker joker,
            MotelyJsonConfigWildcards? wildcard
        )
        {
            if (!wildcard.HasValue)
                return false;
            if (wildcard == MotelyJsonConfigWildcards.AnyJoker)
                return true;

            var rarity = (MotelyJokerRarity)((int)joker & Motely.JokerRarityMask);
            return wildcard switch
            {
                MotelyJsonConfigWildcards.AnyCommon => rarity == MotelyJokerRarity.Common,
                MotelyJsonConfigWildcards.AnyUncommon => rarity == MotelyJokerRarity.Uncommon,
                MotelyJsonConfigWildcards.AnyRare => rarity == MotelyJokerRarity.Rare,
                _ => false,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckShopJokersSingleStatic(
            ref MotelySingleSearchContext ctx,
            MotelyJsonJokerFilterClause clause,
            int ante,
            ref MotelySingleShopItemStream shopStream
        )
        {
            DebugLogger.Log($"[SHOP CHECK] Looking for {clause.JokerType} in ante {ante}");

            // Determine how many slots to check - use config if provided
            int maxSlot = 0;
            if (clause.MaxShopSlot.HasValue)
            {
                // Use configured max shop slot
                maxSlot = clause.MaxShopSlot.Value + 1;
                DebugLogger.Log(
                    $"[SHOP CHECK] Using configured MaxShopSlot, checking {maxSlot} slots"
                );
            }
            else if (!HasShopSlots(clause.WantedShopSlots))
            {
                // No specific slots wanted - use ante-based defaults (pifreak's rules)
                maxSlot = MotelyJsonScoring.GetDefaultShopSlotsForAnte(ante);
                DebugLogger.Log(
                    $"[SHOP CHECK] No specific slots wanted, checking default {maxSlot} slots for ante {ante}"
                );
            }
            else
            {
                // Has specific shop slots - find the max
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
                var item = ctx.GetNextShopItem(ref shopStream);

                DebugLogger.Log(
                    $"[SHOP CHECK] Slot {slot}: {item.Type} (wanted: {(HasShopSlots(clause.WantedShopSlots) ? clause.WantedShopSlots[slot] : "all")})"
                );

                // Check if this slot is wanted (or if no specific slots wanted, check all)
                if (!HasShopSlots(clause.WantedShopSlots) || clause.WantedShopSlots[slot])
                {
                    if (item.TypeCategory == MotelyItemTypeCategory.Joker)
                    {
                        DebugLogger.Log(
                            $"[SHOP CHECK] Found item {item.Type} in slot {slot}, looking for {clause.JokerType}"
                        );
                        bool matches = false;
                        if (!clause.IsWildcard)
                        {
                            if (clause.JokerTypes?.Count > 0)
                            {
                                // Multi-value: check if item matches any of the joker types
                                foreach (var jokerType in clause.JokerTypes)
                                {
                                    var targetType = (MotelyItemType)(
                                        (int)MotelyItemTypeCategory.Joker | (int)jokerType
                                    );
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
                                matches =
                                    item.Type
                                    == (MotelyItemType)(
                                        (int)MotelyItemTypeCategory.Joker
                                        | (int)clause.JokerType.Value
                                    );
                            }
                        }
                        else
                        {
                            matches = CheckWildcardMatch(
                                (MotelyJoker)item.Type,
                                clause.WildcardEnum
                            );
                        }

                        DebugLogger.Log(
                            $"[SHOP CHECK] Type match: {matches}, item.Type={(int)item.Type}"
                        );
                        DebugLogger.Log(
                            $"[SHOP CHECK] Cast (MotelyJoker)item.Type={(int)(MotelyJoker)item.Type}"
                        );

                        if (matches && CheckEditionAndStickersSingle(item, clause))
                        {
                            DebugLogger.Log(
                                $"[SHOP CHECK] MATCH! Found {item.Type} in slot {slot}"
                            );
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckPackJokersSingleStatic(
            ref MotelySingleSearchContext ctx,
            MotelyJsonJokerFilterClause clause,
            int ante,
            ref MotelySingleBoosterPackStream packStream
        )
        {
            // Use config if provided, otherwise use default pack limits
            int maxPacks = clause.MaxPackSlot.HasValue
                ? clause.MaxPackSlot.Value + 1
                : (ante == 1 ? 4 : 6);
            var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);

            for (int packIndex = 0; packIndex < maxPacks; packIndex++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                var packSize = pack.GetPackSize();

                if (pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                {
                    var packContents = ctx.GetNextBuffoonPackContents(ref buffoonStream, packSize);

                    // Check if this pack slot is wanted
                    if (!HasPackSlots(clause.WantedPackSlots) || clause.WantedPackSlots[packIndex])
                    {
                        if (
                            clause.Sources?.RequireMega == true
                            && pack.GetPackSize() != MotelyBoosterPackSize.Mega
                        )
                            continue;

                        for (int i = 0; i < packContents.Length; i++)
                        {
                            var item = packContents[i];
                            var joker = (MotelyJoker)item.Type;
                            bool matches = false;
                            if (!clause.IsWildcard)
                            {
                                if (clause.JokerTypes?.Count > 0)
                                {
                                    // Multi-value: check if item matches any of the joker types
                                    foreach (var jokerType in clause.JokerTypes)
                                    {
                                        var targetType = (MotelyItemType)(
                                            (int)MotelyItemTypeCategory.Joker | (int)jokerType
                                        );
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
                                    matches =
                                        item.Type
                                        == (MotelyItemType)(
                                            (int)MotelyItemTypeCategory.Joker
                                            | (int)clause.JokerType.Value
                                        );
                                }
                            }
                            else
                            {
                                matches = CheckWildcardMatch(joker, clause.WildcardEnum);
                            }

                            if (matches && CheckEditionAndStickersSingle(item, clause))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckEditionAndStickersSingle(
            in MotelyItem item,
            MotelyJsonJokerFilterClause clause
        )
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
                        _ => true,
                    };
                    if (!hasSticker)
                        return false;
                }
            }

            return true;
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
        private static bool HasPackSlots(bool[] slots)
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
    }
}
