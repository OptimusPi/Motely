using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;
using System.Numerics;

namespace Motely.Filters;


public struct MotelySeedScoreTally : IMotelySeedScore
{
    public int Score { get; }
    public List<int> TallyColumns { get; }
    public string Seed { get; }

    public MotelySeedScoreTally(string seed, int score, List<int> tallyColumns)
    {
        Seed = seed;
        Score = score;
        TallyColumns = tallyColumns;
    }
}   

/// <summary>
/// Clean filter descriptor for MongoDB-style queries
/// </summary>
public struct MotelyJsonSeedScoreDesc(
    MotelyJsonConfig Config,
    int Cutoff,
    bool AutoCutoff,
    Action<MotelySeedScoreTally> OnResultFound
)
    : IMotelySeedScoreDesc<MotelyJsonSeedScoreDesc.MotelyJsonSeedScoreProvider>
{

    // Auto cutoff state
    private static int _learnedCutoff = 1;
    private global::System.Int32 cutoff = 1;

    // Results tracking for rarity calculation
    private static long _resultsFound = 0;
    public static long ResultsFound => _resultsFound;

    // Callback to return the score object to (the caller can print, send to a db, I don't care)
    private readonly Action<MotelySeedScoreTally> _onResultFound = OnResultFound;

    public MotelyJsonSeedScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx)
    {
        // Reset for new search
        _learnedCutoff = Cutoff;
        _resultsFound = 0;

        return new MotelyJsonSeedScoreProvider(Config, Cutoff, AutoCutoff, _onResultFound);
    }

    public struct MotelyJsonSeedScoreProvider(MotelyJsonConfig Config, int Cutoff, bool AutoCutoff, Action<MotelySeedScoreTally> OnResultFound)
        : IMotelySeedScoreProvider
    {
        public static bool IsCancelled;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Score(ref MotelyVectorSearchContext searchContext)
        {
            if (IsCancelled)
                return;

            // Copy fields to local variables to avoid struct closure issues
            var config = Config;
            var cutoff = Cutoff;
            var autoCutoff = AutoCutoff;
            var onResultFound = OnResultFound;

            searchContext.SearchIndividualSeeds(VectorMask.AllBitsSet, (ref MotelySingleSearchContext singleCtx) =>
            {
                var runState = new MotelyRunState();

                // Activate all vouchers for scoring (using cached property)
                if (config.MaxVoucherAnte > 0)
                {
                    MotelyJsonScoring.ActivateAllVouchers(ref singleCtx, ref runState, config.MaxVoucherAnte);
                }

                foreach (var clause in config.Must)
                {
                    DebugLogger.Log($"[Must] Checking {clause.ItemTypeEnum} {clause.Value} in antes [{string.Join(",", clause.EffectiveAntes ?? new int[0])}]");
                    DebugLogger.Log($"[Must] Showman active: {runState.ShowmanActive}, Owned jokers: {runState.OwnedJokers.Length}");

                    bool clauseResult = MotelyJsonScoring.CheckSingleClause(ref singleCtx, clause, ref runState);

                    DebugLogger.Log($"[Must] Result: {clauseResult} for {clause.ItemTypeEnum} {clause.Value}");

                    if (!clauseResult)
                    {
                        DebugLogger.Log($"[Must] FAILED! Seed filtered out because {clause.ItemTypeEnum} {clause.Value} not found");
                        return false; // Seed doesn't meet requirements
                    }
                }

                // Check all MUST NOT clauses
                if (config.MustNot?.Count > 0)
                {
                    foreach (var clause in config.MustNot)
                    {
                        if (MotelyJsonScoring.CheckSingleClause(ref singleCtx, clause, ref runState))
                            return false;
                    }
                }

                // Calculate scores for SHOULD clauses
                int totalScore = 1;
                var scores = new List<int>();

                if (config.Should?.Count > 0)
                {
                    foreach (var should in config.Should)
                    {
                        int count = MotelyJsonScoring.CountOccurrences(ref singleCtx, should, ref runState);
                        int score = count * should.Score;
                        scores.Add(count);
                        totalScore += score;
                    }
                }

                // Only return true if score meets threshold
                if (totalScore >= GetCurrentCutoff(totalScore, autoCutoff, cutoff))
                {
                    // Track results for rarity calculation
                    Interlocked.Increment(ref _resultsFound);

                    string seedStr;
                    unsafe
                    {
                        char* seedPtr = stackalloc char[9];
                        int length = singleCtx.GetSeed(seedPtr);
                        seedStr = new string(seedPtr, 0, length);
                    }
                    var seedScore = new MotelySeedScoreTally(seedStr, totalScore, scores);
                    onResultFound(seedScore); // RICH CALLBACK!
                    
                    return true; // Tell framework this seed passed
                }

                return false;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetCurrentCutoff(int currentScore, bool autoCutoff, int cutoff)
        {
            if (!autoCutoff)
                return cutoff;

            // Thread-safe auto cutoff: Start at 1, raise to highest score found
            if (currentScore > _learnedCutoff)
            {
                var oldCutoff = Interlocked.Exchange(ref _learnedCutoff, currentScore);
                DebugLogger.Log($"[AutoCutoff] Raised cutoff from {oldCutoff} to {currentScore}");
            }

            return _learnedCutoff;
        }
    }
}