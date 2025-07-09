using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;
using System.Collections.Generic;
using Motely;
using Motely.Filters;

namespace Motely;

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
                    CacheStreamForType(ctx, need.Type, ante);
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
                    CacheStreamForType(ctx, want.Type, ante);
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
        return false;
    }

    private static void CacheStreamForType(MotelyFilterCreationContext ctx, string type, int ante)
    {
        if (string.IsNullOrEmpty(type))
        {
            DebugLogger.LogFormat("[OuijaJsonFilterDesc] Empty type for ante {0}", ante);
            return;
        }

        if (!TryParseTypeCategory(type, out var parsedType)) 
        {
            // Handle special cases that don't map directly to MotelyItemTypeCategory
            if (string.Equals(type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
            {
                // Soul jokers come from The Soul card in Arcana/Spectral packs
                // Need to cache tarot streams and soul joker streams
                ctx.CacheTarotStream(ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerSoul + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.ShopPack + ante);
                return;
            }
            
            DebugLogger.LogFormat("[OuijaJsonFilterDesc] Unknown type '{0}' (ante {1}) - no canonical PRNG key used", type, ante);
            return;
        }
        
        switch (parsedType)
        {
            case MotelyItemTypeCategory.Joker:
                // Regular jokers need different streams based on rarity
                // Regular jokers need different streams based on rarity WITH SOURCE
                ctx.CachePseudoHash(MotelyPrngKeys.JokerCommon + "sho" + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerUncommon + "sho" + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerRare + "sho" + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerLegendary + "sho" + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerEdition + "sho" + ante);
                break;
            case MotelyItemTypeCategory.PlayingCard:
                // TODO: Implement when playing card streams are available
                DebugLogger.LogFormat("[OuijaJsonFilterDesc] PlayingCard streams not yet implemented for ante {0}", ante);
                break;
            case MotelyItemTypeCategory.TarotCard:
                ctx.CacheTarotStream(ante);
                break;
            case MotelyItemTypeCategory.PlanetCard:
                // TODO: Implement when planet card streams are available
                DebugLogger.LogFormat("[OuijaJsonFilterDesc] PlanetCard streams not yet implemented for ante {0}", ante);
                break;
            case MotelyItemTypeCategory.SpectralCard:
                // TODO: Implement when spectral card streams are available
                DebugLogger.LogFormat("[OuijaJsonFilterDesc] SpectralCard streams not yet implemented for ante {0}", ante);
                break;
            default:
                DebugLogger.LogFormat("[OuijaJsonFilterDesc] Unhandled canonical type '{0}' (ante {1}) - no PRNG key used", parsedType, ante);
                break;
        }
    }

    public struct OuijaJsonFilter : IMotelySeedFilter
    {
        // Source constants matching Immolate's source_str() function
        private const string SOURCE_SHOP = "sho";
        private const string SOURCE_ARCANA = "ar1";
        private const string SOURCE_BUFFOON = "buf";
        private const string SOURCE_SOUL = "sou";
        
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
                DebugLogger.Log("Filter method called");
                VectorMask mask = VectorMask.AllBitsSet;

                if (_config == null)
                {
                    DebugLogger.Log("ERROR: Config is null in Filter method");
                    return default;
                }

                // Copy config to local variable to avoid struct member access in lambda
                var localConfig = _config;

                // PHASE 1: Vector filtering for NEEDS (hard requirements)
                mask = ProcessNeeds(ref searchContext, mask, localConfig);
                 
                int needsPassCount = CountBitsSet(mask);
                DebugLogger.LogFormat("After ProcessNeeds: {0} seeds passed (mask value: {1})", needsPassCount, mask.Value);
                
                if (mask.IsAllFalse()) 
                {
                    DebugLogger.Log("All seeds filtered out by NEEDS - no seeds passed requirements");
                    return mask;
                }

                // PHASE 2: Individual seed processing for WANTS (scoring)
                mask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
                {
                    var result = ProcessWantsAndScore(ref singleCtx, localConfig);

                    if (result.Success)
                    {
                        PrintResult(result, localConfig);
                    }
                    
                    return result.Success;
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

            DebugLogger.LogFormat("Processing {0} needs", config.Needs.Length);

            // Group needs by ante for efficient stream processing
            var needsByAnte = config.Needs
                .Where(n => n != null)
                .SelectMany(n => 
                {
                    var antes = n.SearchAntes?.Length > 0 ? n.SearchAntes : new[] { n.DesireByAnte };
                    return antes.Select(ante => (ante, need: n));
                })
                .GroupBy(x => x.ante)
                .OrderBy(g => g.Key);

            foreach (var anteGroup in needsByAnte)
            {
                int ante = anteGroup.Key;
                
                // Process all needs for this ante
                foreach (var (_, need) in anteGroup)
                {
                    mask = ProcessNeedVector(ref searchContext, need, ante, mask);
                    if (mask.IsAllFalse()) return default; // Early exit optimization
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
            if (need == null || string.IsNullOrEmpty(need.Type))
            {
                DebugLogger.Log("Invalid need: null or empty type");
                return mask;
            }

            if (!TryParseTypeCategory(need.Type, out var typeCat))
            {
                // Handle special joker types
                if (string.Equals(need.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessSoulJokerNeed(ref searchContext, need, ante, mask);
                }
                
                DebugLogger.LogFormat("⚠️  Unknown need type: {0}", need.Type);
                return mask;
            }
            
            switch(typeCat)
            {
                case MotelyItemTypeCategory.Joker:
                    return ProcessJokerNeed(ref searchContext, need, ante, mask);

                case MotelyItemTypeCategory.TarotCard:
                    return ProcessTarotNeed(ref searchContext, need, ante, mask);

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
            if (string.IsNullOrEmpty(need.Value))
            {
                DebugLogger.Log("Empty joker value in need");
                return mask;
            }

            var targetJoker = ParseJoker(need.Value);
            if (targetJoker == default)
            {
                DebugLogger.LogFormat("❌ Unknown joker: {0}", need.Value);
                return mask;
            }

            var jokerRarity = GetJokerRarity(targetJoker);
            
            // Legendary jokers like Perkeo come from Soul cards in Arcana packs, NOT shop streams!
            if (jokerRarity == MotelyJokerRarity.Legendary)
            {
                DebugLogger.LogFormat("🔍 Legendary joker {0} requires Soul detection - using individual seed processing", need.Value);
                var legendaryMask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
                {
                    return ProcessLegendaryJokerFromSoul(ref singleCtx, targetJoker, ante);
                });
                DebugLogger.LogFormat("Legendary joker search complete - {0} seeds found with {1}", CountBitsSet(legendaryMask), need.Value);
                return legendaryMask;
            }
            
            // Non-legendary jokers use regular shop streams
            string prngKey = jokerRarity switch
            {
                MotelyJokerRarity.Common => MotelyPrngKeys.JokerCommon,
                MotelyJokerRarity.Uncommon => MotelyPrngKeys.JokerUncommon,
                MotelyJokerRarity.Rare => MotelyPrngKeys.JokerRare,
                _ => MotelyPrngKeys.JokerCommon
            };

            // Include source in the key! Shop jokers come from shop source
            var prng = searchContext.CreatePrngStreamCached(prngKey + SOURCE_SHOP + ante);
            
            VectorMask jokerMask = jokerRarity switch
            {
                MotelyJokerRarity.Common => ProcessJokerRarityVector<MotelyJokerCommon>(ref searchContext, ref prng, targetJoker),
                MotelyJokerRarity.Uncommon => ProcessJokerRarityVector<MotelyJokerUncommon>(ref searchContext, ref prng, targetJoker),
                MotelyJokerRarity.Rare => ProcessJokerRarityVector<MotelyJokerRare>(ref searchContext, ref prng, targetJoker),
                _ => default
            };
            
            DebugLogger.LogFormat("Looking for {0} (rarity: {1}) at ante {2} with key: {3}", 
                need.Value, jokerRarity, ante, prngKey + SOURCE_SHOP + ante);
            
            // Check edition if specified
            if (!string.IsNullOrEmpty(need.Edition) && need.Edition != "None")
            {
                // Edition also needs source!
                var editionPrng = searchContext.CreatePrngStreamCached(MotelyPrngKeys.JokerEdition + SOURCE_SHOP + ante);
                var editionRolls = searchContext.GetNextRandom(ref editionPrng);
                var threshold = GetEditionThreshold(need.Edition);
                var editionMask = Vector512.LessThan(editionRolls, Vector512.Create(threshold));
                jokerMask &= editionMask;
            }
            
            return mask & jokerMask;
        }
        
        private static bool ProcessLegendaryJokerFromSoul(ref MotelySingleSearchContext singleCtx, MotelyJoker targetJoker, int ante)
        {
            // Use Perkeo-style logic for legendary/soul jokers
            return PerkeoStyleSoulJokerCheck(ref singleCtx, targetJoker);
        }

        private static VectorMask ProcessSoulJokerNeed(ref MotelyVectorSearchContext searchContext, 
            OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (string.IsNullOrEmpty(need.Value))
            {
                DebugLogger.Log("Empty value for SoulJoker need");
                return mask;
            }

            // Soul jokers come from arcana packs - this is more complex and might need individual seed processing
            return searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                return ProcessSoulJokerIndividual(ref singleCtx, need, ante);
            });
        }

        private static bool ProcessSoulJokerIndividual(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire need, int ante)
        {
            var targetJoker = ParseJoker(need.Value);
            return PerkeoStyleSoulJokerCheck(ref singleCtx, targetJoker);
        }

        private static VectorMask ProcessTarotNeed(ref MotelyVectorSearchContext searchContext, 
            OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            // TODO: Implement tarot card filtering
            DebugLogger.LogFormat("Tarot card needs not yet implemented");
            return mask;
        }

        public static ConcurrentQueue<OuijaResult>? ResultsQueue { get; set; }

        private static OuijaResult ProcessWantsAndScore(ref MotelySingleSearchContext singleCtx, OuijaConfig config)
        {
            string seed = singleCtx.GetCurrentSeed();
            var result = new OuijaResult
            {
                Seed = seed,
                TotalScore = 0,
                NaturalNegativeJokers = 0,
                DesiredNegativeJokers = 0,
                ScoreWants = new int[32],
                Success = false
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
            }

            // Total score is just the sum of all want scores
            int totalScore = 0;
            for (int i = 0; i < 32; i++)
            {
                totalScore += result.ScoreWants[i];
            }

            result.TotalScore = (ushort)totalScore;
            result.Success = totalScore >= GetMinimumScore(config);

            return result;
        }

        static void PrintResult(OuijaResult result, OuijaConfig config)
        {
            if (config == null)
            {
                Console.WriteLine($"|{result.Seed},{result.TotalScore}");
                return;
            }

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
            if (want == null || string.IsNullOrEmpty(want.Type))
            {
                DebugLogger.Log("Invalid want: null or empty type");
                return (0, false, false);
            }

            if (!TryParseTypeCategory(want.Type, out var typeCat))
            {
                // Handle special cases
                if (string.Equals(want.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessSoulJokerWant(ref singleCtx, want, ante);
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

                default:
                    DebugLogger.LogFormat("❌ Unhandled want type: {0}", want.Type);
                    return (0, false, false);
            }
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessJokerWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            if (string.IsNullOrEmpty(want.Value))
                return (0, false, false);

            var targetJoker = ParseJoker(want.Value);
            if (targetJoker == default)
                return (0, false, false);

            var jokerRarity = GetJokerRarity(targetJoker);
            
            // Handle legendary jokers specially (they come from Soul cards)
            if (jokerRarity == MotelyJokerRarity.Legendary)
            {
                bool found = PerkeoStyleSoulJokerCheck(ref singleCtx, targetJoker);
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
            var prng = singleCtx.CreatePrngStream(prngKey + SOURCE_SHOP + ante);
            
            bool jokerMatches = jokerRarity switch
            {
                MotelyJokerRarity.Common => ProcessJokerRaritySingle<MotelyJokerCommon>(ref singleCtx, ref prng, targetJoker),
                MotelyJokerRarity.Uncommon => ProcessJokerRaritySingle<MotelyJokerUncommon>(ref singleCtx, ref prng, targetJoker),
                MotelyJokerRarity.Rare => ProcessJokerRaritySingle<MotelyJokerRare>(ref singleCtx, ref prng, targetJoker),
                _ => false
            };
            
            if (!jokerMatches)
                return (0, false, false);

            // Check edition if specified
            if (!string.IsNullOrEmpty(want.Edition) && want.Edition != "None")
            {
                var hasEdition = CheckJokerEdition(ref singleCtx, want.Edition, ante);
                bool isNaturalNeg = want.Edition == "Negative" && hasEdition;
                bool isDesiredNeg = want.Edition == "Negative" && hasEdition;
                
                return hasEdition ? (want.Score, isNaturalNeg, isDesiredNeg) : (0, false, false);
            }

            return (want.Score, false, false);
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessSoulJokerWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            if (string.IsNullOrEmpty(want.Value))
                return (0, false, false);

            var targetJoker = ParseJoker(want.Value);
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

        private static bool CheckJokerEdition(ref MotelySingleSearchContext singleCtx, string edition, int ante)
        {
            // Include source!
            var editionPrng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerEdition + SOURCE_SHOP + ante);
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
        private static MotelyJoker ParseJoker(string value)
        {
            if (string.IsNullOrEmpty(value))
                return default;
                
            if (MotelyEnumUtil.TryParseEnum<MotelyJoker>(value, out var joker))
                return joker;
            return default;
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
                    if (joker.Type == (MotelyItemType)(int)targetJoker)
                        return true;
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
                            if (joker.Type == (MotelyItemType)(int)targetJoker)
                                return true;
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
                            return true;
                    }
                }
            }
            return false;
        }
    }

    public static OuijaJsonFilterDesc LoadFromFile(string configFileName)
    {
        var config = OuijaConfig.Load(configFileName, OuijaConfig.GetOptions());
        return new OuijaJsonFilterDesc(config);
    }
}
