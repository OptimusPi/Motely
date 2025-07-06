using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

namespace Motely;

public struct OuijaJsonFilterDesc : IMotelySeedFilterDesc<OuijaJsonFilterDesc.OuijaJsonFilter>
{
    public OuijaConfig Config { get; }

    public OuijaJsonFilterDesc(OuijaConfig config) => Config = config;

    public OuijaJsonFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        Console.WriteLine($"Debug: Creating OuijaJsonFilter with {Config.Needs?.Length ?? 0} needs and {Config.Wants?.Length ?? 0} wants");
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

    private static void CacheStreamForType(MotelyFilterCreationContext ctx, string type, int ante)
    {
        switch (type)
        {
            case "SmallBlindTag":
            case "BigBlindTag":
                ctx.CacheTagStream(ante);
                break;
            case "Joker":
                ctx.CachePseudoHash(MotelyPrngKeys.JokerSoul + ante);
                ctx.CachePseudoHash("edition_" + ante); // Cache edition stream
                break;
            case "SoulJoker":
                ctx.CachePseudoHash("soul_joker_" + ante);
                ctx.CachePseudoHash("edition_" + ante);
                break;
            case "Standard_Card":
                ctx.CachePseudoHash("deck_cards_" + ante);
                ctx.CachePseudoHash("card_edition_" + ante);
                break;
        }
    }

    public struct OuijaJsonFilter : IMotelySeedFilter
    {
        public OuijaConfig Config { get; }
        
        // Thread-local storage for scoring results
        private static readonly ThreadLocal<Dictionary<string, OuijaResult>> _threadLocalResults = 
            new(() => new Dictionary<string, OuijaResult>());
        
        public static Dictionary<string, OuijaResult> GetThreadLocalResults() => _threadLocalResults.Value!;
        
        public OuijaJsonFilter(OuijaConfig config) => Config = config;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            Console.WriteLine($"Debug: Filter method called");
            VectorMask mask = VectorMask.AllBitsSet;

            // PHASE 1: Vector filtering for NEEDS (filtering)
            var localConfig = Config; // Copy to avoid struct lambda capture issues
            mask = ProcessNeeds(ref searchContext, mask, localConfig);
             
            // Debug: Check how many seeds passed NEEDS filtering
            int needsPassCount = CountBitsSet(mask);
            if (needsPassCount > 0)
            {
                Console.WriteLine($"Debug: {needsPassCount} seeds passed NEEDS filtering");
            }
            
            if (mask.IsAllFalse()) return mask;

            // PHASE 2: Individual seed processing for WANTS (scoring)
            mask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                bool result = ProcessWantsAndScore(ref singleCtx, localConfig);
                if (result)
                {
                    Console.WriteLine($"Debug: Seed passed WANTS scoring");
                }
                return result;
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
                    if (mask.IsAllFalse()) return mask; // Early exit optimization
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
            switch (need.Type)
            {
                case "SmallBlindTag":
                    var tagStream = searchContext.CreateTagStream(ante);
                    var tag = searchContext.GetNextTag(ref tagStream);
                    VectorMask tagMask = VectorEnum256.Equals(tag, ParseTag(need.Value));
                    return mask & tagMask;

                case "BigBlindTag":
                    var bigTagStream = searchContext.CreateTagStream(ante);
                    searchContext.GetNextTag(ref bigTagStream); // Skip small blind
                    var bigTag = searchContext.GetNextTag(ref bigTagStream); // Get big blind
                    VectorMask bigTagMask = VectorEnum256.Equals(bigTag, ParseTag(need.Value));
                    return mask & bigTagMask;

                case "Joker":
                    var prng = searchContext.CreatePrngStream(MotelyPrngKeys.JokerSoul + ante);
                    var jokerVec = searchContext.GetNextRandomElement(ref prng, jokerChoices);
                    var targetJoker = ParseJoker(need.Value);
                    var jokerMask = (VectorMask)VectorEnum256.Equals(jokerVec, targetJoker);
                    
                    // Debug: Check what jokers are being generated
                    Console.WriteLine($"Debug: Looking for {need.Value} ({targetJoker}) at ante {ante}");
                    Console.WriteLine($"Debug: Generated jokers in vector: {string.Join(", ", Enumerable.Range(0, 8).Select(i => jokerVec[i]))}");
                    Console.WriteLine($"Debug: Joker mask result: {string.Join(", ", Enumerable.Range(0, 8).Select(i => jokerMask[i]))}");
                    
                    // Check for matches manually
                    for (int i = 0; i < 8; i++)
                    {
                        if (jokerVec[i] == targetJoker)
                        {
                            Console.WriteLine($"Debug: MATCH FOUND at lane {i}: {jokerVec[i]} == {targetJoker}");
                        }
                    }
                    
                    // Check edition if specified - simplified for now
                    if (!string.IsNullOrEmpty(need.Edition) && need.Edition != "None")
                    {
                        // For needs, we'll do a simple probability check without full vector processing
                        var editionPrng = searchContext.CreatePrngStream("edition_" + ante);
                        var editionRolls = searchContext.GetNextRandom(ref editionPrng);
                        var threshold = GetEditionThreshold(need.Edition);
                        var editionMask = Vector512.LessThan(editionRolls, Vector512.Create(threshold));
                        var editionMask256 = new VectorMask(MotelyVectorUtils.VectorMaskToIntMask(editionMask));
                        jokerMask &= editionMask256;

                    }
                    
                    return mask & jokerMask;

                case "SoulJoker":
                    var soulPrng = searchContext.CreatePrngStream("soul_joker_" + ante);
                    var soulJokerVec = searchContext.GetNextRandomElement(ref soulPrng, jokerChoices);
                    VectorMask soulJokerMask = VectorEnum256.Equals(soulJokerVec, ParseJoker(need.Value));
                    return mask & soulJokerMask;

                default:
                    Console.WriteLine($"⚠️  Unknown need type: {need.Type}");
                    return mask;
            }
        }

        private static bool ProcessWantsAndScore(ref MotelySingleSearchContext singleCtx, OuijaConfig config)
        {
            int totalScore = 0;
            int naturalNegatives = 0;
            int desiredNegatives = 0;
            var scoreWants = new List<int>();

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

                scoreWants.Add(wantScore);
                totalScore += wantScore;
            }

            // Apply threshold filtering - For simple test, allow any score >= 0
            bool passes = totalScore >= 0; // Changed from GetMinimumScore(config) to 0 for debugging

            // Store the scoring result for this seed
            if (passes)
            {
                string seed = GetCurrentSeed(ref singleCtx);
                var result = new OuijaResult
                {
                    Seed = seed,
                    TotalScore = (ushort)Math.Max(0, totalScore),
                    NaturalNegativeJokers = (byte)naturalNegatives,
                    DesiredNegativeJokers = (byte)desiredNegatives,
                    ScoreWants = new byte[32], // Initialize with zeros
                    Success = true
                };
                
                // Copy want scores to the result array
                for (int i = 0; i < Math.Min(scoreWants.Count, 32); i++)
                {
                    result.ScoreWants[i] = (byte)Math.Max(0, Math.Min(255, scoreWants[i]));
                }
                
                _threadLocalResults.Value![seed] = result;
            }

            return passes;
        }
        
        private static unsafe string GetCurrentSeed(ref MotelySingleSearchContext singleCtx)
        {
            // Reconstruct the seed from the search context
            // The seed consists of the last characters (fixed part) + the first character (from vector lane)
            string seed = "";
            
            // Add the last characters (positions 1-7)
            for (int i = 0; i < singleCtx.SeedLength - 1; i++)
            {
                if (singleCtx.SeedLastCharacters[i] != '\0')
                    seed += singleCtx.SeedLastCharacters[i];
            }
            
            // Add the first character from the vector lane
            char firstChar = (char)singleCtx.SeedFirstCharacter[singleCtx.VectorLane];
            seed = firstChar + seed;
            
            return seed;
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessWantForAnte(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante, MotelyJoker[] jokerChoices)
        {
            switch (want.Type)
            {
                case "SmallBlindTag":
                    var tagStream = singleCtx.CreateTagStream(ante);
                    var tag = singleCtx.GetNextTag(ref tagStream);
                    return tag == ParseTag(want.Value) ? (1, false, false) : (0, false, false);

                case "BigBlindTag":
                    var bigTagStream = singleCtx.CreateTagStream(ante);
                    singleCtx.GetNextTag(ref bigTagStream); // Skip small blind
                    var bigTag = singleCtx.GetNextTag(ref bigTagStream); // Get big blind
                    return bigTag == ParseTag(want.Value) ? (1, false, false) : (0, false, false);

                case "Joker":
                    return ProcessJokerWant(ref singleCtx, want, ante, jokerChoices, false);

                case "SoulJoker":
                    return ProcessJokerWant(ref singleCtx, want, ante, jokerChoices, true);

                case "Standard_Card":
                    return ProcessCardWant(ref singleCtx, want, ante);

                default:
                    Console.WriteLine($"⚠️  Unknown want type: {want.Type}");
                    return (0, false, false);
            }
        }

        private static (int score, bool isNaturalNegative, bool isDesiredNegative) ProcessJokerWant(
            ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire want, int ante, 
            MotelyJoker[] jokerChoices, bool isSoulJoker)
        {
            // Get the joker
            var prng = isSoulJoker 
                ? singleCtx.CreatePrngStream("soul_joker_" + ante)
                : singleCtx.CreatePrngStream(MotelyPrngKeys.JokerSoul + ante);
            
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
                
                return hasEdition ? (1, isNaturalNeg, isDesiredNeg) : (0, false, false);
            }

            return (1, false, false);
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

            return (1, false, false);
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
        private static MotelyTag ParseTag(string value)
        {
            return Enum.TryParse<MotelyTag>(value, true, out var tag) ? tag : default;
        }

        private static MotelyJoker ParseJoker(string value)
        {
            return Enum.TryParse<MotelyJoker>(value, true, out var joker) ? joker : default;
        }

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
