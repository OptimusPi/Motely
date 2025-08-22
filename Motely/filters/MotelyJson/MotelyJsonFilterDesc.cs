using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;
using System.Numerics;
namespace Motely.Filters;

/// <summary>
/// Clean filter descriptor for MongoDB-style queries
/// </summary>
public struct MotelyJsonFinalTallyScoresDescDesc(MotelyJsonConfig Config, int Cutoff, bool AutoCutoff, Action<string, int, int[]> OnResultFound) : IMotelySeedFilterDesc<MotelyJsonFinalTallyScoresDescDesc.MotelyJsonFinalTallyScoresDesc>
{
    public int Cutoff { get => cutoff; set => cutoff = value; }
    public bool AutoCutoff { get; set; } = false;

    // Auto cutoff state
    private static int _learnedCutoff = 1;

    // Results tracking for rarity calculation
    private static long _resultsFound = 0;
    private global::System.Int32 cutoff = 1;

    public static long ResultsFound => _resultsFound;

    public MotelyJsonFinalTallyScoresDesc CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Reset for new search
        _learnedCutoff = Cutoff;
        _resultsFound = 0;

        return new MotelyJsonFinalTallyScoresDesc(_config, Cutoff, AutoCutoff, _onResultFound);
    }

    public struct MotelyJsonFinalTallyScoresDesc(MotelyJsonConfig config, int cutoff, bool autoCutoff, Action<string, int, int[]> OnResultFound)
        : IMotelySeedFilter
    {
        public static bool IsCancelled;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            if (IsCancelled)
                return VectorMask.NoBitsSet;

            // Copy fields to local variables to avoid struct closure issues
            var config = _config;
            var cutoff = _cutoff;
            var autoCutoff = _autoCutoff;
            
            return searchContext.SearchIndividualSeeds(VectorMask.AllBitsSet, (ref MotelySingleSearchContext singleCtx) =>
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

                    bool clauseResult = CheckSingleClause(ref singleCtx, clause, ref runState);

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
                        if (CheckSingleClause(ref singleCtx, clause, ref runState))
                            return false;
                    }
                }

                // Calculate scores for SHOULD clauses
                int totalScore = 1; // Base score for passing MUST
                var scores = new List<int>();

                if (config.Should?.Count > 0)
                {
                    foreach (var should in config.Should)
                    {
                        int count = CountOccurrences(ref singleCtx, should, ref runState);
                        int score = count * should.Score;
                        scores.Add(count);
                        totalScore += score;
                    }
                }

                // Auto cutoff logic
                var currentCutoff = autoCutoff ? GetCurrentCutoff(totalScore) : cutoff;

                // Only return true if score meets threshold
                if (totalScore >= currentCutoff)
                {
                    // Track results for rarity calculation
                    Interlocked.Increment(ref _resultsFound);

                    // Use callback for CSV formatting (moved out of hot path)
                    if (onResultFound != null)
                    {
                        unsafe
                        {
                            char* seedPtr = stackalloc char[9];
                            int len = singleCtx.GetSeed(seedPtr);
                            string seedStr = new string(seedPtr, 0, len);
                            onResultFound(seedStr, totalScore, scores.ToArray());
                        }
                        return false; // Callback handled output, don't print seed again
                    }

                    return true; // Let framework handle seed output if no callback
                }

                return false;
            });
        }

        private static int GetCurrentCutoff(int currentScore)
        {
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