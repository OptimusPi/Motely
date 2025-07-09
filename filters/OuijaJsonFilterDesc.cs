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

        return new OuijaJsonFilter(Config);
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
        
        public OuijaJsonFilter(OuijaConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
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
                mask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
                {
                    var result = ProcessWantsAndScore(ref singleCtx, localConfig);
                    if (result.Success)
                        ResultsQueue.Enqueue(result);
                    return result.Success;
                });
                // Print all results after batch
                if (ResultsQueue != null)
                {
                    foreach (var result in ResultsQueue)
                        PrintResult(result, localConfig);
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

            string prngKey = jokerRarity switch
            {
                MotelyJokerRarity.Common => MotelyPrngKeys.JokerCommon,
                MotelyJokerRarity.Uncommon => MotelyPrngKeys.JokerUncommon,
                MotelyJokerRarity.Rare => MotelyPrngKeys.JokerRare,
                _ => MotelyPrngKeys.JokerCommon
            };

            // Include source in the key! Shop jokers come from shop source
            var prng = searchContext.CreatePrngStreamCached(prngKey + OuijaSourceConstants.SHOP + ante);
            if (jokerRarity == MotelyJokerRarity.Common)
            {
                var choices = MotelyEnum<MotelyJokerCommon>.Values;
                var jv = searchContext.GetNextRandomElement(ref prng, choices);
                var targetInRarity = GetJokerInRarityEnum<MotelyJokerCommon>(targetJoker);
                DebugLogger.LogFormat("[DEBUG] (Need) Joker PRNG: key={0}, ante={1}, generated vector={2}, target={3} ({4})",
                    prngKey + OuijaSourceConstants.SHOP + ante, ante, jv, targetInRarity, (int)targetJoker);
            }
            else if (jokerRarity == MotelyJokerRarity.Uncommon)
            {
                var choices = MotelyEnum<MotelyJokerUncommon>.Values;
                var jv = searchContext.GetNextRandomElement(ref prng, choices);
                var targetInRarity = GetJokerInRarityEnum<MotelyJokerUncommon>(targetJoker);
                DebugLogger.LogFormat("[DEBUG] (Need) Joker PRNG: key={0}, ante={1}, generated vector={2}, target={3} ({4})",
                    prngKey + OuijaSourceConstants.SHOP + ante, ante, jv, targetInRarity, (int)targetJoker);
            }
            else if (jokerRarity == MotelyJokerRarity.Rare)
            {
                var choices = MotelyEnum<MotelyJokerRare>.Values;
                var jv = searchContext.GetNextRandomElement(ref prng, choices);
                var targetInRarity = GetJokerInRarityEnum<MotelyJokerRare>(targetJoker);
                DebugLogger.LogFormat("[DEBUG] (Need) Joker PRNG: key={0}, ante={1}, generated vector={2}, target={3} ({4})",
                    prngKey + OuijaSourceConstants.SHOP + ante, ante, jv, targetInRarity, (int)targetJoker);
            }
            VectorMask jokerMask = jokerRarity switch
            {
                MotelyJokerRarity.Common => ProcessJokerRarityVector<MotelyJokerCommon>(ref searchContext, ref prng, targetJoker),
                MotelyJokerRarity.Uncommon => ProcessJokerRarityVector<MotelyJokerUncommon>(ref searchContext, ref prng, targetJoker),
                MotelyJokerRarity.Rare => ProcessJokerRarityVector<MotelyJokerRare>(ref searchContext, ref prng, targetJoker),
                _ => default
            };
    
            // Check edition if specified
            if (!string.IsNullOrEmpty(need.Edition) && need.Edition != "None")
            {
                var editionPrng = searchContext.CreatePrngStreamCached(MotelyPrngKeys.JokerEdition + OuijaSourceConstants.SHOP + ante);
                var editionRolls = searchContext.GetNextRandom(ref editionPrng);
                var threshold = GetEditionThreshold(need.Edition);
                var editionMask = Vector512.LessThan(editionRolls, Vector512.Create(threshold));
                DebugLogger.LogFormat("[DEBUG] (Need) Joker Edition: {0}, rolls={1}, threshold={2}", need.Edition, editionRolls, threshold);
                jokerMask &= editionMask;
            }
    
            return mask & jokerMask;
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
            DebugLogger.LogFormat("Tarot card needs not yet implemented");
            return mask;
        }

        private static VectorMask ProcessPlanetNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.PlanetEnum.HasValue)
            {
                DebugLogger.Log("Missing PlanetEnum for Planet need");
                return mask;
            }
            var planetMask = searchContext.FilterPlanetCard(ante, need.PlanetEnum.Value, MotelyPrngKeys.Shop);
            return mask & planetMask;
        }

        private static VectorMask ProcessSpectralNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.SpectralEnum.HasValue)
            {
                DebugLogger.Log("Missing SpectralEnum for Spectral need");
                return mask;
            }
            var spectralMask = searchContext.FilterSpectralCard(ante, need.SpectralEnum.Value, MotelyPrngKeys.Shop);
            return mask & spectralMask;
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

            // Process each want and accumulate scores
            if (config?.Wants != null)
            {
                for (int wantIndex = 0; wantIndex < config.Wants.Length && wantIndex < 32; wantIndex++)
                {
                    var want = config.Wants[wantIndex];
                    if (want == null) continue;

                    var searchAntes = want.SearchAntes?.Length > 0 ? want.SearchAntes : new[] { want.DesireByAnte };
                    foreach (var ante in searchAntes)
                    {
                        var (score, isNaturalNeg, isDesiredNeg) = ProcessWantForAnte(ref singleCtx, want, ante);
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

        static void PrintResult(OuijaResult result, OuijaConfig config)
        {
            if (config == null)
            {
                Console.WriteLine($"|{result.Seed},{result.TotalScore}");
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

                case MotelyItemTypeCategory.PlanetCard:
                    return ProcessPlanetWant(ref singleCtx, want, ante);

                case MotelyItemTypeCategory.SpectralCard:
                    return ProcessSpectralWant(ref singleCtx, want, ante);

                default:
                    DebugLogger.LogFormat("❌ Unhandled want type: {0}", want.Type);
                    return (0, false, false);
            }
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessJokerWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            var targetJoker = ParseJoker(want);
            if (targetJoker == default)
                return (0, false, false);

            var jokerRarity = GetJokerRarity(targetJoker);
            
            // Handle legendary jokers specially (they come from Soul cards)
            if (jokerRarity == MotelyJokerRarity.Legendary)
            {
                bool found = PerkeoStyleSoulJokerCheck(ref singleCtx, targetJoker);
                DebugLogger.LogFormat("[DEBUG] (Want) Legendary Joker: Checking for {0} in seed {1} => {2}", targetJoker, singleCtx.GetCurrentSeed(), found);
                return found ? (want.Score, false, false) : (0, false, false);
            }

            string prngKey = jokerRarity switch
            {
                MotelyJokerRarity.Common => MotelyPrngKeys.JokerCommon,
                MotelyJokerRarity.Uncommon => MotelyPrngKeys.JokerUncommon,
                MotelyJokerRarity.Rare => MotelyPrngKeys.JokerRare,
                _ => MotelyPrngKeys.JokerCommon
            };

            // Include source!
            var prng = singleCtx.CreatePrngStream(prngKey + OuijaSourceConstants.SHOP + ante);
            bool jokerMatches = false;
            if (jokerRarity == MotelyJokerRarity.Common)
            {
                var choices = MotelyEnum<MotelyJokerCommon>.Values;
                var j = singleCtx.GetNextRandomElement(ref prng, choices);
                var generatedJoker = (MotelyJoker)((int)MotelyJokerRarity.Common + (int)j);
                DebugLogger.LogFormat("[DEBUG] (Want) Joker PRNG: key={0}, ante={1}, generated={2} ({3}), target={4} ({5}), seed={6}",
                    prngKey + OuijaSourceConstants.SHOP + ante, ante, generatedJoker, (int)generatedJoker, targetJoker, (int)targetJoker, singleCtx.GetCurrentSeed());
                jokerMatches = generatedJoker.Equals(targetJoker);
            }
            else if (jokerRarity == MotelyJokerRarity.Uncommon)
            {
                var choices = MotelyEnum<MotelyJokerUncommon>.Values;
                var j = singleCtx.GetNextRandomElement(ref prng, choices);
                var generatedJoker = (MotelyJoker)((int)MotelyJokerRarity.Uncommon + (int)j);
                DebugLogger.LogFormat("[DEBUG] (Want) Joker PRNG: key={0}, ante={1}, generated={2} ({3}), target={4} ({5}), seed={6}",
                    prngKey + OuijaSourceConstants.SHOP + ante, ante, generatedJoker, (int)generatedJoker, targetJoker, (int)targetJoker, singleCtx.GetCurrentSeed());
                jokerMatches = generatedJoker.Equals(targetJoker);
            }
            else if (jokerRarity == MotelyJokerRarity.Rare)
            {
                var choices = MotelyEnum<MotelyJokerRare>.Values;
                var j = singleCtx.GetNextRandomElement(ref prng, choices);
                var generatedJoker = (MotelyJoker)((int)MotelyJokerRarity.Rare + (int)j);
                DebugLogger.LogFormat("[DEBUG] (Want) Joker PRNG: key={0}, ante={1}, generated={2} ({3}), target={4} ({5}), seed={6}",
                    prngKey + OuijaSourceConstants.SHOP + ante, ante, generatedJoker, (int)generatedJoker, targetJoker, (int)targetJoker, singleCtx.GetCurrentSeed());
                jokerMatches = generatedJoker.Equals(targetJoker);
            }
    
            // Check edition if specified
            if (!string.IsNullOrEmpty(want.Edition) && want.Edition != "None")
            {
                var hasEdition = CheckJokerEdition(ref singleCtx, want.Edition, ante);
                DebugLogger.LogFormat("[DEBUG] (Want) Joker Edition: {0}, hasEdition={1}", want.Edition, hasEdition);
                bool isNaturalNeg = want.Edition == "Negative" && hasEdition;
                bool isDesiredNeg = want.Edition == "Negative" && hasEdition;
                return hasEdition ? (want.Score, isNaturalNeg, isDesiredNeg) : (0, false, false);
            }

            return jokerMatches ? (want.Score, false, false) : (0, false, false);
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

            var planetStream = singleCtx.CreateShopPlanetStream(ante);
            var planet = singleCtx.GetNextShopPlanet(ref planetStream);
            DebugLogger.LogFormat("[DEBUG] (Want) Planet PRNG: ante={0}, generated={1}, target={2}, seed={3}", ante, planet.Type, ((MotelyItemType)MotelyItemTypeCategory.PlanetCard | (MotelyItemType)want.PlanetEnum.Value), singleCtx.GetCurrentSeed());
            bool matches = planet.Type == ((MotelyItemType)MotelyItemTypeCategory.PlanetCard | (MotelyItemType)want.PlanetEnum.Value);
            return matches ? (want.Score, false, false) : (0, false, false);
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessSpectralWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            if (!want.SpectralEnum.HasValue)
                return (0, false, false);

            var spectralStream = singleCtx.CreateShopSpectralStream(ante);
            var spectral = singleCtx.GetNextShopSpectral(ref spectralStream);
            DebugLogger.LogFormat("[DEBUG] (Want) Spectral PRNG: ante={0}, generated={1}, target={2}, seed={3}", ante, spectral.Type, ((MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)want.SpectralEnum.Value), singleCtx.GetCurrentSeed());
            bool matches = spectral.Type == ((MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)want.SpectralEnum.Value);
            return matches ? (want.Score, false, false) : (0, false, false);
        }
    }

    public static OuijaJsonFilterDesc LoadFromFile(string configFileName)
    {
        var config = OuijaConfigLoader.Load(configFileName);
        return new OuijaJsonFilterDesc(config);
    }
}
