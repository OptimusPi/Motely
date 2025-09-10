using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on spectral card criteria from JSON configuration.
/// </summary>
public struct MotelyJsonSpectralCardFilterDesc(List<MotelyJsonSpectralFilterClause> spectralClauses)
    : IMotelySeedFilterDesc<MotelyJsonSpectralCardFilterDesc.MotelyJsonSpectralCardFilter>
{
    private readonly List<MotelyJsonSpectralFilterClause> _spectralClauses = spectralClauses;

    public MotelyJsonSpectralCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Calculate ante range from bitmasks
        var (minAnte, maxAnte) = MotelyJsonFilterClause.CalculateAnteRange(_spectralClauses);

        // Cache streams for all antes we'll check
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheBoosterPackStream(ante);
            ctx.CacheShopStream(ante);
        }

        return new MotelyJsonSpectralCardFilter(_spectralClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonSpectralCardFilter(List<MotelyJsonSpectralFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonSpectralFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;

            // Quick vectorized check for potential spectral cards
            VectorMask hasPotential = VectorMask.NoBitsSet;
            
            if (DebugLogger.IsEnabled)
            {
                DebugLogger.Log($"[DEBUG] SpectralFilter: Checking antes {_minAnte} to {_maxAnte}");
            }

            for (int ante = _minAnte; ante <= _maxAnte; ante++)
            {
                // Check shop slots for any spectral cards
                var shopStream = ctx.CreateShopItemStream(ante);
                // Use proper shop slot limits based on clause configuration
                int maxShopSlots = GetMaxShopSlotsNeeded();
                for (int shopSlot = 0; shopSlot < maxShopSlots; shopSlot++)
                {
                    var shopItem = ctx.GetNextShopItem(ref shopStream);
                    var shopPotential = VectorEnum256.Equals(shopItem.TypeCategory, MotelyItemTypeCategory.SpectralCard);
                    hasPotential |= shopPotential;
                }

                // Check spectral and arcana packs for any spectral cards
                var packStream = ctx.CreateBoosterPackStream(ante);
                var spectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                var arcanaStream = ctx.CreateArcanaPackTarotStream(ante);
                int totalPacks = ante == 1 ? 4 : 6;
                for (int packSlot = 0; packSlot < totalPacks; packSlot++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    VectorMask isSpectralPack = VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Spectral);
                    VectorMask isArcanaPack = VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Arcana);

                    if (isSpectralPack.IsPartiallyTrue())
                    {
                        var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize()[0]);
                        for (int j = 0; j < contents.Length; j++)
                        {
                            if (DebugLogger.IsEnabled)
                            {
                                DebugLogger.Log($"[DEBUG] Spectral pack slot {packSlot}, card {j}: Type={contents[j].Type}, TypeCategory={contents[j].TypeCategory}");
                            }
                            var packPotential = VectorEnum256.Equals(contents[j].TypeCategory, MotelyItemTypeCategory.SpectralCard);
                            hasPotential |= (packPotential & isSpectralPack);
                        }
                    }

                    if (isArcanaPack.IsPartiallyTrue())
                    {
                        var contents = ctx.GetNextArcanaPackContents(ref arcanaStream, pack.GetPackSize()[0]);
                        for (int j = 0; j < contents.Length; j++)
                        {
                            // Check for The Soul in Arcana packs
                            var soulPotential = VectorEnum256.Equals(contents[j].Type, (MotelyItemType)MotelySpectralCard.Soul);
                            hasPotential |= (soulPotential & isArcanaPack);
                        }
                    }
                }
            }

            // Early exit if no potential matches
            if (DebugLogger.IsEnabled)
                DebugLogger.Log($"[DEBUG] SpectralFilter: hasPotential={hasPotential}");
            if (hasPotential.IsAllFalse())
                return VectorMask.NoBitsSet;

            // Copy struct fields to local variables for lambda
            var clauses = _clauses;
            int minAnte = _minAnte;
            int maxAnte = _maxAnte;
            int maxShopSlotsNeeded = GetMaxShopSlotsNeeded();

            // Now do full individual processing for seeds with potential
            if (DebugLogger.IsEnabled)
                DebugLogger.Log($"[DEBUG] SpectralFilter: Searching individual seeds with hasPotential={hasPotential}");
            return ctx.SearchIndividualSeeds(hasPotential, (ref MotelySingleSearchContext singleCtx) =>
            {
                if (DebugLogger.IsEnabled)
                    DebugLogger.Log($"[DEBUG] SpectralFilter: Processing individual seed");
                // Stack-allocated to avoid heap allocation in hot path!
                Span<bool> clauseMatches = stackalloc bool[clauses.Count];
                for (int i = 0; i < clauseMatches.Length; i++) 
                {
                    clauseMatches[i] = false;
                    if (DebugLogger.IsEnabled)
                        DebugLogger.Log($"[DEBUG] Initialized clause {i} to false");
                }

                for (int ante = minAnte; ante <= maxAnte; ante++)
                {
                    if (DebugLogger.IsEnabled)
                        DebugLogger.Log($"[DEBUG] SpectralFilter: Processing ante {ante}");
                    ulong anteBit = 1UL << (ante - 1);

                    // Create streams ONCE per ante for performance and PRNG correctness
                    var shopStream = singleCtx.CreateShopItemStream(ante);
                    var packStream = singleCtx.CreateBoosterPackStream(ante);
                    var spectralStream = singleCtx.CreateSpectralPackSpectralStream(ante);
                    var arcanaStream = singleCtx.CreateArcanaPackTarotStream(ante);

                    // Process shop slots - MUST iterate ALL slots to maintain PRNG state
                    for (int shopSlot = 0; shopSlot < maxShopSlotsNeeded; shopSlot++)
                    {
                        var shopItem = singleCtx.GetNextShopItem(ref shopStream);

                        ulong shopSlotBit = 1UL << shopSlot;

                        // Check each clause to see if it wants this shop slot
                        for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                        {
                            var clause = clauses[clauseIndex];
                            // Check ante bitmask
                            if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) continue;

                            if (DebugLogger.IsEnabled && shopSlot == 0)
                                DebugLogger.Log($"[DEBUG] Clause {clauseIndex}: Sources={clause.Sources}, PackSlots={clause.Sources?.PackSlots}, ShopSlots={clause.Sources?.ShopSlots}, PackSlotBitmask={clause.PackSlotBitmask:X}, ShopSlotBitmask={clause.ShopSlotBitmask:X}");

                            // Skip shop checking if only packSlots were specified
                            if (clause.Sources != null && clause.Sources.PackSlots != null && clause.Sources.PackSlots.Length > 0 && 
                                (clause.Sources.ShopSlots == null || clause.Sources.ShopSlots.Length == 0)) 
                            {
                                if (DebugLogger.IsEnabled)
                                    DebugLogger.Log($"[DEBUG] Skipping shop slot {shopSlot} for clause {clauseIndex} - only packSlots specified");
                                continue;
                            }

                            // Check shop slot bitmask (0 means check all slots)
                            if (clause.ShopSlotBitmask != 0 && (clause.ShopSlotBitmask & shopSlotBit) == 0) continue;

                            bool typeMatches;
                            if (clause.SpectralType.HasValue && shopItem.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                            {
                                var spectralCard = new MotelyItem(shopItem.Value).GetSpectral();
                                typeMatches = spectralCard == clause.SpectralType.Value;
                            }
                            else if (!clause.SpectralType.HasValue)
                            {
                                typeMatches = shopItem.TypeCategory == MotelyItemTypeCategory.SpectralCard;
                            }
                            else
                            {
                                typeMatches = false;
                            }

                            if (typeMatches)
                            {
                                bool editionMatches = !clause.EditionEnum.HasValue ||
                                                    shopItem.Edition == clause.EditionEnum.Value;

                                if (typeMatches && editionMatches)
                                {
                                    if (DebugLogger.IsEnabled)
                                    {
                                        var spectralCard = shopItem.TypeCategory == MotelyItemTypeCategory.SpectralCard ? 
                                            new MotelyItem(shopItem.Value).GetSpectral().ToString() : "NOT_SPECTRAL";
                                        DebugLogger.Log($"[DEBUG] MATCH! Shop slot {shopSlot}: Type={shopItem.Type}, TypeCategory={shopItem.TypeCategory}, SpectralCard={spectralCard}, Setting clause {clauseIndex} to true");
                                    }
                                    clauseMatches[clauseIndex] = true;
                                }
                            }
                        }
                    }

                    // Process packs - MUST iterate ALL packs to maintain PRNG state
                    int totalPacks = ante == 1 ? 4 : 6;
                    for (int packSlot = 0; packSlot < totalPacks; packSlot++)
                    {
                        var pack = singleCtx.GetNextBoosterPack(ref packStream);
                        var packType = pack.GetPackType();
                        VectorMask isSpectralPack = packType == MotelyBoosterPackType.Spectral ? VectorMask.AllBitsSet : VectorMask.NoBitsSet;
                        VectorMask isArcanaPack = packType == MotelyBoosterPackType.Arcana ? VectorMask.AllBitsSet : VectorMask.NoBitsSet;

                        // Process Spectral packs
                        if (isSpectralPack.IsPartiallyTrue())
                        {
                            var contents = singleCtx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());

                            // Check each clause to see if it wants this pack slot
                            for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                            {
                                var clause = clauses[clauseIndex];
                                // Check ante bitmask
                                if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) continue;

                                // Check pack slot bitmask (0 means check all slots)
                                ulong packSlotBit = 1UL << packSlot;
                                if (clause.PackSlotBitmask != 0 && (clause.PackSlotBitmask & packSlotBit) == 0) continue;

                                // Check pack size requirements if specified
                                bool sizeMatches = true;
                                if (clause.Sources?.RequireMega is true)
                                {
                                    sizeMatches = pack.GetPackSize() == MotelyBoosterPackSize.Mega;
                                }

                                // Check contents
                                for (int j = 0; j < contents.Length; j++)
                                {
                                    bool typeMatches = false;
                                    
                                    // Check multi-value OR match
                                    if (clause.SpectralTypes?.Count > 0)
                                    {
                                        var spectralCard = new MotelyItem(contents[j].Value).GetSpectral();
                                        foreach (var spectralType in clause.SpectralTypes)
                                        {
                                            if (spectralCard == spectralType)
                                            {
                                                typeMatches = true;
                                                break;
                                            }
                                        }
                                    }
                                    // Fallback to single value or wildcard
                                    else if (clause.SpectralType.HasValue)
                                    {
                                        var spectralCard = new MotelyItem(contents[j].Value).GetSpectral();
                                        typeMatches = spectralCard == clause.SpectralType.Value;
                                        if (DebugLogger.IsEnabled && packSlot == 3)
                                            DebugLogger.Log($"[DEBUG] Comparing: spectralCard={spectralCard} vs clause.SpectralType={clause.SpectralType.Value}, typeMatches={typeMatches}");
                                    }
                                    else
                                    {
                                        typeMatches = contents[j].TypeCategory == MotelyItemTypeCategory.SpectralCard;
                                    }

                                    bool editionMatches = !clause.EditionEnum.HasValue ||
                                                        contents[j].Edition == clause.EditionEnum.Value;

                                    if (typeMatches && editionMatches && sizeMatches)
                                    {
                                        clauseMatches[clauseIndex] = true;
                                    }
                                }
                            }
                        }

                        // Also check Arcana packs for The Soul (it's a spectral card that can appear there)
                        if (isArcanaPack.IsPartiallyTrue())
                        {
                            var contents = singleCtx.GetNextArcanaPackContents(ref arcanaStream, pack.GetPackSize());

                            // Check each clause to see if it wants this pack slot
                            for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                            {
                                var clause = clauses[clauseIndex];
                                // Check ante bitmask
                                if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) continue;

                                // Check pack slot bitmask (0 means check all slots)
                                ulong packSlotBit = 1UL << packSlot;
                                if (clause.PackSlotBitmask != 0 && (clause.PackSlotBitmask & packSlotBit) == 0) continue;

                                // Check pack size requirements if specified
                                bool sizeMatches = true;
                                if (clause.Sources?.RequireMega == true)
                                {
                                    sizeMatches = pack.GetPackSize() == MotelyBoosterPackSize.Mega;
                                }

                                // Check contents - The Soul is a spectral card that can appear in Arcana packs
                                for (int j = 0; j < contents.Length; j++)
                                {
                                    // Check if it's The Soul specifically
                                    bool typeMatches = false;
                                    if (clause.SpectralType.HasValue && clause.SpectralType.Value == MotelySpectralCard.Soul)
                                    {
                                        // Check if this is The Soul card (it's a spectral card in Arcana packs)
                                        typeMatches = contents[j].Type == (MotelyItemType)MotelySpectralCard.Soul;
                                    }

                                    bool editionMatches = !clause.EditionEnum.HasValue ||
                                                        contents[j].Edition == clause.EditionEnum.Value;

                                    if (typeMatches && editionMatches && sizeMatches)
                                    {
                                        clauseMatches[clauseIndex] = true;
                                    }
                                }
                            }
                        }
                    }

                }

                // Combine all clause results - ALL clauses must match
                bool allClausesMatched = true;
                for (int i = 0; i < clauseMatches.Length; i++)
                {
                    if (DebugLogger.IsEnabled)
                        DebugLogger.Log($"[DEBUG] SpectralFilter: Clause {i} matches = {clauseMatches[i]}");
                    if (!clauseMatches[i])
                    {
                        allClausesMatched = false;
                        break;
                    }
                }
                
                if (DebugLogger.IsEnabled)
                    DebugLogger.Log($"[DEBUG] SpectralFilter: allClausesMatched = {allClausesMatched}");
                
                return allClausesMatched;
            });
        }

        private int GetMaxShopSlotsNeeded()
        {
            int maxSlots = 0;
            foreach (var clause in _clauses)
            {
                for (int slot = 0; slot < 64; slot++)
                {
                    if ((clause.ShopSlotBitmask & (1UL << slot)) != 0)
                    {
                        maxSlots = Math.Max(maxSlots, slot + 1);
                    }
                }
            }
            return Math.Max(maxSlots, 5); // Default minimum
        }
    }
}