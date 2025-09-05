using System;
using System.Runtime.CompilerServices;

namespace Motely.Filters;

public struct NegativeCopyJokersScore : IMotelySeedScore
{
    public int Score { get; }
    public string Seed { get; }
    
    // Individual tallies for CSV columns
    public int ShowmanCount { get; }
    public int BlueprintCount { get; }
    public int BrainstormCount { get; }
    public int InvisibleCount { get; }
    public int NegativeShowmanCount { get; }
    public int NegativeBlueprintCount { get; }
    public int NegativeBrainstormCount { get; }
    public int NegativeInvisibleCount { get; }
    
    public NegativeCopyJokersScore(string seed, int score, 
        int showman, int blueprint, int brainstorm, int invisible,
        int negShowman, int negBlueprint, int negBrainstorm, int negInvisible)
    {
        Seed = seed;
        Score = score;
        ShowmanCount = showman;
        BlueprintCount = blueprint;
        BrainstormCount = brainstorm;
        InvisibleCount = invisible;
        NegativeShowmanCount = negShowman;
        NegativeBlueprintCount = negBlueprint;
        NegativeBrainstormCount = negBrainstorm;
        NegativeInvisibleCount = negInvisible;
    }
}

public struct NegativeCopyJokersScoreDesc(
    int cutoff,
    bool autoCutoff,
    Action<NegativeCopyJokersScore> onResultFound)
    : IMotelySeedScoreDesc<NegativeCopyJokersScoreDesc.NegativeCopyJokersScoreProvider>
{
    private static int _learnedCutoff = 1;
    
    public NegativeCopyJokersScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx)
    {
        // Cache streams for scoring
        for (int ante = 1; ante <= 8; ante++)
        {
            ctx.CacheBoosterPackStream(ante);
            ctx.CacheShopJokerStream(ante);
        }
        
        return new NegativeCopyJokersScoreProvider(cutoff, autoCutoff, onResultFound);
    }
    
    public struct NegativeCopyJokersScoreProvider : IMotelySeedScoreProvider
    {
        private readonly int _cutoff;
        private readonly bool _autoCutoff;
        public NegativeCopyJokersScoreProvider(int cutoff, bool autoCutoff, Action<NegativeCopyJokersScore> onResultFound)
        {
            _cutoff = cutoff;
            _autoCutoff = autoCutoff;
            // onResultFound no longer needed - we use buffer directly!
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly VectorMask Score(ref MotelyVectorSearchContext searchContext, MotelySeedScoreTally[] buffer, VectorMask baseFilterMask = default, int scoreThreshold = 0)
        {
            var cutoff = scoreThreshold > 0 ? scoreThreshold : _cutoff;
            var autoCutoff = _autoCutoff;
            
            // Caller MUST provide a valid mask with at least some bits set
            System.Diagnostics.Debug.Assert(baseFilterMask.IsPartiallyTrue(), 
                "Score() called with empty mask - this is a bug in the calling code!");
            
            // Process each seed that already passed the base filter
            return searchContext.SearchIndividualSeeds(baseFilterMask, (ref MotelySingleSearchContext ctx) =>
            {
                int blueprintCount = 0;
                int brainstormCount = 0;
                int invisibleCount = 0;
                int showmanCount = 0;
                int negativeShowmanCount = 0;
                int negativeBlueprint = 0;
                int negativeBrainstorm = 0;
                int negativeInvisible = 0;
                
                // Check all 8 antes thoroughly
                for (int ante = 1; ante <= 8; ante++)
                {
                    // Check shop items
                    var shopStream = ctx.CreateShopItemStream(ante, 
                        MotelyShopStreamFlags.ExcludeTarots | MotelyShopStreamFlags.ExcludePlanets,
                        MotelyJokerStreamFlags.Default);
                    
                    int shopSlots = ante switch
                    {
                        1 => 4,
                        2 => 12,
                        3 => 25,
                        4 => 35,
                        5 => 45,
                        6 => 55,
                        7 => 65,
                        8 => 75,
                        _ => 25
                    };
                    
                    for (int i = 0; i < shopSlots; i++)
                    {
                        var shopItem = ctx.GetNextShopItem(ref shopStream);
                        
                        if (shopItem.Type == MotelyItemType.Showman)
                        {
                            if (shopItem.Edition == MotelyItemEdition.Negative)
                                negativeShowmanCount++;
                            else
                                showmanCount++;
                        }
                        else if (shopItem.Type == MotelyItemType.Blueprint)
                        {
                            if (shopItem.Edition == MotelyItemEdition.Negative)
                                negativeBlueprint++;
                            else
                                blueprintCount++;
                        }
                        else if (shopItem.Type == MotelyItemType.Brainstorm)
                        {
                            if (shopItem.Edition == MotelyItemEdition.Negative)
                                negativeBrainstorm++;
                            else
                                brainstormCount++;
                        }
                        else if (shopItem.Type == MotelyItemType.InvisibleJoker)
                        {
                            if (shopItem.Edition == MotelyItemEdition.Negative)
                                negativeInvisible++;
                            else
                                invisibleCount++;
                        }
                    }
                    
                    // Check buffoon packs
                    var boosterPackStream = ctx.CreateBoosterPackStream(ante, ante > 1, false);
                    var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
                    
                    int maxPackSlots = ante == 1 ? 4 : 6;
                    for (int i = 0; i < maxPackSlots; i++)
                    {
                        var pack = ctx.GetNextBoosterPack(ref boosterPackStream);
                        
                        if (pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                        {
                            var contents = ctx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize());
                            
                            for (int j = 0; j < contents.Length; j++)
                            {
                                var item = contents[j];

                                if (item.Type == MotelyItemType.Showman)
                                {
                                    if (item.Edition == MotelyItemEdition.Negative)
                                        negativeShowmanCount++;
                                    else
                                        showmanCount++;
                                }
                                else if (item.Type == MotelyItemType.Blueprint)
                                {
                                    if (item.Edition == MotelyItemEdition.Negative)
                                        negativeBlueprint++;
                                    else
                                        blueprintCount++;
                                }
                                else if (item.Type == MotelyItemType.Brainstorm)
                                {
                                    if (item.Edition == MotelyItemEdition.Negative)
                                        negativeBrainstorm++;
                                    else
                                        brainstormCount++;
                                }
                                else if (item.Type == MotelyItemType.InvisibleJoker)
                                {
                                    if (item.Edition == MotelyItemEdition.Negative)
                                        negativeInvisible++;
                                    else
                                        invisibleCount++;
                                }
                            }
                        }
                    }
                }
                
                // Calculate score
                int totalCopyJokers = blueprintCount + brainstormCount + invisibleCount;
                int totalNegatives = negativeBlueprint + negativeBrainstorm + negativeInvisible + negativeShowmanCount;
                int showmanScore = Math.Min(showmanCount, 1);
                int endScore = Math.Min(totalCopyJokers, 6) + totalNegatives;
                
                // Apply cutoff
                int currentCutoff = GetCurrentCutoff(endScore, autoCutoff, cutoff);
                if (endScore < currentCutoff)
                    return true; // Still return true to continue processing other seeds
                
                // Get seed string
                string seedStr;
                unsafe
                {
                    char* seedPtr = stackalloc char[9];
                    int length = ctx.GetSeed(seedPtr);
                    seedStr = new string(seedPtr, 0, length);
                }
                
                // Create score object and notify
                // Convert to MotelySeedScoreTally for buffer compatibility
                var tallies = new List<int> { 
                    showmanCount, blueprintCount, brainstormCount, invisibleCount,
                    negativeShowmanCount, negativeBlueprint, negativeBrainstorm, negativeInvisible 
                };
                
                // Write to buffer at current lane position - NO CALLBACK!
                buffer[ctx.VectorLane] = new MotelySeedScoreTally(seedStr, endScore, tallies);
                
                return true; // This seed passed
            });
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetCurrentCutoff(int currentScore, bool autoCutoff, int cutoff)
        {
            if (!autoCutoff)
                return cutoff;
                
            // Auto-adjust cutoff based on scores found
            if (currentScore > _learnedCutoff)
                _learnedCutoff = currentScore;
                
            return Math.Max(cutoff, _learnedCutoff);
        }
    }
}