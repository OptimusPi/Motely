using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;
using System.Collections.Generic;
using Motely;
using Motely.Filters;
using System.Diagnostics;
using System.Numerics;

namespace Motely;

public struct OuijaJsonFilterDesc : IMotelySeedFilterDesc<OuijaJsonFilterDesc.OuijaJsonFilter>
{
    public OuijaConfig Config { get; }
    public int Cutoff { get; set; } = 0;

    public OuijaJsonFilterDesc(OuijaConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public OuijaJsonFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        if (Config == null)
        {
            DebugLogger.Log("ERROR: Config is null in CreateFilter");
            throw new InvalidOperationException("Config is null");
        }

        DebugLogger.LogFormat("Creating OuijaJsonFilter with {0} needs and {1} wants", 
            Config.Needs?.Length ?? 0, 
            Config.Wants?.Length ?? 0);
        
        // Cache streams for all filter types and antes used in needs/wants
        var allAntes = new HashSet<int>();
        
        if (Config.Needs != null)
        {
            foreach (var need in Config.Needs)
            {
                if (need == null) continue;
                
                var searchAntes = need.SearchAntes?.Length > 0 ? need.SearchAntes : new[] { need.DesireByAnte };
                foreach (var ante in searchAntes)
                {
                    allAntes.Add(ante);
                    CacheStreamForType(ctx, need, ante);
                }
            }
        }
        
        if (Config.Wants != null)
        {
            foreach (var want in Config.Wants)
            {
                if (want == null) continue;
                
                var searchAntes = want.SearchAntes?.Length > 0 ? want.SearchAntes : new[] { want.DesireByAnte };
                foreach (var ante in searchAntes)
                {
                    allAntes.Add(ante);
                    CacheStreamForType(ctx, want, ante);
                }
            }
        }

        return new OuijaJsonFilter(Config, Cutoff);
    }

    private static void CacheStreamForType(MotelyFilterCreationContext ctx, OuijaConfig.Desire desire, int ante)
    {
        // Use pre-parsed TypeCategory from config loader
        var typeCategory = desire.TypeCategory;
        
        if (!typeCategory.HasValue)
        {
            // Handle special cases that don't map directly to MotelyItemTypeCategory
            if (string.Equals(desire.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
            {
                ctx.CachePseudoHash(MotelyPrngKeys.ShopPack + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.ArcanaPack + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerSoul + ante);
                ctx.CacheTarotStream(ante);
                return;
            }
            else if (string.Equals(desire.Type, "Tag", StringComparison.OrdinalIgnoreCase))
            {
                ctx.CacheTagStream(ante);
                return;
            }
            else if (string.Equals(desire.Type, "Voucher", StringComparison.OrdinalIgnoreCase))
            {
                ctx.CacheVoucherStream(ante);
                return;
            }
            DebugLogger.LogFormat("[OuijaJsonFilterDesc] Unknown type '{0}' (ante {1}) - no canonical PRNG key used", desire.Type, ante);
            return;
        }
        
        var parsedType = typeCategory.Value;
        
        switch (parsedType)
        {
            case MotelyItemTypeCategory.Joker:
                ctx.CachePseudoHash(MotelyPrngKeys.JokerCommon + "sho" + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerUncommon + "sho" + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerRare + "sho" + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerLegendary + "sho" + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerEdition + "sho" + ante);
                break;
            case MotelyItemTypeCategory.PlayingCard:
                DebugLogger.LogFormat("[OuijaJsonFilterDesc] PlayingCard streams not yet implemented for ante {0}", ante);
                break;
            case MotelyItemTypeCategory.TarotCard:
                ctx.CacheTarotStream(ante);
                ctx.CachePseudoHash(MotelyPrngKeys.Tarot + MotelyPrngKeys.ArcanaPack + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.TarotSoul + MotelyPrngKeys.Tarot + ante);
                break;
            case MotelyItemTypeCategory.PlanetCard:
                ctx.CachePseudoHash(MotelyPrngKeys.Planet + MotelyPrngKeys.Shop + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.Planet + MotelyPrngKeys.CelestialPack + ante);
                break;
            case MotelyItemTypeCategory.SpectralCard:
                ctx.CachePseudoHash(MotelyPrngKeys.Spectral + MotelyPrngKeys.Shop + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.Spectral + MotelyPrngKeys.SpectralPack + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.SpectralSoul + ante);
                break;
            default:
                DebugLogger.LogFormat("[OuijaJsonFilterDesc] Unhandled canonical type '{0}' (ante {1}) - no PRNG key used", parsedType, ante);
                break;
        }
    }

    public struct OuijaJsonFilter : IMotelySeedFilter
    {
        private readonly OuijaConfig _config;
        private readonly int _cutoff;
        private readonly MotelyDeck _deck;
        private readonly MotelyStake _stake;
        
        public OuijaJsonFilter(OuijaConfig config, int cutoff = 0)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _cutoff = cutoff;
            
            // Use pre-parsed enums from config
            _deck = config.ParsedDeck;
            _stake = config.ParsedStake;
            
            DebugLogger.LogFormat("[OuijaJsonFilter] Using deck: {0}, stake: {1}", _deck, _stake);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            try
            {
                VectorMask mask = VectorMask.AllBitsSet;
                if (_config == null)
                {
                    DebugLogger.Log("ERROR: Config is null in Filter method");
                    return default;
                }
                var localConfig = _config;
                mask = ProcessNeeds(ref searchContext, mask, localConfig);
                if (mask.IsAllFalse())
                {
                    DebugLogger.Log("All needs failed to match");
                    return mask;
                }
                DebugLogger.LogFormat("After needs, {0} seeds passing", CountBitsSet(mask));
                // Clear results queue before batch
                ResultsQueue = new ConcurrentQueue<OuijaResult>();
                int cutoff = _cutoff;
                mask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
                {
                    // First check any needs that couldn't be vectorized
                    if (!CheckNonVectorizedNeeds(ref singleCtx, localConfig))
                    {
                        DebugLogger.Log("Non-vectorized needs failed to match");
                        return false;
                    }
                        
                    // Then process wants and score
                        var result = ProcessWantsAndScore(ref singleCtx, localConfig);
                    if (result.Success && result.TotalScore >= cutoff)
                        ResultsQueue.Enqueue(result);
                    return result.Success && result.TotalScore >= cutoff;
                });
                return mask;
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("ERROR in Filter: {0}\n{1}", ex.Message, ex.StackTrace ?? "No stack trace");
                return default;
            }
        }

        private static VectorMask ProcessNeeds(ref MotelyVectorSearchContext searchContext, VectorMask mask, OuijaConfig config)
        {
            if (config?.Needs == null || config.Needs.Length == 0)
            {
                DebugLogger.Log("No needs to process - returning full mask");
                return mask;
            }

            // Process each need
            foreach (var need in config.Needs.Where(n => n != null))
            {
                // Check if this is a non-vectorizable need
                bool isNonVectorizable = false;
                
                // SoulJoker needs can't be vectorized
                if (string.Equals(need.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
                {
                    DebugLogger.LogFormat("SoulJoker need {0} can't be vectorized", need.Value);
                    isNonVectorizable = true;
                }
                // ALL Joker needs currently can't be vectorized (shop generation not vectorized)
                else if (need.TypeCategory == MotelyItemTypeCategory.Joker)
                {
                    isNonVectorizable = true;
                }
                // Tarot needs can't be vectorized (pack checking not vectorized)
                else if (need.TypeCategory == MotelyItemTypeCategory.TarotCard)
                {
                    isNonVectorizable = true;
                }
                // Planet/Spectral needs are partially vectorizable (shop yes, packs no)
                // Since we need to check packs too, we'll handle them in individual checking
                // TODO: Could optimize by doing shop vector check + pack individual check
                
                if (isNonVectorizable)
                {
                    continue;
                }
                
                var searchAntes = need.SearchAntes?.Length > 0 ? need.SearchAntes : new[] { need.DesireByAnte };
                // For each need, we want to find seeds that have it in ANY of the specified antes (OR logic)
                VectorMask needMask = VectorMask.AllBitsClear;
                foreach (var ante in searchAntes)
                {
                    var anteMask = ProcessNeedVector(ref searchContext, need, ante, VectorMask.AllBitsSet);
                    needMask |= anteMask; // OR operation - seed passes if found in ANY ante
                }
                // After collecting all antes for this need, mask is ANDed with needMask (all needs must be satisfied)
                mask &= needMask;
                int passing = CountBitsSet(mask);
                // Print the relevant enum property for debug clarity
                string itemStr = need.GetDisplayString();
                DebugLogger.LogFormat("After need (type={0}, item={1}): {2} seeds passing", need.Type, itemStr, passing);
                if (mask.IsAllFalse()) 
                {
                    DebugLogger.Log("All seeds filtered out by need - no seeds passed requirements");
                    return mask;
                }
            }
            return mask;
        }

        private static int CountBitsSet(VectorMask mask)
        {
            int count = 0;
            for (int i = 0; i < Vector512<double>.Count; i++)
            {
                if (mask[i]) count++;
            }
            return count;
        }

        private static VectorMask ProcessNeedVector(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, 
            int ante, VectorMask mask)
        {
            if (need == null)
            {
                DebugLogger.Log("Invalid need: null");
                return mask;
            }

            // Use pre-parsed TypeCategory from config loader
            var typeCat = need.TypeCategory;
            if (!typeCat.HasValue)
            {
                // Handle special types that don't map to MotelyItemTypeCategory
                if (string.Equals(need.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
                {
                    // Soul jokers can't be vectorized but we still need to filter!
                    // Return original mask - individual processing will happen in CheckNonVectorizedNeeds
                    // This is because we can't check soul jokers in vector context
                    return mask;
                }
                if (string.Equals(need.Type, "Tag", StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessTagNeed(ref searchContext, need, ante, mask);
                }
                if (string.Equals(need.Type, "Voucher", StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessVoucherNeed(ref searchContext, need, ante, mask);
                }
                Console.WriteLine("⚠️  Unknown need type: {0}", need.Type);
                return mask;
            }
            
            switch(typeCat.Value)
            {
                case MotelyItemTypeCategory.Joker:
                    return ProcessJokerNeed(ref searchContext, need, ante, mask);

                case MotelyItemTypeCategory.TarotCard:
                    return ProcessTarotNeed(ref searchContext, need, ante, mask);

                case MotelyItemTypeCategory.PlanetCard:
                    return ProcessPlanetNeed(ref searchContext, need, ante, mask);

                case MotelyItemTypeCategory.SpectralCard:
                    return ProcessSpectralNeed(ref searchContext, need, ante, mask);

                case MotelyItemTypeCategory.PlayingCard:
                    // TODO: Implement when playing card support is added
                    DebugLogger.LogFormat("PlayingCard needs not yet implemented");
                    return mask;

                default:
                    DebugLogger.LogFormat("❌ Unhandled need type: {0}", need.Type);
                    return mask;
            }
        }

        private static VectorMask ProcessJokerNeed(ref MotelyVectorSearchContext searchContext, 
            OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.JokerEnum.HasValue)
            {
                DebugLogger.LogFormat("❌ No JokerEnum for joker need: {0}", need.Value);
                return default; // Return empty mask
            }

            var targetJoker = need.JokerEnum.Value;
            var jokerRarity = GetJokerRarity(targetJoker);
            
            // Legendary jokers come from Soul cards - can't be vectorized yet
            if (jokerRarity == MotelyJokerRarity.Legendary)
            {
                DebugLogger.LogFormat("[WARNING] Legendary joker {0} needs cannot be vectorized - skipping", targetJoker);
                // TODO: When soul joker vectorization is available, implement here
                return mask; // For now, return original mask (don't filter)
            }

            // For shop jokers, we need vectorized shop generation which isn't available yet
            // TODO: When vectorized shop generation is available, use it here
            DebugLogger.LogFormat("[WARNING] Shop joker filtering not yet vectorized for {0}", targetJoker);
            
            // For now, return the original mask - filtering will happen in individual seed processing
            return mask;
        }

        private static VectorMask ProcessTarotNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.TarotEnum.HasValue)
            {
                DebugLogger.Log("Missing TarotEnum for Tarot need");
                return mask;
            }
            
            // Tarot cards in Arcana packs can't be vectorized yet
            DebugLogger.LogFormat("[WARNING] Tarot pack filtering not yet vectorized");
            return mask; // Return original mask - will be handled in individual processing
        }

        private static VectorMask ProcessPlanetNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.PlanetEnum.HasValue)
            {
                DebugLogger.Log("Missing PlanetEnum for Planet need");
                return mask;
            }
            
            // Use vectorized shop planet filter!
            var shopPlanetMask = searchContext.FilterPlanetCard(ante, need.PlanetEnum.Value, MotelyPrngKeys.Shop);
            
            // Celestial packs can't be vectorized yet
            DebugLogger.LogFormat("[WARNING] Celestial pack filtering not yet vectorized - only checking shop planets");
            
            return mask & shopPlanetMask;
        }

        private static VectorMask ProcessSpectralNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.SpectralEnum.HasValue)
            {
                DebugLogger.Log("Missing SpectralEnum for Spectral need");
                return mask;
            }
            
            // Use vectorized shop spectral filter!
            var shopSpectralMask = searchContext.FilterSpectralCard(ante, need.SpectralEnum.Value, MotelyPrngKeys.Shop);
            
            // Spectral packs can't be vectorized yet
            DebugLogger.LogFormat("[WARNING] Spectral pack filtering not yet vectorized - only checking shop spectrals");
            
            return mask & shopSpectralMask;
        }

        private static VectorMask ProcessTagNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.TagEnum.HasValue)
            {
                DebugLogger.Log("Missing TagEnum for Tag need");
                return mask;
            }
            var targetTag = need.TagEnum.Value;
            MotelyVectorTagStream tagStream = searchContext.CreateTagStream(ante);
            
            // Check if EITHER blind has the tag (OR logic)
            var smallBlindTag = searchContext.GetNextTag(ref tagStream);
            var bigBlindTag = searchContext.GetNextTag(ref tagStream);
            
            var smallMatch = VectorEnum256.Equals(smallBlindTag, targetTag);
            var bigMatch = VectorEnum256.Equals(bigBlindTag, targetTag);
            var hasTag = smallMatch | bigMatch;
            
            // Debug output for single seed case
            if (CountBitsSet(mask) == 1)
            {
                DebugLogger.LogFormat("[DEBUG] Tag check ante {0}: Looking for {1}", ante, targetTag);
                DebugLogger.LogFormat("[DEBUG]   Small blind tags: {0}", smallBlindTag);
                DebugLogger.LogFormat("[DEBUG]   Big blind tags: {0}", bigBlindTag);
                DebugLogger.LogFormat("[DEBUG]   Match result: {0}", hasTag);
            }
            
            return mask & hasTag;
        }

        private static VectorMask ProcessVoucherNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.VoucherEnum.HasValue)
            {
                DebugLogger.Log("Missing VoucherEnum for Voucher need");
                return mask;
            }
            var targetVoucher = need.VoucherEnum.Value;
            var voucherVec = searchContext.GetAnteFirstVoucher(ante);
            return VectorEnum256.Equals(voucherVec, targetVoucher) & mask;
        }

        private static bool ProcessLegendaryJokerFromSoul(ref MotelySingleSearchContext singleCtx, MotelyJoker targetJoker, int ante, MotelyItemEdition? requiredEdition = null)
        {
            // Just cast MotelyJoker to MotelyItemType - the enum already handles the conversion!
            var targetItemType = (MotelyItemType)targetJoker;
            int totalPacks = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante);
            
            bool tarotStreamInit = false;
            MotelySingleTarotStream tarotStream = default;

            DebugLogger.LogFormat("[====ProcessLegendaryJokerFromSoul] Processing legendary joker {0} in ante {1} with total packs {2}",
                targetItemType, ante, totalPacks);
            for (int i = 0; i < totalPacks; i++)
            {
                DebugLogger.LogFormat("===ProcessLegendaryJokerFromSoul] Processing booster pack {0} in ante {1}", i + 1, ante);
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream = singleCtx.CreateArcanaPackTarotStream(ante);
                    }
                    if (singleCtx.GetArcanaPackContents(ref tarotStream, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                    {
                        DebugLogger.LogFormat("[=ProcessLegendaryJokerFromSoul] Found Soul card in Arcana pack in ante {0}", ante);
                        var stream = singleCtx.CreateSoulJokerStream(ante);
                        var joker = singleCtx.NextJoker(ref stream);

                        // Check like PerkeoObservatoryDesc does
                        if (joker.Type == (targetItemType | MotelyItemType.Joker))
                        {
                            DebugLogger.LogFormat("[ProcessLegendaryJokerFromSoul] Found legendary joker {0} in ante {1}",
                                joker, ante);
                            if (requiredEdition.HasValue)
                                return ((int)joker.Edition << Motely.ItemEditionOffset) == (int)requiredEdition.Value;
                            return true;
                        }
                        else
                        {
                            DebugLogger.LogFormat("[----------------] Joker {0} does not match target {1} in ante {2}",
                                (int)joker.Type, (int)targetItemType, ante);
                        }
                    }
                }
            }
            return false;
        }

        // Check needs that couldn't be vectorized
        private static bool CheckNonVectorizedNeeds(ref MotelySingleSearchContext singleCtx, OuijaConfig config)
        {
            if (config?.Needs == null || config.Needs.Length == 0)
                return true;
                
            foreach (var need in config.Needs.Where(n => n != null))
            {
                var searchAntes = need.SearchAntes?.Length > 0 ? need.SearchAntes : new[] { need.DesireByAnte };
                bool foundInAnyAnte = false;
                
                DebugLogger.LogFormat("[Search Antes.......]  {0}", searchAntes.Count());

                foreach (int ante in searchAntes)
                {
                    DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Processing ante {0} for need {1}", ante, need.Value);
                }
                foreach (var ante in searchAntes)
                {
                    DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Checking ante {0} for need {1}", ante, need.Value);
                    // Check needs that require individual processing
                    var typeCat = need.TypeCategory;

                    if (string.Equals(need.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase) ||
                        (typeCat == MotelyItemTypeCategory.Joker && GetJokerRarity(need.JokerEnum ?? default) == MotelyJokerRarity.Legendary))
                    {
                        DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Processing SoulJoker need {0} with edition {1} in ante {2}",
                            need.Value, need.ParsedEdition?.ToString() ?? "None", ante);
                        // Check legendary/soul jokers
                        if (need.JokerEnum.HasValue)
                        {
                            DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Checking legendary joker {0} in ante {1}",
                                need.JokerEnum.Value, ante);

                            if (ProcessLegendaryJokerFromSoul(ref singleCtx, need.JokerEnum.Value, ante, need.ParsedEdition))
                            {
                                foundInAnyAnte = true;
                                break;
                            }
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.Joker)
                    {
                        // Check shop jokers
                        if (CheckShopJoker(ref singleCtx, need, ante))
                        {
                            DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Found shop joker {0} with edition {1} in ante {2}",
                                need.JokerEnum?.ToString() ?? need.Value, need.ParsedEdition?.ToString() ?? "None", ante);
                            foundInAnyAnte = true;
                            break;
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.TarotCard)
                    {
                        // Check tarot in packs
                        if (CheckTarotInPacks(ref singleCtx, need, ante))
                        {
                            foundInAnyAnte = true;
                            break;
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.PlanetCard)
                    {
                        // Check planet in celestial packs (shop already checked by vector)
                        if (CheckPlanetInPacks(ref singleCtx, need, ante))
                        {
                            foundInAnyAnte = true;
                            break;
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.SpectralCard)
                    {
                        // Check spectral in packs (shop already checked by vector)
                        if (CheckSpectralInPacks(ref singleCtx, need, ante))
                        {
                            foundInAnyAnte = true;
                            break;
                        }
                    }
                }
                
                if (!foundInAnyAnte)
                    return false; // Need not satisfied
            }
            
            return true;
        }

        private static bool CheckShopJoker(ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire need, int ante)
        {
            if (!need.JokerEnum.HasValue)
            {
                DebugLogger.LogFormat("CheckShopJoker: No JokerEnum for need {0}", need.Value);
                return false;
            }
            
            var targetJoker = need.JokerEnum.Value;
            var shop = singleCtx.GenerateFullShop(ante);
            
            for (int i = 0; i < ShopState.ShopSlots; i++)
            {
                ref var item = ref shop.Items[i];
                if (item.Type == ShopState.ShopItem.ShopItemType.Joker && item.Joker == targetJoker)
                {
                    // Check edition if needed
                    if (need.ParsedEdition.HasValue)
                    {
                        return item.Edition == need.ParsedEdition.Value;
                    }
                    return true;
                }
            }
            return false;
        }

        private static bool CheckTarotInPacks(ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire need, int ante)
        {
            if (!need.TarotEnum.HasValue)
                return false;
                
            var targetTarot = (MotelyItemType)MotelyItemTypeCategory.TarotCard | (MotelyItemType)need.TarotEnum.Value;
            int packsToCheck = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
            
            bool tarotStreamInit = false;
            MotelySingleTarotStream tarotStream = default;
            
            for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream = singleCtx.CreateArcanaPackTarotStream(ante);
                    }
                    
                    var packContents = singleCtx.GetArcanaPackContents(ref tarotStream, pack.GetPackSize());
                    if (packContents.Contains(targetTarot))
                        return true;
                }
            }
            return false;
        }

        private static bool CheckPlanetInPacks(ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire need, int ante)
        {
            if (!need.PlanetEnum.HasValue)
                return false;
                
            var targetPlanet = (MotelyItemType)MotelyItemTypeCategory.PlanetCard | (MotelyItemType)need.PlanetEnum.Value;
            int packsToCheck = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
            
            bool planetStreamInit = false;
            MotelySinglePlanetStream planetStream = default;
            
            for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                {
                    if (!planetStreamInit)
                    {
                        planetStreamInit = true;
                        planetStream = singleCtx.CreateCelestialPackPlanetStreamCached(ante);
                    }
                    
                    var packContents = singleCtx.GetCelestialPackContents(ref planetStream, pack.GetPackSize());
                    if (packContents.Contains(targetPlanet))
                        return true;
                }
            }
            return false;
        }

        private static bool CheckSpectralInPacks(ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire need, int ante)
        {
            if (!need.SpectralEnum.HasValue)
                return false;
                
            var targetSpectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)need.SpectralEnum.Value;
            int packsToCheck = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
            
            bool spectralStreamInit = false;
            MotelySingleSpectralStream spectralStream = default;
            
            for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    if (!spectralStreamInit)
                    {
                        spectralStreamInit = true;
                        spectralStream = singleCtx.CreateSpectralPackStream(ante);
                    }
                    
                    var packContents = singleCtx.GetSpectralPackContents(ref spectralStream, pack.GetPackSize());
                    if (packContents.Contains(targetSpectral))
                        return true;
                }
            }
            return false;
        }

        public static ConcurrentQueue<OuijaResult>? ResultsQueue { get; set; } = new ConcurrentQueue<OuijaResult>();

        private static OuijaResult ProcessWantsAndScore(ref MotelySingleSearchContext singleCtx, OuijaConfig config)
        {
            string seed = singleCtx.GetCurrentSeed();
            var result = new OuijaResult
            {
                Seed = seed,
                TotalScore = 1,
                NaturalNegativeJokers = 0,
                DesiredNegativeJokers = 0,
                ScoreWants = new int[32],
                Success = true
            };

            if (config?.Wants == null || config.Wants.Length == 0)
                return result;

            // Process each want individually
            for (int wantIndex = 0; wantIndex < config.Wants.Length && wantIndex < 32; wantIndex++)
            {
                var want = config.Wants[wantIndex];
                if (want == null) continue;
                
                var searchAntes = want.SearchAntes?.Length > 0 ? want.SearchAntes : new[] { want.DesireByAnte };
                
                foreach (var ante in searchAntes)
                {
                    bool foundInThisAnte = false;
                    
                    // Route to appropriate handler based on type
                    var typeCat = want.TypeCategory;
                    
                    if (string.Equals(want.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
                    {
                        if (want.JokerEnum.HasValue)
                        {
                            foundInThisAnte = ProcessLegendaryJokerFromSoul(ref singleCtx, 
                                want.JokerEnum.Value, ante, want.ParsedEdition);
                        }
                            
                        if (foundInThisAnte)
                        {
                            result.ScoreWants[wantIndex] += want.Score;
                            if (want.ParsedEdition == MotelyItemEdition.Negative)
                                result.DesiredNegativeJokers++;
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.Joker)
                    {
                        if (!want.JokerEnum.HasValue)
                        {
                            DebugLogger.LogFormat("ProcessWantsAndScore: No JokerEnum for joker want {0}", want.Value);
                            continue;
                        }
                        
                        // Handle shop jokers with proper edition checking
                        var shop = singleCtx.GenerateFullShop(ante);
                        var targetJoker = want.JokerEnum.Value;
                        
                        for (int i = 0; i < ShopState.ShopSlots; i++)
                        {
                            ref var item = ref shop.Items[i];
                            if (item.Type == ShopState.ShopItem.ShopItemType.Joker && item.Joker == targetJoker)
                            {
                                // Check edition
                                bool editionMatches = !want.ParsedEdition.HasValue || item.Edition == want.ParsedEdition.Value;
                                
                                if (editionMatches)
                                {
                                    DebugLogger.LogFormat("[ProcessWantsAndScore] Shop Joker MATCH: {0} with edition {1} (required: {2}) at ante {3}", 
                                        item.Joker, item.Edition, want.ParsedEdition?.ToString() ?? "None", ante);
                                    
                                    foundInThisAnte = true;
                                    result.ScoreWants[wantIndex] += want.Score;
                                    
                                    if (item.Edition == MotelyItemEdition.Negative && want.ParsedEdition == MotelyItemEdition.Negative)
                                    {
                                        result.DesiredNegativeJokers++;
                                    }
                                    break; // Found the joker, stop searching
                                }
                            }
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.TarotCard)
                    {
                        foundInThisAnte = CheckTarotInShopOrPacks(ref singleCtx, want, ante);
                        if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                    }
                    else if (typeCat == MotelyItemTypeCategory.PlanetCard)
                    {
                        foundInThisAnte = CheckPlanetInShopOrPacks(ref singleCtx, want, ante);
                        if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                    }
                    else if (typeCat == MotelyItemTypeCategory.SpectralCard)
                    {
                        foundInThisAnte = CheckSpectralInShopOrPacks(ref singleCtx, want, ante);
                        if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                    }
                    else if (string.Equals(want.Type, "Tag", StringComparison.OrdinalIgnoreCase))
                    {
                        foundInThisAnte = CheckTagForAnte(ref singleCtx, want, ante);
                        if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                    }
                    else if (string.Equals(want.Type, "Voucher", StringComparison.OrdinalIgnoreCase))
                    {
                        foundInThisAnte = CheckVoucherForAnte(ref singleCtx, want, ante);
                        if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                    }
                }
            }
            
            // Calculate total score
            for (int i = 0; i < 32; i++)
            {
                result.TotalScore += result.ScoreWants[i];
            }
            
            return result;
        }

        // Helper methods for checking in shop or packs
        private static bool CheckPlanetInShopOrPacks(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            if (!want.PlanetEnum.HasValue)
                return false;
                
            var targetPlanet = (MotelyItemType)MotelyItemTypeCategory.PlanetCard | (MotelyItemType)want.PlanetEnum.Value;
            
            // Check shop first
            var planetStream = singleCtx.CreateShopPlanetStream(ante);
            var planet = singleCtx.GetNextShopPlanet(ref planetStream);
            if (planet.Type == targetPlanet)
                return true;
                
            // Check packs
            return CheckPlanetInPacks(ref singleCtx, want, ante);
        }

        private static bool CheckTarotInShopOrPacks(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            // Tarot cards don't appear in shops, only in Arcana packs
            return CheckTarotInPacks(ref singleCtx, want, ante);
        }

        private static bool CheckSpectralInShopOrPacks(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            if (!want.SpectralEnum.HasValue)
                return false;
                
            var targetSpectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)want.SpectralEnum.Value;
            
            // Check shop first
            var spectralStream = singleCtx.CreateShopSpectralStream(ante);
            var spectral = singleCtx.GetNextShopSpectral(ref spectralStream);
            if (spectral.Type == targetSpectral)
                return true;
                
            // Check packs
            return CheckSpectralInPacks(ref singleCtx, want, ante);
        }

        private static bool CheckTagForAnte(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            if (!want.TagEnum.HasValue)
                return false;
                
            var targetTag = want.TagEnum.Value;
            var tagStream = singleCtx.CreateTagStream(ante);
            
            // Check if EITHER blind has the tag
            var smallBlindTag = singleCtx.GetNextTag(ref tagStream);
            var bigBlindTag = singleCtx.GetNextTag(ref tagStream);
            
            return smallBlindTag == targetTag || bigBlindTag == targetTag;
        }

        private static bool CheckVoucherForAnte(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            if (!want.VoucherEnum.HasValue)
                return false;
                
            var targetVoucher = want.VoucherEnum.Value;
            var voucher = singleCtx.GetAnteFirstVoucher(ante);
            return voucher == targetVoucher;
        }

        static void PrintResult(OuijaResult result, OuijaConfig config, int cutoff = 0)
        {
            // Check if the result meets the cutoff score
            if (result.TotalScore < cutoff) 
            {
                DebugLogger.LogFormat("[PrintResult] Skipping seed {0} - score {1} < cutoff {2}", result.Seed, result.TotalScore, cutoff);
                return;
            }
            if (config == null)
            {
                Console.WriteLine($"[noconfig]|{result.Seed},{result.TotalScore}");
                return;
            }

            // DEBUG: Print every result, even if not successful, for troubleshooting
            DebugLogger.LogFormat("[RESULT DEBUG] Seed: {0}, TotalScore: {1}, Success: {2}", result.Seed, result.TotalScore, result.Success);

            var row = $"|{result.Seed},{result.TotalScore}";

            if (config.ScoreNaturalNegatives)
                row += $",{result.NaturalNegativeJokers}";
            if (config.ScoreDesiredNegatives)
                row += $",{result.DesiredNegativeJokers}";

            // Add scores for each want
            if (config.Wants != null && result.ScoreWants != null)
            {
                for (int i = 0; i < Math.Min(result.ScoreWants.Length, config.Wants.Length); i++)
                {
                    row += $",{result.ScoreWants[i]}";
                }
            }

            Console.WriteLine(row);
        }

        private static int GetMinimumScore(OuijaConfig? config)
        {
            return 1; // Require at least score 1 to filter out zero scores
        }

        private static MotelyJokerRarity GetJokerRarity(MotelyJoker joker)
        {
            return (MotelyJokerRarity)((int)joker & (0b11 << Motely.JokerRarityOffset));
        }

        // Check for legendary/soul jokers across all packs
        private static bool PerkeoStyleSoulJokerCheck(ref MotelySingleSearchContext singleCtx, MotelyJoker targetJoker, MotelyItemEdition? requiredEdition = null, int ante = 0)
        {
            // Convert MotelyJoker to the corresponding MotelyItemType for proper comparison
            int legendaryJokerBase = (int)targetJoker & ~(Motely.JokerRarityMask << Motely.JokerRarityOffset);
            MotelyItemType targetItemType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | legendaryJokerBase);
            
            // Check first booster pack for Arcana/Soul/targetJoker
            MotelySingleBoosterPackStream boosterPackStream = singleCtx.CreateBoosterPackStream(ante, true);
            MotelyBoosterPack pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
            if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
            {
                var tarotStream = singleCtx.CreateArcanaPackTarotStream(ante);
                if (singleCtx.GetArcanaPackContents(ref tarotStream, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                {
                    var soulJokerStream = singleCtx.CreateSoulJokerStream(ante);
                    var joker = singleCtx.NextJoker(ref soulJokerStream);
                    
                    if (joker.Type == targetItemType)
                    {
                        // Check edition if required
                        if (requiredEdition.HasValue)
                        {
                            if (joker.Edition != requiredEdition.Value) return false;
                        }
                        DebugLogger.LogFormat("[DEBUG] PerkeoStyleSoulJokerCheck: MATCH! Seed: {0}", singleCtx.GetCurrentSeed());
                        return true;
                    }
                }
            }

            // Check all remaining packs
            {
                int totalPacks = ante == 1 ? 4 : 6; // ante 1: 2 shops * 2 packs, ante 2+: 3 shops * 2 packs
                boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
                
                bool tarotStreamInit = false;
                MotelySingleTarotStream tarotStream2 = default;
                
                for (int i = 0; i < totalPacks; i++)
                {
                    pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        if (!tarotStreamInit)
                        {
                            tarotStreamInit = true;
                            tarotStream2 = singleCtx.CreateArcanaPackTarotStream(ante);
                        }
                        if (singleCtx.GetArcanaPackContents(ref tarotStream2, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                        {
                            var soulJokerStream = singleCtx.CreateSoulJokerStream(ante);
                            var joker = singleCtx.NextJoker(ref soulJokerStream);
                            if (joker.Type == targetItemType)
                            {
                                // Check edition if required
                                if (requiredEdition.HasValue)
                                {
                                    if (joker.Edition != requiredEdition.Value) continue;
                                }
                                DebugLogger.LogFormat("[DEBUG] PerkeoStyleSoulJokerCheck: MATCH! Seed: {0}", singleCtx.GetCurrentSeed());
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
    }

    public static OuijaJsonFilterDesc LoadFromFile(string configFileName)
    {
        var config = OuijaConfigLoader.Load(configFileName);
        return new OuijaJsonFilterDesc(config);
    }
}
