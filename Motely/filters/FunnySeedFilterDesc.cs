using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics;
using Motely;

namespace Motely.Filters;

/// <summary>
/// Searches for seeds with funny words, patterns, or interesting properties
/// Used with --funny flag to find entertaining seeds like "TACOTACO" or "11111111"
/// </summary>
public struct FunnySeedFilterDesc : IMotelySeedFilterDesc<FunnySeedFilterDesc.FunnySeedFilter>
{
    public static System.Collections.Concurrent.ConcurrentBag<string> FoundSeeds = new();
    public static List<string>? CustomKeywords { get; set; }
    public static bool CollectOnly { get; set; } = false; // When true, collect seeds but don't report them
    
    public FunnySeedFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // No caching needed - we're just checking the seed hash itself
        DebugLogger.Log("[FunnySeedFilter] CreateFilter called! CustomKeywords = " + 
            (CustomKeywords == null ? "null" : string.Join(", ", CustomKeywords)));
        return new FunnySeedFilter();
    }

    public struct FunnySeedFilter : IMotelySeedFilter
    {
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            DebugLogger.Log("[FunnySeedFilter] Filter called!");
            // Use SearchIndividualSeeds which returns a VectorMask directly
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                // Check if the seed is funny
                var seed = singleCtx.GetSeed();
                bool isFunny = CheckSeedStatic(seed);
                
                if (isFunny)
                {
                    DebugLogger.Log($"[FunnySeedFilter] Found funny seed: {seed}");
                }
                
                // In CollectOnly mode, collect but don't report
                if (CollectOnly && isFunny)
                {
                    return false; // Don't report to search infrastructure
                }
                
                return isFunny;
            });
        }
        
        static bool CheckSeedStatic(string seed)
        {
            // Use custom keywords if provided, otherwise use default funny words
            var funnyWords = CustomKeywords ?? new List<string> { 
                "TACO", "PIES"
            };

            // Check for funny words! (case insensitive)
            foreach (var funnyWord in funnyWords)
            {
                if (seed.Contains(funnyWord, StringComparison.OrdinalIgnoreCase))
                {
                    // Log the funny seed we found (commented out to reduce spam)
                    DebugLogger.Log($"😂 Found funny seed: {seed} (contains {funnyWord})");
                    FunnySeedFilterDesc.FoundSeeds.Add(seed);
                    return true;
                }
            }

            return false;
        }
        
        public bool CheckSeed(ref MotelySingleSearchContext ctx)
        {
            var seed = ctx.GetSeed();
            return CheckSeedStatic(seed);
        }
    }
}