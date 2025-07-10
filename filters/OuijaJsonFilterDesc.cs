using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;
using System.Collections.Generic;
using Motely;
using Motely.Filters;

namespace Motely;

// Source constants matching Immolate's source_str() function
internal static class OuijaSourceConstants
{
    public const string SHOP = "sho";
    public const string ARCANA = "ar1";
    public const string BUFFOON = "buf";
    public const string SOUL = "sou";
}

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

    private static bool TryParseTypeCategory(string value, out MotelyItemTypeCategory type)
    {
        type = default;
        if (string.IsNullOrEmpty(value))
            return false;
            
        if (MotelyEnumUtil.TryParseEnum<MotelyItemTypeCategory>(value, out type))
            return true;
        
        // Handle normalized type names
        var normalized = value.Replace("Card", "");
        if (MotelyEnumUtil.TryParseEnum<MotelyItemTypeCategory>(normalized + "Card", out type))
            return true;
            
        return false;
    }

    private static void CacheStreamForType(MotelyFilterCreationContext ctx, OuijaConfig.Desire desire, int ante)
    {
        // Use the cached TypeCategory if available
        var type = desire.Type;
        var typeCategory = desire.TypeCategory;
        
        if (string.IsNullOrEmpty(type) && !typeCategory.HasValue)
        {
            DebugLogger.LogFormat("[OuijaJsonFilterDesc] Empty type for ante {0}", ante);
            return;
        }

        // Use cached category or try to parse
        var parsedType = typeCategory ?? default;
        if (!typeCategory.HasValue && !TryParseTypeCategory(type, out parsedType))
        {
            // Handle special cases that don't map directly to MotelyItemTypeCategory
            if (string.Equals(type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
            {
                ctx.CachePseudoHash(MotelyPrngKeys.ShopPack + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.ArcanaPack + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerSoul + ante);
                ctx.CacheTarotStream(ante);
                if (ante >= 1) ctx.CacheVoucherStream(1);
                if (ante >= 2) ctx.CacheVoucherStream(2);
                return;
            }
            else if (string.Equals(type, "Tag", StringComparison.OrdinalIgnoreCase))
            {
                ctx.CacheTagStream(ante);
                return;
            }
            else if (string.Equals(type, "Voucher", StringComparison.OrdinalIgnoreCase))
            {
                ctx.CacheVoucherStream(ante);
                return;
            }
            DebugLogger.LogFormat("[OuijaJsonFilterDesc] Unknown type '{0}' (ante {1}) - no canonical PRNG key used", type, ante);
            return;
        }
        
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
            
            // Parse deck and stake from config
            _deck = MotelyDeck.RedDeck;
            _stake = MotelyStake.WhiteStake;
            
            if (!string.IsNullOrEmpty(config.Deck))
            {
                if (MotelyEnumUtil.TryParseEnum<MotelyDeck>(config.Deck, out var deck))
                    _deck = deck;
                else
                    DebugLogger.LogFormat("[WARNING] Unknown deck: {0}, using RedDeck", config.Deck);
            }
            
            if (!string.IsNullOrEmpty(config.Stake))
            {
                if (MotelyEnumUtil.TryParseEnum<MotelyStake>(config.Stake, out var stake))
                    _stake = stake;
                else
                    DebugLogger.LogFormat("[WARNING] Unknown stake: {0}, using WhiteStake", config.Stake);
            }
            
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
                int needsPassCount = CountBitsSet(mask);
                if (mask.IsAllFalse())
                {
                    return mask;
                }
                // Clear results queue before batch
                ResultsQueue = new ConcurrentQueue<OuijaResult>();
                int cutoff = _cutoff;
                mask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
                {
                    var result = ProcessWantsAndScore(ref singleCtx, localConfig);
                    if (result.Success && result.TotalScore >= cutoff)
                        ResultsQueue.Enqueue(result);
                    return result.Success && result.TotalScore >= cutoff;
                });
                // Print all results after batch
                if (ResultsQueue != null)
                {
                    foreach (var result in ResultsQueue)
                        PrintResult(result, localConfig, _cutoff);
                }
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

            //DebugLogger.LogFormat("Processing {0} needs", config.Needs.Length);

            // Process each need
            foreach (var need in config.Needs.Where(n => n != null))
            {
                var searchAntes = need.SearchAntes?.Length > 0 ? need.SearchAntes : new[] { need.DesireByAnte };
                // For each need, we want to find seeds that have it in ANY of the specified antes (OR logic)
                VectorMask needMask = new VectorMask(0); // AllBitsClear
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
            if (need == null || (string.IsNullOrEmpty(need.Type) && !need.TypeCategory.HasValue))
            {
                DebugLogger.Log("Invalid need: null or empty type");
                return mask;
            }

            // Use cached category if available
            var typeCat = need.TypeCategory ?? default;
            if (!need.TypeCategory.HasValue && !TryParseTypeCategory(need.Type, out typeCat))
            {
                // Handle special joker types
                if (string.Equals(need.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessSoulJokerNeed(ref searchContext, need, ante, mask);
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
            
            switch(typeCat)
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
            var targetJoker = ParseJoker(need);
            if (targetJoker == default)
            {
                DebugLogger.LogFormat("❌ Unknown joker: {0}", need.Value);
                return default; // Return empty mask instead of original mask
            }
            else 
            {
                DebugLogger.LogFormat("Processing Joker need: {0} (ante {1})", targetJoker, ante);
            }

            var jokerRarity = GetJokerRarity(targetJoker);
            
            // Legendary jokers like Perkeo come from Soul cards in Arcana packs, NOT shop streams!
            if (jokerRarity == MotelyJokerRarity.Legendary)
            {
                DebugLogger.LogFormat("🔍 Legendary joker {0} requires Soul detection - using individual seed processing", targetJoker);
                var legendaryMask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
                {
                    bool found = ProcessLegendaryJokerFromSoul(ref singleCtx, targetJoker, ante);
                    DebugLogger.LogFormat("[DEBUG] (Need) Legendary Joker: Checking for {0} in seed {1} => {2}", targetJoker, singleCtx.GetCurrentSeed(), found);
                    return found;
                });
                DebugLogger.LogFormat("Legendary joker search complete - {0} seeds found with {1}", CountBitsSet(legendaryMask), targetJoker);
                return legendaryMask;
            }

            // IMPORTANT: We need to check both shop slots AND Buffoon packs!
            // First check shop slots
            var shopJokerMask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // Check all 8 shop slots
                var cardTypePrng = singleCtx.CreatePrngStream(MotelyPrngKeys.CardType + ante);
                
                // We need separate PRNG streams for each rarity that could appear
                var rarityPrng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerRarity + ante + OuijaSourceConstants.SHOP);
                var commonPrng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerCommon + OuijaSourceConstants.SHOP + ante);
                var uncommonPrng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerUncommon + OuijaSourceConstants.SHOP + ante);
                var rarePrng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerRare + OuijaSourceConstants.SHOP + ante);
                
                for (int slot = 0; slot < 8; slot++)
                {
                    double totalRate = 28; // 20 joker + 4 tarot + 4 planet
                    double cardTypeRoll = singleCtx.GetNextRandom(ref cardTypePrng) * totalRate;
                    
                    // Is this slot a joker?
                    if (cardTypeRoll < 20)
                    {
                        // Yes! Now check rarity
                        double rarityRoll = singleCtx.GetNextRandom(ref rarityPrng);
                        
                        MotelyJokerRarity slotRarity;
                        if (rarityRoll > 0.95)
                            slotRarity = MotelyJokerRarity.Rare;
                        else if (rarityRoll > 0.7)
                            slotRarity = MotelyJokerRarity.Uncommon;
                        else
                            slotRarity = MotelyJokerRarity.Common;
                            
                        // Does this slot's rarity match our target?
                        if (slotRarity == jokerRarity)
                        {
                            // Yes! Now check the specific joker
                            MotelyJoker generatedJoker;
                            switch (jokerRarity)
                            {
                                case MotelyJokerRarity.Common:
                                    var commonChoice = singleCtx.GetNextRandomElement(ref commonPrng, MotelyEnum<MotelyJokerCommon>.Values);
                                    generatedJoker = (MotelyJoker)((int)MotelyJokerRarity.Common + (int)commonChoice);
                                    break;
                                    
                                case MotelyJokerRarity.Uncommon:
                                    var uncommonChoice = singleCtx.GetNextRandomElement(ref uncommonPrng, MotelyEnum<MotelyJokerUncommon>.Values);
                                    generatedJoker = (MotelyJoker)((int)MotelyJokerRarity.Uncommon + (int)uncommonChoice);
                                    break;
                                    
                                case MotelyJokerRarity.Rare:
                                    var rareChoice = singleCtx.GetNextRandomElement(ref rarePrng, MotelyEnum<MotelyJokerRare>.Values);
                                    generatedJoker = (MotelyJoker)((int)MotelyJokerRarity.Rare + (int)rareChoice);
                                    break;
                                    
                                default:
                                    continue;
                            }
                            
                            DebugLogger.LogFormat("[DEBUG] Shop slot {0}: Joker {1} (target: {2})", 
                                slot + 1, generatedJoker, targetJoker);
                                
                            if (generatedJoker == targetJoker)
                            {
                                // Found it! Check edition if needed
                                if (!string.IsNullOrEmpty(need.Edition) && need.Edition != "None")
                                {
                                    var editionPrng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerEdition + OuijaSourceConstants.SHOP + ante);
                                    
                                    // Advance edition PRNG to the correct slot (only advances for joker slots)
                                    // We need to count how many jokers we've seen so far
                                    var tempCardTypePrng = singleCtx.CreatePrngStream(MotelyPrngKeys.CardType + ante);
                                    int jokerCount = 0;
                                    for (int i = 0; i < slot; i++)
                                    {
                                        double tempRoll = singleCtx.GetNextRandom(ref tempCardTypePrng) * totalRate;
                                        if (tempRoll < 20) // Is joker
                                        {
                                            jokerCount++;
                                            singleCtx.GetNextRandom(ref editionPrng); // Advance edition PRNG
                                        }
                                    }
                                    
                                    double editionRoll = singleCtx.GetNextRandom(ref editionPrng);
                                    double threshold = GetEditionThreshold(need.Edition);
                                    DebugLogger.LogFormat("[DEBUG] Edition check: roll={0:F4}, threshold={1}, passes={2}", 
                                        editionRoll, threshold, editionRoll < threshold);
                                    return editionRoll < threshold;
                                }
                                return true;
                            }
                        }
                        else
                        {
                            // Wrong rarity, but we still need to advance the appropriate PRNG
                            switch (slotRarity)
                            {
                                case MotelyJokerRarity.Common:
                                    singleCtx.GetNextRandom(ref commonPrng);
                                    break;
                                case MotelyJokerRarity.Uncommon:
                                    singleCtx.GetNextRandom(ref uncommonPrng);
                                    break;
                                case MotelyJokerRarity.Rare:
                                    singleCtx.GetNextRandom(ref rarePrng);
                                    break;
                            }
                        }
                    }
                }
                return false;
            });
            
            DebugLogger.LogFormat("Shop joker search complete - {0} seeds found with {1} in shops", CountBitsSet(shopJokerMask), targetJoker);
            
            // Now check Buffoon packs!
            var buffoonPackMask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // Check ALL packs for Buffoon packs
                int packsToCheck = ante == 1 ? 4 : 6;
                var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
                
                // We need separate PRNG streams for each rarity in Buffoon packs
                var buffoonRarityPrng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerRarity + ante + OuijaSourceConstants.BUFFOON);
                var buffoonCommonPrng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerCommon + OuijaSourceConstants.BUFFOON + ante);
                var buffoonUncommonPrng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerUncommon + OuijaSourceConstants.BUFFOON + ante);
                var buffoonRarePrng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerRare + OuijaSourceConstants.BUFFOON + ante);
                
                for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
                {
                    var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                    
                    if (pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                    {
                        DebugLogger.LogFormat("[DEBUG] Found Buffoon pack at position {0} in ante {1}", packIndex + 1, ante);
                        
                        // Buffoon packs contain jokers - check each one
                        int packSize = pack.GetPackCardCount();
                        for (int jokerIndex = 0; jokerIndex < packSize; jokerIndex++)
                        {
                            // Get rarity for this joker
                            double rarityRoll = singleCtx.GetNextRandom(ref buffoonRarityPrng);
                            MotelyJokerRarity packRarity;
                            if (rarityRoll > 0.95)
                                packRarity = MotelyJokerRarity.Rare;
                            else if (rarityRoll > 0.7)
                                packRarity = MotelyJokerRarity.Uncommon;
                            else
                                packRarity = MotelyJokerRarity.Common;
                                
                            // Does this rarity match what we're looking for?
                            if (packRarity == jokerRarity)
                            {
                                // Yes! Check the specific joker
                                MotelyJoker generatedJoker;
                                switch (jokerRarity)
                                {
                                    case MotelyJokerRarity.Common:
                                        var commonChoice = singleCtx.GetNextRandomElement(ref buffoonCommonPrng, MotelyEnum<MotelyJokerCommon>.Values);
                                        generatedJoker = (MotelyJoker)((int)MotelyJokerRarity.Common + (int)commonChoice);
                                        break;
                                        
                                    case MotelyJokerRarity.Uncommon:
                                        var uncommonChoice = singleCtx.GetNextRandomElement(ref buffoonUncommonPrng, MotelyEnum<MotelyJokerUncommon>.Values);
                                        generatedJoker = (MotelyJoker)((int)MotelyJokerRarity.Uncommon + (int)uncommonChoice);
                                        break;
                                        
                                    case MotelyJokerRarity.Rare:
                                        var rareChoice = singleCtx.GetNextRandomElement(ref buffoonRarePrng, MotelyEnum<MotelyJokerRare>.Values);
                                        generatedJoker = (MotelyJoker)((int)MotelyJokerRarity.Rare + (int)rareChoice);
                                        break;
                                        
                                    default:
                                        continue;
                                }
                                
                                DebugLogger.LogFormat("[DEBUG] Buffoon pack joker {0}: {1} (target: {2})", 
                                    jokerIndex + 1, generatedJoker, targetJoker);
                                    
                                if (generatedJoker == targetJoker)
                                {
                                    // Found it! Check edition if needed
                                    if (!string.IsNullOrEmpty(need.Edition) && need.Edition != "None")
                                    {
                                        var editionPrng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerEdition + OuijaSourceConstants.BUFFOON + ante);
                                        
                                        // Advance edition PRNG to the correct position
                                        for (int i = 0; i < jokerIndex; i++)
                                        {
                                            singleCtx.GetNextRandom(ref editionPrng);
                                        }
                                        
                                        double editionRoll = singleCtx.GetNextRandom(ref editionPrng);
                                        double threshold = GetEditionThreshold(need.Edition);
                                        DebugLogger.LogFormat("[DEBUG] Buffoon edition check: roll={0:F4}, threshold={1}, passes={2}", 
                                            editionRoll, threshold, editionRoll < threshold);
                                        return editionRoll < threshold;
                                    }
                                    return true;
                                }
                            }
                            else
                            {
                                // Wrong rarity, advance the appropriate PRNG
                                switch (packRarity)
                                {
                                    case MotelyJokerRarity.Common:
                                        singleCtx.GetNextRandom(ref buffoonCommonPrng);
                                        break;
                                    case MotelyJokerRarity.Uncommon:
                                        singleCtx.GetNextRandom(ref buffoonUncommonPrng);
                                        break;
                                    case MotelyJokerRarity.Rare:
                                        singleCtx.GetNextRandom(ref buffoonRarePrng);
                                        break;
                                }
                            }
                        }
                    }
                }
                return false;
            });
            
            // Combine shop and Buffoon pack results
            var combinedMask = shopJokerMask | buffoonPackMask;
            DebugLogger.LogFormat("Joker search complete - {0} seeds found with {1} (shop: {2}, buffoon: {3})", 
                CountBitsSet(combinedMask), targetJoker, CountBitsSet(shopJokerMask), CountBitsSet(buffoonPackMask));
            return combinedMask;
        }

        // --- MISSING NEED HELPERS ---
        private static VectorMask ProcessSoulJokerNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.JokerEnum.HasValue)
            {
                DebugLogger.Log("Missing JokerEnum for SoulJoker need");
                return mask;
            }
            var targetJoker = need.JokerEnum.Value;
            string? requiredEdition = need.Edition;
            // Soul jokers require individual seed processing
            return searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                return ProcessLegendaryJokerFromSoul(ref singleCtx, targetJoker, ante, requiredEdition);
            });
        }

        private static VectorMask ProcessTarotNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.TarotEnum.HasValue)
            {
                DebugLogger.Log("Missing TarotEnum for Tarot need");
                return mask;
            }
            var targetTarot = (MotelyItemType)MotelyItemTypeCategory.TarotCard | (MotelyItemType)need.TarotEnum.Value;
            
            // Tarot cards only come from Arcana Packs, not shops
            // Must use individual seed search to check pack contents
            var arcanaMask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // Check ALL packs for Arcana
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
            });
            
            DebugLogger.LogFormat("[DEBUG] (Need) Tarot check: ante={0}, target={1}, found in {2} seeds", 
                ante, need.TarotEnum.Value, CountBitsSet(arcanaMask));
            return mask & arcanaMask;
        }

        private static VectorMask ProcessPlanetNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.PlanetEnum.HasValue)
            {
                DebugLogger.Log("Missing PlanetEnum for Planet need");
                return mask;
            }
            var targetPlanet = (MotelyItemType)MotelyItemTypeCategory.PlanetCard | (MotelyItemType)need.PlanetEnum.Value;
            
            DebugLogger.LogFormat("[DEBUG] Looking for planet {0} ({1}) in ante {2}", 
                need.PlanetEnum.Value, targetPlanet, ante);
            
            // Let's debug ALL shop items for this ante
            DebugLogger.LogFormat("[DEBUG] === DEBUGGING SHOP ITEMS FOR ANTE {0} ===", ante);
            var debugMask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // Shop has specific slots for different item types
                // Let's see what's generated
                DebugLogger.LogFormat("[DEBUG] Shop items for seed {0}:", singleCtx.GetCurrentSeed());
                
                // Generate all 8 shop slots using card type RNG
                var cardTypePrng = singleCtx.CreatePrngStream(MotelyPrngKeys.CardType + ante);
                
                for (int slot = 0; slot < 8; slot++)
                {
                    double totalRate = 28; // 20 joker + 4 tarot + 4 planet
                    double cardTypeRoll = singleCtx.GetNextRandom(ref cardTypePrng) * totalRate;
                    
                    string itemType;
                    if (cardTypeRoll < 20)
                        itemType = "JOKER";
                    else if (cardTypeRoll < 24)
                        itemType = "TAROT";
                    else
                        itemType = "PLANET";
                        
                    DebugLogger.LogFormat("[DEBUG]   Slot {0}: roll={1:F2}, type={2}", 
                        slot + 1, cardTypeRoll, itemType);
                }
                
                return false;
            });
            
            // Shop planet vectorized filter
            var shopPlanetMask = searchContext.FilterPlanetCard(ante, need.PlanetEnum.Value, MotelyPrngKeys.Shop);
            
            DebugLogger.LogFormat("[DEBUG] Shop planet mask: {0}", shopPlanetMask);
            // Celestial pack: must use individual seed search
            var celestialMask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // Ante 1: 2 shops × 2 packs = 4 packs total
                // Ante 2+: 3 shops × 2 packs = 6 packs total
                int packsToCheck = ante == 1 ? 4 : 6;
                
                DebugLogger.LogFormat("[DEBUG] Checking {0} packs for Celestial in ante {1}, seed {2}", 
                    packsToCheck, ante, singleCtx.GetCurrentSeed());
                
                // Start from the beginning, including the Buffoon pack
                var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
                
                // We need to track if we've initialized the planet stream
                bool planetStreamInit = false;
                MotelySinglePlanetStream planetStream = default;
                
                for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
                {
                    var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                    
                    // Calculate which shop this pack is from
                    int shopNumber = (packIndex / 2) + 1;
                    int packInShop = (packIndex % 2) + 1;
                    
                    DebugLogger.LogFormat("[DEBUG] Pack {0}/{1} (Shop {2}, Pack {3}): {4}", 
                        packIndex + 1, packsToCheck, shopNumber, packInShop, pack.GetPackType());
                    
                    if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                    {
                        if (!planetStreamInit)
                        {
                            planetStreamInit = true;
                            planetStream = singleCtx.CreateCelestialPackPlanetStreamCached(ante);
                        }
                        
                        var packContents = singleCtx.GetCelestialPackContents(ref planetStream, pack.GetPackSize());
                        
                        // Build a string of the pack contents
                        string contents = "";
                        for (int i = 0; i < packContents.Length; i++)
                        {
                            if (i > 0) contents += ", ";
                            contents += packContents.GetItem(i).ToString();
                        }
                        
                        DebugLogger.LogFormat("[DEBUG] Celestial pack contents: [{0}]", contents);
                        
                        if (packContents.Contains(targetPlanet))
                        {
                            DebugLogger.LogFormat("[DEBUG] FOUND {0} in Celestial Pack!", targetPlanet);
                            return true;
                        }
                    }
                }
                return false;
            });
            
            var combinedMask = shopPlanetMask | celestialMask;
            DebugLogger.LogFormat("[DEBUG] (Need) Planet: ante={0}, shopMask={1}, celestialMask={2}, combined={3}", 
                ante, CountBitsSet(shopPlanetMask), CountBitsSet(celestialMask), CountBitsSet(combinedMask));
            return mask & combinedMask;
        }

        private static VectorMask ProcessSpectralNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.SpectralEnum.HasValue)
            {
                DebugLogger.Log("Missing SpectralEnum for Spectral need");
                return mask;
            }
            var targetSpectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)need.SpectralEnum.Value;
            
            // Shop spectral vectorized filter
            var shopSpectralMask = searchContext.FilterSpectralCard(ante, need.SpectralEnum.Value, MotelyPrngKeys.Shop);
            
            // Spectral pack: must use individual seed search
            var spectralPackMask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // Check ALL packs for Spectral
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
            });
            
            var combinedMask = shopSpectralMask | spectralPackMask;
            DebugLogger.LogFormat("[DEBUG] (Need) Spectral: ante={0}, shopMask={1}, packMask={2}, combined={3}", 
                ante, CountBitsSet(shopSpectralMask), CountBitsSet(spectralPackMask), CountBitsSet(combinedMask));
            return mask & combinedMask;
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

        private static bool ProcessLegendaryJokerFromSoul(ref MotelySingleSearchContext singleCtx, MotelyJoker targetJoker, int ante, string? requiredEdition = null)
        {
            // This logic matches PerkeoStyleSoulJokerCheck for legendary jokers
            MotelySingleBoosterPackStream boosterPackStream = singleCtx.CreateBoosterPackStream(ante, true);
            MotelyBoosterPack pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
            if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
            {
                var tarotStream = singleCtx.CreateArcanaPackTarotStream(ante);
                if (singleCtx.GetArcanaPackContents(ref tarotStream, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                {
                    var soulJokerStream = singleCtx.CreateSoulJokerStream(ante);
                    var joker = singleCtx.NextJoker(ref soulJokerStream);
                    int legendaryJokerBase = (int)targetJoker & ~(Motely.JokerRarityMask << Motely.JokerRarityOffset);
                    int rarityBits = (int)targetJoker & (Motely.JokerRarityMask << Motely.JokerRarityOffset);
                    MotelyItemType targetItemType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | legendaryJokerBase | rarityBits);
                    bool typeMatch = joker.Type == targetItemType;
                    bool editionMatch = true;
                    if (!string.IsNullOrEmpty(requiredEdition) && requiredEdition != "None")
                    {
                        editionMatch = CheckJokerEdition(ref singleCtx, requiredEdition, ante);
                    }
                    DebugLogger.LogFormat("[DEBUG] SoulJoker: Found={0} ({1}), Target={2} ({3}), EditionOK={4}, Seed={5}", joker.Type, (int)joker.Type, targetItemType, (int)targetItemType, editionMatch, singleCtx.GetCurrentSeed());
                    if (typeMatch && editionMatch)
                        return true;
                }
            }
            if (ante == 2)
            {
                var voucher1 = singleCtx.GetAnteFirstVoucher(1);
                var voucher2 = singleCtx.GetAnteFirstVoucher(2);
                if (voucher1 == MotelyVoucher.Telescope && voucher2 == MotelyVoucher.Observatory)
                {
                    boosterPackStream = singleCtx.CreateBoosterPackStream(2);
                    bool tarotStreamInit = false;
                    MotelySingleTarotStream tarotStream2 = default;
                    for (int i = 0; i < 3; i++)
                    {
                        pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                        if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                        {
                            if (!tarotStreamInit)
                            {
                                tarotStreamInit = true;
                                tarotStream2 = singleCtx.CreateArcanaPackTarotStream(2);
                            }
                            if (singleCtx.GetArcanaPackContents(ref tarotStream2, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                            {
                                var soulJokerStream = singleCtx.CreateSoulJokerStream(2);
                                var joker = singleCtx.NextJoker(ref soulJokerStream);
                                int legendaryJokerBase = (int)targetJoker & ~(Motely.JokerRarityMask << Motely.JokerRarityOffset);
                                int rarityBits = (int)targetJoker & (Motely.JokerRarityMask << Motely.JokerRarityOffset);
                                MotelyItemType targetItemType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | legendaryJokerBase | rarityBits);
                                bool typeMatch = joker.Type == targetItemType;
                                bool editionMatch = true;
                                if (!string.IsNullOrEmpty(requiredEdition) && requiredEdition != "None")
                                {
                                    editionMatch = CheckJokerEdition(ref singleCtx, requiredEdition, ante);
                                }
                                DebugLogger.LogFormat("[DEBUG] SoulJoker: Found={0} ({1}), Target={2} ({3}), EditionOK={4}, Seed={5}", joker.Type, (int)joker.Type, targetItemType, (int)targetItemType, editionMatch, singleCtx.GetCurrentSeed());
                                if (typeMatch && editionMatch)
                                    return true;
                            }
                        }
                    }
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

            // Refactored: For each ante, process all wants in order, advancing PRNG only once per ante/type
            if (config?.Wants != null && config.Wants.Length > 0)
            {
                // Collect all antes used in wants
                var allAntes = new HashSet<int>();
                for (int wantIndex = 0; wantIndex < config.Wants.Length && wantIndex < 32; wantIndex++)
                {
                    var want = config.Wants[wantIndex];
                    if (want == null) continue;
                    var searchAntes = want.SearchAntes?.Length > 0 ? want.SearchAntes : new[] { want.DesireByAnte };
                    foreach (var ante in searchAntes)
                        allAntes.Add(ante);
                }
                // For each ante, process wants grouped by type (and for Jokers, by rarity)
                foreach (var ante in allAntes.OrderBy(x => x))
                {
                    // Group wants by type for this ante
                    var wantsForAnte = config.Wants
                        .Select((want, idx) => (want, idx))
                        .Where(x => x.want != null && (x.want.SearchAntes?.Length > 0 ? x.want.SearchAntes : [x.want.DesireByAnte]).Contains(ante))
                        .ToList();

                    // --- Joker wants ---
                    var jokerWants = wantsForAnte
                        .Where(x => (x.want.TypeCategory == MotelyItemTypeCategory.Joker) || 
                                   (x.want.TypeCategory == null && 
                                    x.want.Type != null && 
                                    x.want.Type.ToLower().Contains("joker") && 
                                    !x.want.Type.Equals("SoulJoker", StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                        
                    // For joker wants, we need to check the actual shop generation to get editions right
                    if (jokerWants.Any())
                    {
                        // Generate the shop to find jokers with correct editions
                        var shopJokers = singleCtx.GetShopJokersForAnte(ante);
                        
                        foreach (var shopJoker in shopJokers)
                        {
                            foreach (var tuple in jokerWants)
                            {
                                var want = tuple.want;
                                var wantIndex = tuple.idx;
                                var targetJoker = ParseJoker(want);
                                
                                if (shopJoker.Joker == targetJoker)
                                {
                                    // Joker type matches, now check edition
                                    if (!string.IsNullOrEmpty(want.Edition) && want.Edition != "None")
                                    {
                                        // Convert MotelyItemEdition to string for comparison
                                        string actualEdition = shopJoker.Edition switch
                                        {
                                            MotelyItemEdition.Negative => "Negative",
                                            MotelyItemEdition.Polychrome => "Polychrome",
                                            MotelyItemEdition.Holographic => "Holographic",
                                            MotelyItemEdition.Foil => "Foil",
                                            _ => "None"
                                        };
                                        
                                        DebugLogger.LogFormat("[DEBUG] (Want) Shop Joker: {0} with edition {1} (wanted: {2})", 
                                            shopJoker.Joker, actualEdition, want.Edition);
                                        
                                        if (want.Edition.Equals(actualEdition, StringComparison.OrdinalIgnoreCase))
                                        {
                                            result.ScoreWants[wantIndex] += want.Score;
                                            if (actualEdition == "Negative")
                                            {
                                                result.DesiredNegativeJokers++;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // No edition requirement
                                        result.ScoreWants[wantIndex] += want.Score;
                                    }
                                }
                            }
                        }
                        
                        // TODO: Also check Buffoon packs for joker wants with editions
                    }

                    // --- SoulJoker wants ---
                    var soulJokerWants = wantsForAnte
                        .Where(x => x.want.Type != null && x.want.Type.Equals("SoulJoker", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    foreach (var tuple in soulJokerWants)
                    {
                        var want = tuple.want;
                        var wantIndex = tuple.idx;
                        var (score, isNaturalNeg, isDesiredNeg) = ProcessWantForAnte(ref singleCtx, want, ante);
                        result.ScoreWants[wantIndex] += score;
                        if (isNaturalNeg) result.NaturalNegativeJokers++;
                        if (isDesiredNeg) result.DesiredNegativeJokers++;
                    }

                    // --- Planet wants ---
                    foreach (var tuple in wantsForAnte.Where(x => x.want.TypeCategory == MotelyItemTypeCategory.PlanetCard))
                    {
                        var want = tuple.want;
                        var wantIndex = tuple.idx;
                        var (score, isNaturalNeg, isDesiredNeg) = ProcessPlanetWant(ref singleCtx, want, ante);
                        result.ScoreWants[wantIndex] += score;
                        if (isNaturalNeg) result.NaturalNegativeJokers++;
                        if (isDesiredNeg) result.DesiredNegativeJokers++;
                    }

                    // --- Tarot wants ---
                    foreach (var tuple in wantsForAnte.Where(x => x.want.TypeCategory == MotelyItemTypeCategory.TarotCard))
                    {
                        var want = tuple.want;
                        var wantIndex = tuple.idx;
                        var (score, isNaturalNeg, isDesiredNeg) = ProcessTarotWant(ref singleCtx, want, ante);
                        result.ScoreWants[wantIndex] += score;
                        if (isNaturalNeg) result.NaturalNegativeJokers++;
                        if (isDesiredNeg) result.DesiredNegativeJokers++;
                    }
                    
                    // --- Spectral wants ---
                    foreach (var tuple in wantsForAnte.Where(x => x.want.TypeCategory == MotelyItemTypeCategory.SpectralCard))
                    {
                        var want = tuple.want;
                        var wantIndex = tuple.idx;
                        var (score, isNaturalNeg, isDesiredNeg) = ProcessSpectralWant(ref singleCtx, want, ante);
                        result.ScoreWants[wantIndex] += score;
                        if (isNaturalNeg) result.NaturalNegativeJokers++;
                        if (isDesiredNeg) result.DesiredNegativeJokers++;
                    }

                    // --- Tag wants ---
                    foreach (var tuple in wantsForAnte.Where(x => (x.want.Type != null && x.want.Type.ToLower() == "tag")))
                    {
                        var want = tuple.want;
                        var wantIndex = tuple.idx;
                        var (score, isNaturalNeg, isDesiredNeg) = ProcessTagWant(ref singleCtx, want, ante);
                        result.ScoreWants[wantIndex] += score;
                        if (isNaturalNeg) result.NaturalNegativeJokers++;
                        if (isDesiredNeg) result.DesiredNegativeJokers++;
                    }

                    // --- Voucher wants ---
                    foreach (var tuple in wantsForAnte.Where(x => (x.want.Type != null && x.want.Type.ToLower() == "voucher")))
                    {
                        var want = tuple.want;
                        var wantIndex = tuple.idx;
                        var (score, isNaturalNeg, isDesiredNeg) = ProcessVoucherWant(ref singleCtx, want, ante);
                        result.ScoreWants[wantIndex] += score;
                        if (isNaturalNeg) result.NaturalNegativeJokers++;
                        if (isDesiredNeg) result.DesiredNegativeJokers++;
                    }
                }
                for (int i = 0; i < 32; i++)
                {
                    result.TotalScore += result.ScoreWants[i];
                }
            }
            return result;
        }

        static void PrintResult(OuijaResult result, OuijaConfig config, int cutoff = 0)
        {
            // Check if the result meets the cutoff scor
            // bool is_cool = false;
            // if (result.Seed.ToString().Contains("TACO"))
            // {
            //    is_cool = true;
            // }
            // if (!is_cool) return;

            if (result.TotalScore < cutoff) return;
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

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessWantForAnte(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            if (want == null || (string.IsNullOrEmpty(want.Type) && !want.TypeCategory.HasValue))
            {
                DebugLogger.Log("Invalid want: null or empty type");
                return (0, false, false);
            }

            // Use cached category if available
            var typeCat = want.TypeCategory ?? default;
            if (!want.TypeCategory.HasValue && !TryParseTypeCategory(want.Type, out typeCat))
            {
                // Handle special cases
                if (string.Equals(want.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessSoulJokerWant(ref singleCtx, want, ante);
                }
                if (string.Equals(want.Type, "Tag", StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessTagWant(ref singleCtx, want, ante);
                }
                if (string.Equals(want.Type, "Voucher", StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessVoucherWant(ref singleCtx, want, ante);
                }
                DebugLogger.LogFormat("❌ Unknown want type: {0}", want.Type);
                return (0, false, false);
            }
            
            switch (typeCat)
            {
                case MotelyItemTypeCategory.Joker:
                    return ProcessJokerWant(ref singleCtx, want, ante);

                case MotelyItemTypeCategory.PlayingCard:
                    return ProcessCardWant(ref singleCtx, want, ante);
                    
                case MotelyItemTypeCategory.TarotCard:
                    return ProcessTarotWant(ref singleCtx, want, ante);

                case MotelyItemTypeCategory.PlanetCard:
                    return ProcessPlanetWant(ref singleCtx, want, ante);

                case MotelyItemTypeCategory.SpectralCard:
                    return ProcessSpectralWant(ref singleCtx, want, ante);

                default:
                    DebugLogger.LogFormat("❌ Unhandled want type: {0}", want.Type);
                    return (0, false, false);
            }
        }

        // This method is no longer used for wants - we process joker wants directly in ProcessWantsAndScore
        // to properly handle editions based on which shop slot contains the joker
        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessJokerWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            // This shouldn't be called anymore
            DebugLogger.LogFormat("[WARNING] ProcessJokerWant called but should use shop generation instead");
            return (0, false, false);
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessSoulJokerWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            if (!want.JokerEnum.HasValue)
                return (0, false, false);

            var targetJoker = ParseJoker(want);
            if (targetJoker == default)
                return (0, false, false);

            bool found = PerkeoStyleSoulJokerCheck(ref singleCtx, targetJoker);
            return found ? (want.Score, false, false) : (0, false, false);
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessCardWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            // TODO: Implement playing card processing when streams are available
            DebugLogger.LogFormat("Playing card wants not yet implemented");
            return (0, false, false);
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessTagWant(ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            if (!want.TagEnum.HasValue)
            {
                DebugLogger.Log("Missing TagEnum for Tag want");
                return (0, false, false);
            }
            var targetTag = want.TagEnum.Value;
            MotelySingleTagStream tagStream = singleCtx.CreateTagStream(ante);
            
            // Check if EITHER blind has the tag
            var smallBlindTag = singleCtx.GetNextTag(ref tagStream);
            var bigBlindTag = singleCtx.GetNextTag(ref tagStream);
            
            if (smallBlindTag == targetTag || bigBlindTag == targetTag)
                return (want.Score, false, false);
                
            return (0, false, false);
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessVoucherWant(ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            if (!want.VoucherEnum.HasValue)
            {
                DebugLogger.Log("Missing VoucherEnum for Voucher want");
                return (0, false, false);
            }
            var targetVoucher = want.VoucherEnum.Value;
            var voucher = singleCtx.GetAnteFirstVoucher(ante);
            return voucher == targetVoucher ? (want.Score, false, false) : (0, false, false);
        }

        private static bool CheckJokerEdition(ref MotelySingleSearchContext singleCtx, string edition, int ante)
        {
            // Include source!
            var editionPrng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerEdition + OuijaSourceConstants.SHOP + ante);
            var roll = singleCtx.GetNextRandom(ref editionPrng);
            return roll < GetEditionThreshold(edition);
        }

        private static double GetEditionThreshold(string edition)
        {
            return edition?.ToLowerInvariant() switch
            {
                "negative" => 0.1,     // 10% chance
                "foil" => 0.05,        // 5% chance
                "holographic" => 0.02, // 2% chance
                "polychrome" => 0.01,  // 1% chance
                _ => 0.0
            };
        }

        private static int GetMinimumScore(OuijaConfig? config)
        {
            return 1; // Require at least score 1 to filter out zero scores
        }

        // Helper methods for joker processing
        private static MotelyJoker ParseJoker(OuijaConfig.Desire desire)
        {
            // Use cached enum for performance
            return desire.JokerEnum ?? default;
        }

        private static MotelyJokerRarity GetJokerRarity(MotelyJoker joker)
        {
            return (MotelyJokerRarity)((int)joker & (0b11 << Motely.JokerRarityOffset));
        }

        private static VectorMask ProcessJokerRarityVector<T>(ref MotelyVectorSearchContext searchContext, ref MotelyVectorPrngStream prng, MotelyJoker targetJoker) 
            where T : unmanaged, Enum
        {
            var choices = MotelyEnum<T>.Values;
            var jokerVec = searchContext.GetNextRandomElement(ref prng, choices);
            var targetInRarity = GetJokerInRarityEnum<T>(targetJoker);
            return VectorEnum256.Equals(jokerVec, targetInRarity);
        }

        private static bool ProcessJokerRaritySingle<T>(ref MotelySingleSearchContext singleCtx, ref MotelySinglePrngStream prng, MotelyJoker targetJoker) 
            where T : unmanaged, Enum
        {
            var choices = MotelyEnum<T>.Values;
            var joker = singleCtx.GetNextRandomElement(ref prng, choices);
            var targetInRarity = GetJokerInRarityEnum<T>(targetJoker);
            return joker.Equals(targetInRarity);
        }

        private static T GetJokerInRarityEnum<T>(MotelyJoker joker) where T : unmanaged, Enum
        {
            // Extract the base joker value (without rarity bits)
            int baseValue = (int)joker & ~(0b11 << Motely.JokerRarityOffset);
            return (T)(object)baseValue;
        }

        // Matches PerkeoObservatoryFilterDesc logic for legendary/soul jokers
        private static bool PerkeoStyleSoulJokerCheck(ref MotelySingleSearchContext singleCtx, MotelyJoker targetJoker)
        {
            // Check vouchers first for Telescope/Observatory strategy
            var voucher1 = singleCtx.GetAnteFirstVoucher(1);
            var voucher2 = singleCtx.GetAnteFirstVoucher(2);
            
            bool hasTelescope = voucher1 == MotelyVoucher.Telescope;
            bool hasObservatory = voucher2 == MotelyVoucher.Observatory;
            
            // Convert MotelyJoker to the corresponding MotelyItemType for proper comparison
            int legendaryJokerBase = (int)targetJoker & ~(Motely.JokerRarityMask << Motely.JokerRarityOffset);
            MotelyItemType targetItemType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | legendaryJokerBase);
            
            // ANTE 1: Check first booster pack for Arcana/Soul/targetJoker
            MotelySingleBoosterPackStream boosterPackStream = singleCtx.CreateBoosterPackStream(1, true);
            MotelyBoosterPack pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
            if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
            {
                var tarotStream = singleCtx.CreateArcanaPackTarotStream(1);
                if (singleCtx.GetArcanaPackContents(ref tarotStream, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                {
                    var soulJokerStream = singleCtx.CreateSoulJokerStream(1);
                    var joker = singleCtx.NextJoker(ref soulJokerStream);
                    
                    DebugLogger.LogFormat("[DEBUG] Joker comparison - Found: {0} ({1}), Target: {2} ({3}), Seed: {4}", 
                        joker.Type, (int)joker.Type, targetItemType, (int)targetItemType, singleCtx.GetCurrentSeed());
                    
                    if (joker.Type == targetItemType)
                    {
                        DebugLogger.LogFormat("[DEBUG] PerkeoStyleSoulJokerCheck: MATCH! Seed: {0}", singleCtx.GetCurrentSeed());
                        return true;
                    }
                }
            }

            // ANTE 2: If we have Telescope, check up to 3 booster packs
            if (hasTelescope && hasObservatory)
            {
                boosterPackStream = singleCtx.CreateBoosterPackStream(2);
                bool tarotStreamInit = false;
                MotelySingleTarotStream tarotStream2 = default;
                for (int i = 0; i < 3; i++)
                {
                    pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        if (!tarotStreamInit)
                        {
                            tarotStreamInit = true;
                            tarotStream2 = singleCtx.CreateArcanaPackTarotStream(2);
                        }
                        if (singleCtx.GetArcanaPackContents(ref tarotStream2, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                        {
                            var soulJokerStream = singleCtx.CreateSoulJokerStream(2);
                            var joker = singleCtx.NextJoker(ref soulJokerStream);
                            if (joker.Type == targetItemType)
                            {
                                DebugLogger.LogFormat("[DEBUG] PerkeoStyleSoulJokerCheck: MATCH! Seed: {0}", singleCtx.GetCurrentSeed());
                                return true;
                            }
                        }
                    }
                }
            }
            else
            {
                // Without Telescope, only check first pack in ante 2
                boosterPackStream = singleCtx.CreateBoosterPackStream(2, true);
                pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    var tarotStream = singleCtx.CreateArcanaPackTarotStream(2);
                    if (singleCtx.GetArcanaPackContents(ref tarotStream, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                    {
                        var soulJokerStream = singleCtx.CreateSoulJokerStream(2);
                        var joker = singleCtx.NextJoker(ref soulJokerStream);
                        if (joker.Type == (MotelyItemType)(int)targetJoker)
                        {
                            DebugLogger.LogFormat("[DEBUG] PerkeoStyleSoulJokerCheck: MATCH! Seed: {0}", singleCtx.GetCurrentSeed());
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // Helper methods for Planet and Spectral card checking
        private static bool CheckForBlackHole(ref MotelySingleSearchContext singleCtx, int ante)
        {
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, true);
            var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
            if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
            {
                var planetStream = singleCtx.CreateCelestialPackPlanetStream(ante);
                var contents = singleCtx.GetCelestialPackContents(ref planetStream, pack.GetPackSize());
                return contents.Contains(MotelyItemType.BlackHole);
            }
            return false;
        }

        private static bool CheckForSpectralSpecial(ref MotelySingleSearchContext singleCtx, string specialType, int ante)
        {
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, true);
            var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
            if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
            {
                var spectralStream = singleCtx.CreateSpectralPackStream(ante);
                var contents = singleCtx.GetSpectralPackContents(ref spectralStream, pack.GetPackSize());
                if (string.Equals(specialType, "Soul", StringComparison.OrdinalIgnoreCase))
                    return contents.Contains(MotelyItemType.Soul);
                else if (string.Equals(specialType, "BlackHole", StringComparison.OrdinalIgnoreCase))
                    return contents.Contains(MotelyItemType.BlackHole);
            }
            return false;
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessPlanetWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            if (!want.PlanetEnum.HasValue)
                return (0, false, false);
                
            var targetPlanet = (MotelyItemType)MotelyItemTypeCategory.PlanetCard | (MotelyItemType)want.PlanetEnum.Value;

            // Check shop planet
            var planetStream = singleCtx.CreateShopPlanetStream(ante);
            var planet = singleCtx.GetNextShopPlanet(ref planetStream);
            DebugLogger.LogFormat("[DEBUG] (Want) Planet PRNG: ante={0}, generated={1}, target={2}, seed={3}", 
                ante, planet.Type, targetPlanet, singleCtx.GetCurrentSeed());
                
            if (planet.Type == targetPlanet)
                return (want.Score, false, false);
                
            // Also check ALL booster packs for Celestial Pack
            // Ante 1: 2 shops × 2 packs = 4 packs total
            // Ante 2+: 3 shops × 2 packs = 6 packs total
            int packsToCheck = ante == 1 ? 4 : 6;
            
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
            
            // Track if we've initialized the planet stream
            bool planetStreamInit = false;
            MotelySinglePlanetStream celestialPlanetStream = default;
            
            for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                {
                    if (!planetStreamInit)
                    {
                        planetStreamInit = true;
                        celestialPlanetStream = singleCtx.CreateCelestialPackPlanetStream(ante);
                    }
                    
                    var packContents = singleCtx.GetCelestialPackContents(ref celestialPlanetStream, pack.GetPackSize());
                    if (packContents.Contains(targetPlanet))
                    {
                        DebugLogger.LogFormat("[DEBUG] (Want) Planet found in celestial pack: {0} in ante {1}, seed={2}", 
                            want.PlanetEnum.Value, ante, singleCtx.GetCurrentSeed());
                        return (want.Score, false, false);
                    }
                }
            }
                
            return (0, false, false);
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessTarotWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            if (!want.TarotEnum.HasValue)
                return (0, false, false);
                
            var targetTarot = (MotelyItemType)MotelyItemTypeCategory.TarotCard | (MotelyItemType)want.TarotEnum.Value;
            
            // Check ALL booster packs for Arcana containing the tarot
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
                    {
                        DebugLogger.LogFormat("[DEBUG] (Want) Tarot found: {0} in ante {1}, seed={2}", 
                            want.TarotEnum.Value, ante, singleCtx.GetCurrentSeed());
                        return (want.Score, false, false);
                    }
                }
            }
            return (0, false, false);
        }
        
        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessSpectralWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            if (!want.SpectralEnum.HasValue)
                return (0, false, false);
                
            var targetSpectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)want.SpectralEnum.Value;
            
            // Check shop spectral
            var spectralStream = singleCtx.CreateShopSpectralStream(ante);
            var spectral = singleCtx.GetNextShopSpectral(ref spectralStream);
            DebugLogger.LogFormat("[DEBUG] (Want) Spectral PRNG: ante={0}, generated={1}, target={2}, seed={3}", 
                ante, spectral.Type, targetSpectral, singleCtx.GetCurrentSeed());
                
            if (spectral.Type == targetSpectral)
                return (want.Score, false, false);
                
            // Also check ALL booster packs for Spectral
            int packsToCheck = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
            
            bool spectralStreamInit = false;
            MotelySingleSpectralStream spectralPackStream = default;
            
            for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    if (!spectralStreamInit)
                    {
                        spectralStreamInit = true;
                        spectralPackStream = singleCtx.CreateSpectralPackStream(ante);
                    }
                    
                    var packContents = singleCtx.GetSpectralPackContents(ref spectralPackStream, pack.GetPackSize());
                    if (packContents.Contains(targetSpectral))
                    {
                        DebugLogger.LogFormat("[DEBUG] (Want) Spectral found in pack: {0} in ante {1}, seed={2}", 
                            want.SpectralEnum.Value, ante, singleCtx.GetCurrentSeed());
                        return (want.Score, false, false);
                    }
                }
            }
            
            return (0, false, false);
        }
    }

    public static OuijaJsonFilterDesc LoadFromFile(string configFileName)
    {
        var config = OuijaConfigLoader.Load(configFileName);
        return new OuijaJsonFilterDesc(config);
    }
}
