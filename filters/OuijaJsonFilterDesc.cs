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

    public OuijaJsonFilterDesc(OuijaConfig config) => Config = config;

    public OuijaJsonFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        DebugLogger.LogFormat("Creating OuijaJsonFilter with {0} needs and {1} wants", Config.Needs?.Length ?? 0, Config.Wants?.Length ?? 0);
        // Cache streams for all filter types and antes used in needs/wants
        var allAntes = new HashSet<int>();
        
        foreach (var need in Config.Needs ?? [])
        {
            foreach (var ante in need.SearchAntes ?? [need.DesireByAnte])
            {
                allAntes.Add(ante);
                CacheStreamForType(ctx, need.Type, ante);
            }
        }
        
        foreach (var want in Config.Wants ?? [])
        {
            foreach (var ante in want.SearchAntes ?? [want.DesireByAnte])
            {
                allAntes.Add(ante);
                CacheStreamForType(ctx, want.Type, ante);
            }
        }

        return new OuijaJsonFilter(Config);
    }

    // Tolerant parse for Type using MotelyItemTypeCategory
    private static bool TryParseTypeCategory(string value, out MotelyItemTypeCategory type)
    {
        if (MotelyEnumUtil.TryParseEnum<MotelyItemTypeCategory>(value, out type))
            return true;
        return false;
    }

    private static void CacheStreamForType(MotelyFilterCreationContext ctx, string type, int ante)
    {
        if (!TryParseTypeCategory(type, out var parsedType)) {
            DebugLogger.LogFormat("[OuijaJsonFilterDesc] Unknown type '{0}' (ante {1}) - no canonical PRNG key used", type, ante);
            return; // Unknown type, skip
        }
        switch (parsedType)
        {
            case MotelyItemTypeCategory.Joker:
                ctx.CachePseudoHash(MotelyPrngKeys.TerrotSoul + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerEdition + ante);
                break;
            case MotelyItemTypeCategory.PlayingCard:
                //ctx.CachePseudoHash(MotelyPrngKeys.Card + ante); TODO
                //ctx.CachePseudoHash(MotelyPrngKeys.StandardEdition + ante); TODO
                break;
            case MotelyItemTypeCategory.TarotCard:
                ctx.CachePseudoHash(MotelyPrngKeys.Tarot + ante);
                break;
            case MotelyItemTypeCategory.PlanetCard:
                //ctx.CachePseudoHash(MotelyPrngKeys.Planet + ante); TODO
                break;
            case MotelyItemTypeCategory.SpectralCard:
                //ctx.CachePseudoHash(MotelyPrngKeys.Spectral + ante); TODO
                break;
            default:
                DebugLogger.LogFormat("[OuijaJsonFilterDesc] Unhandled canonical type '{0}' (ante {1}) - no PRNG key used", parsedType, ante);
                break;
        }
    }

    public struct OuijaJsonFilter : IMotelySeedFilter
    {
        public OuijaConfig Config { get; }
        
        public OuijaJsonFilter(OuijaConfig config) => Config = config;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            DebugLogger.Log("Filter method called");
            VectorMask mask = VectorMask.AllBitsSet;

            // PHASE 1: Vector filtering for NEEDS (filtering)
            var localConfig = Config; // Copy to avoid struct lambda capture issues
            mask = ProcessNeeds(ref searchContext, mask, localConfig);
             
            // Debug: Check how many seeds passed NEEDS filtering
            int needsPassCount = CountBitsSet(mask);
            if (needsPassCount > 0)
            {
                DebugLogger.LogFormat("{0} seeds passed NEEDS filtering", needsPassCount);
            }
            
            if (mask.IsAllFalse()) return mask;

            // PHASE 2: Individual seed processing for WANTS (scoring)
            mask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                var result = ProcessWantsAndScore(ref singleCtx, localConfig);

                if (result.Success)
                {
                    string seed = singleCtx.GetCurrentSeed();
                    string csvLine = $"|{seed},{result.TotalScore},{result.NaturalNegativeJokers},{result.DesiredNegativeJokers},{string.Join(",", result.ScoreWants)}";
                    FancyConsole.WriteLine($"{csvLine}");
                }
                
                return result.Success;
            });
            
            return mask;
        }

        private static VectorMask ProcessNeeds(ref MotelyVectorSearchContext searchContext, VectorMask mask, OuijaConfig config)
        {
            var jokerChoices = (MotelyJoker[])Enum.GetValues(typeof(MotelyJoker));

            // Group needs by ante for efficient stream processing
            var needsByAnte = (config.Needs ?? [])
                .SelectMany(n => (n.SearchAntes ?? [n.DesireByAnte]).Select(ante => (ante, need: n)))
                .GroupBy(x => x.ante)
                .OrderBy(g => g.Key);

            foreach (var anteGroup in needsByAnte)
            {
                int ante = anteGroup.Key;
                
                // Process all needs for this ante
                foreach (var (_, need) in anteGroup)
                {
                    mask = ProcessNeedVector(ref searchContext, need, ante, jokerChoices, mask);
                    if (mask.IsAllFalse()) return default; // Early exit optimization
                }
            }

            return mask;
        }

        private static int CountBitsSet(VectorMask mask)
        {
            int count = 0;
            for (int i = 0; i < 8; i++)
            {
                if (mask[i]) count++;
            }
            return count;
        }

        private static VectorMask ProcessNeedVector(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, 
            int ante, MotelyJoker[] jokerChoices, VectorMask mask)
        {
            if (!TryParseTypeCategory(need.Type, out var typeCat))
            {
                DebugLogger.LogFormat("⚠️  Unknown need type: {0}", need.Type);
                return mask;
            }
            switch(typeCat)
            {
                case MotelyItemTypeCategory.Joker:
                    // Handle SoulJoker as a special case (legendary jokers from TheSoul)
                    bool isSoulJoker = string.Equals(need.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase);
                        
                    var prng = isSoulJoker ? searchContext.CreatePrngStream(MotelyPrngKeys.TerrotSoul + ante)
                                           : searchContext.CreatePrngStream(MotelyPrngKeys.TerrotSoul + ante);
                    var jokerVec = searchContext.GetNextRandomElement(ref prng, jokerChoices);
                    var targetJoker = ParseJoker(need.Value);
                    var jokerMask = (VectorMask)VectorEnum256.Equals(jokerVec, targetJoker);
                    
                    // Debug: Check what jokers are being generated
                    DebugLogger.LogFormat("Looking for {0} ({1}) at ante {2}", need.Value, targetJoker, ante);
                    DebugLogger.LogFormat("Generated jokers in vector: {0}", string.Join(", ", Enumerable.Range(0, 8).Select(i => jokerVec[i])));
                    DebugLogger.LogFormat("Joker mask result: {0}", string.Join(", ", Enumerable.Range(0, 8).Select(i => jokerMask[i])));
                    
                    // Check for matches manually
                    for (int i = 0; i < 8; i++)
                    {
                        if (jokerVec[i] == targetJoker)
                        {
                            DebugLogger.LogFormat("MATCH FOUND at lane {0}: {1} == {2}", i, jokerVec[i], targetJoker);
                        }
                    }
                    
                    // Check edition if specified - simplified for now
                    if (!string.IsNullOrEmpty(need.Edition) && need.Edition != "None")
                    {
                        var editionPrng = searchContext.CreatePrngStream(MotelyPrngKeys.JokerEdition + ante);
                        var editionRolls = searchContext.GetNextRandom(ref editionPrng);
                        var threshold = GetEditionThreshold(need.Edition);
                        var editionMask = Vector512.LessThan(editionRolls, Vector512.Create(threshold));
                        var editionMask256 = new VectorMask(MotelyVectorUtils.VectorMaskToIntMask(editionMask));
                        jokerMask &= editionMask256;
                    }
                    return mask & jokerMask;

                case MotelyItemTypeCategory.PlayingCard:
                    // ...existing code for PlayingCard...
                    return mask; // Implement as needed

                default:
                    DebugLogger.LogFormat("❌ Unknown need type: {0}", need.Type);
                    return mask;
            }
        }

        public static ConcurrentQueue<OuijaResult>? ResultsQueue { get; set; }

        private static OuijaResult ProcessWantsAndScore(ref MotelySingleSearchContext singleCtx, OuijaConfig config)
        {
            int totalScore = 0;
            int naturalNegatives = 0;
            int desiredNegatives = 0;
            byte[] scoreWants = new byte[32];

            var jokerChoices = (MotelyJoker[])Enum.GetValues(typeof(MotelyJoker));

            // Process each want and accumulate scores
            for (int wantIndex = 0; wantIndex < (config.Wants?.Length ?? 0); wantIndex++)
            {
                var want = config.Wants![wantIndex]; // Null-forgiving since we checked length above
                int wantScore = 0;

                foreach (var ante in want.SearchAntes ?? [want.DesireByAnte])
                {
                    var (score, isNaturalNeg, isDesiredNeg) = ProcessWantForAnte(ref singleCtx, want, ante, jokerChoices);
                    wantScore += score;
                    if (isNaturalNeg) naturalNegatives++;
                    if (isDesiredNeg) desiredNegatives++;
                }

                // Directly populate the ScoreWants array
                scoreWants[wantIndex] = (byte)Math.Max(0, Math.Min(255, wantScore));
                totalScore += wantScore;
            }

            // Apply threshold filtering - For simple test, allow any score >= 0
            bool passes = totalScore >= GetMinimumScore(config);

            // Store the scoring result for this seed
            string seed = singleCtx.GetCurrentSeed();
            var result = new OuijaResult
            {
                Seed = seed,
                TotalScore = (ushort)Math.Max(0, totalScore),
                NaturalNegativeJokers = (byte)naturalNegatives,
                DesiredNegativeJokers = (byte)desiredNegatives,
                ScoreWants = scoreWants,
                Success = passes
            };

            return result;
        }


        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessWantForAnte(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante, MotelyJoker[] jokerChoices)
        {
            if (!TryParseTypeCategory(want.Type, out var typeCat))
            {
                DebugLogger.LogFormat("❌ Unknown want type: {0}", want.Type);
                return (0, false, false);
            }
            switch (typeCat)
            {
                case MotelyItemTypeCategory.Joker:
                    return ProcessJokerWant(ref singleCtx, want, ante, jokerChoices, false);

                case MotelyItemTypeCategory.PlayingCard:
                    return ProcessCardWant(ref singleCtx, want, ante);

                default:
                    DebugLogger.LogFormat("❌ Unknown want type: {0}", want.Type);
                    return (0, false, false);
            }
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessJokerWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante, 
            MotelyJoker[] jokerChoices, bool isJoker)
        {
            // Get the joker
            var prng = isJoker 
                ? singleCtx.CreatePrngStream(MotelyPrngKeys.TerrotSoul + ante)
                : singleCtx.CreatePrngStream(MotelyPrngKeys.TerrotSoul + ante);
            
            // TODO this is not right 
            var joker = singleCtx.GetNextRandomElement(ref prng, jokerChoices);
            var expectedJoker = ParseJoker(want.Value);
            
            if (joker != expectedJoker)
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

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessCardWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante)
        {
            // Basic playing card implementation
            var cardPrng = singleCtx.CreatePrngStream("deck_cards_" + ante);
            
            // Check rank if specified
            if (!string.IsNullOrEmpty(want.Rank))
            {
                var rank = singleCtx.GetNextRandomInt(ref cardPrng, 1, 14); // A=1, K=13
                var expectedRank = ParseCardRank(want.Rank);
                if (rank != expectedRank) return (0, false, false);
            }

            // Check suit if specified
            if (!string.IsNullOrEmpty(want.Suit))
            {
                var suit = singleCtx.GetNextRandomInt(ref cardPrng, 0, 4); // 0-3 for suits
                var expectedSuit = ParseCardSuit(want.Suit);
                if (suit != expectedSuit) return (0, false, false);
            }

            // Check enhancement/chip if specified
            if (!string.IsNullOrEmpty(want.Enchantment))
            {
                var hasEnhancement = CheckCardEnhancement(ref singleCtx, want.Enchantment, ante);
                if (!hasEnhancement) return (0, false, false);
            }

            return (want.Score, false, false);
        }

        private static bool CheckJokerEdition(ref MotelySingleSearchContext singleCtx, string edition, int ante)
        {
            var editionPrng = singleCtx.CreatePrngStream("edition_" + ante);
            var roll = singleCtx.GetNextRandom(ref editionPrng);
            return roll < GetEditionThreshold(edition);
        }

        private static bool CheckCardEnhancement(ref MotelySingleSearchContext singleCtx, string enhancement, int ante)
        {
            var enhancementPrng = singleCtx.CreatePrngStream("card_enhancement_" + ante);
            var roll = singleCtx.GetNextRandom(ref enhancementPrng);
            
            return enhancement.ToLowerInvariant() switch
            {
                "bonus" => roll < 0.2,        // 20% chance
                "mult" => roll < 0.15,        // 15% chance
                "wild" => roll < 0.05,        // 5% chance
                "glass" => roll < 0.03,       // 3% chance
                "steel" => roll < 0.03,       // 3% chance
                "stone" => roll < 0.03,       // 3% chance
                "gold" => roll < 0.01,        // 1% chance
                "lucky" => roll < 0.01,       // 1% chance
                _ => false
            };
        }

        private static double GetEditionThreshold(string edition)
        {
            return edition.ToLowerInvariant() switch
            {
                "negative" => 0.1,     // 10% chance
                "foil" => 0.05,        // 5% chance
                "holographic" => 0.02, // 2% chance
                "polychrome" => 0.01,  // 1% chance
                _ => 0.0
            };
        }

        private static int GetMinimumScore(OuijaConfig config)
        {
            // Always require at least score 1 to filter out zero scores
            // Seeds with score 0 should never appear in output
            return 1;
        }

        // Helper parsing methods
        // Replace ParseTag with tolerant mapping and reporting
        private static MotelyTag ParseTag(string value)
        {
            if (MotelyEnumUtil.TryParseEnum<MotelyTag>(value, out var tag))
                return tag;
            return default;
        }

        // Replace ParseJoker with tolerant mapping and reporting
        private static MotelyJoker ParseJoker(string value)
        {
            if (MotelyEnumUtil.TryParseEnum<MotelyJoker>(value, out var joker))
                return joker;
            return default;
        }

        private static MotelyItemTypeCategory? ParseConfigDesireType(string value)
        {
            if (MotelyEnumUtil.TryParseEnum<MotelyItemTypeCategory>(value, out var type))
                return type;
            return null;
        }

        // If you add more enums (Edition, Seal, etc.), add similar methods and bags here

        private static int ParseCardRank(string rank)
        {
            return rank?.ToUpperInvariant() switch
            {
                "A" or "ACE" => 1,
                "2" => 2, "3" => 3, "4" => 4, "5" => 5, "6" => 6, "7" => 7, "8" => 8, "9" => 9, "10" => 10,
                "J" or "JACK" => 11,
                "Q" or "QUEEN" => 12,
                "K" or "KING" => 13,
                _ => 0
            };
        }

        private static int ParseCardSuit(string suit)
        {
            return suit?.ToUpperInvariant() switch
            {
                "SPADES" or "S" => 0,
                "HEARTS" or "H" => 1,
                "DIAMONDS" or "D" => 2,
                "CLUBS" or "C" => 3,
                _ => 0
            };
        }
    }

    public static OuijaJsonFilterDesc LoadFromFile(string configFileName)
    {
        var config = OuijaConfig.Load(configFileName, OuijaConfig.GetOptions());
        return new OuijaJsonFilterDesc(config);
    }
}