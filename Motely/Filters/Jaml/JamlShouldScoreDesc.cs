using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters.Jaml;

/// <summary>
/// Per-seed scoring pass: <c>must</c> clauses are re-evaluated precisely (SIMD is coarse),
/// then <c>should</c> clauses contribute score and CSV tallies.
/// </summary>
public struct JamlShouldScoreDesc
    : IMotelySeedScoreDesc<JamlShouldScoreDesc.JamlShouldScoreProvider>
{
    private readonly IJamlClause[] _mustClauses;
    private readonly IJamlClause[] _shouldClauses;
    private readonly Action<string>? _seedMatchCallback;
    private readonly int _minimumTotalScore;

    public JamlShouldScoreDesc(
        IJamlClause[] mustClauses,
        IJamlClause[] shouldClauses,
        Action<string>? seedMatchCallback = null,
        int minimumTotalScore = 0
    )
    {
        _mustClauses = mustClauses;
        _shouldClauses = shouldClauses;
        _seedMatchCallback = seedMatchCallback;
        _minimumTotalScore = minimumTotalScore;
    }

    public JamlShouldScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx) =>
        new(
            _mustClauses,
            _shouldClauses,
            _seedMatchCallback ?? ctx.SeedMatchCallback,
            _minimumTotalScore
        );

    public struct JamlShouldScoreProvider : IMotelySeedScoreProvider
    {
        private readonly IJamlClause[] _mustClauses;
        private readonly IJamlClause[] _shouldClauses;
        private readonly Action<string>? _seedMatchCallback;
        private readonly int _minimumTotalScore;

        public JamlShouldScoreProvider(
            IJamlClause[] mustClauses,
            IJamlClause[] shouldClauses,
            Action<string>? seedMatchCallback,
            int minimumTotalScore = 0
        )
        {
            Debug.Assert(
                mustClauses.Length + shouldClauses.Length > 0,
                "Scoring pass requires at least one must or should clause."
            );
            Debug.Assert(
                shouldClauses.Length <= MotelyScoredSeedResult.MAX_TALLY_COUNT,
                $"Should clause count {shouldClauses.Length} exceeds MotelyScoredSeedResult.MAX_TALLY_COUNT ({MotelyScoredSeedResult.MAX_TALLY_COUNT}); fix JAML / builder before search."
            );

            _mustClauses = mustClauses;
            _shouldClauses = shouldClauses;
            _seedMatchCallback = seedMatchCallback;
            _minimumTotalScore = minimumTotalScore;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe VectorMask Score(
            ref MotelyVectorSearchContext searchContext,
            MotelyScoredSeedResult[] buffer,
            VectorMask baseFilterMask,
            int scoreThreshold = 0
        )
        {
            if (baseFilterMask.IsAllFalse())
                return VectorMask.NoBitsSet;

            var mustClauses = _mustClauses;
            var shouldClauses = _shouldClauses;
            var seedMatchCallback = _seedMatchCallback;
            int cutoff = Math.Max(_minimumTotalScore, scoreThreshold);

            return searchContext.SearchIndividualSeeds(
                baseFilterMask,
                (MotelySingleSearchContext singleCtx) =>
                {
                    var runState = new MotelyRunState();
                    JamlScoring.PrepareRunState(
                        ref singleCtx,
                        CombineForPrepareRunState(mustClauses, shouldClauses),
                        runState
                    );

                    int totalScore = 0;
                    ref var tally = ref buffer[singleCtx.VectorLane];
                    tally.Reset(string.Empty);

                    for (int i = 0; i < mustClauses.Length; i++)
                    {
                        int raw = JamlScoring.CountRawOccurrences(
                            ref singleCtx,
                            mustClauses[i],
                            runState
                        );

                        if (raw < mustClauses[i].Min)
                            return 0;
                    }

                    for (int i = 0; i < shouldClauses.Length; i++)
                    {
                        int raw = JamlScoring.CountRawOccurrences(
                            ref singleCtx,
                            shouldClauses[i],
                            runState
                        );
                        int weighted = JamlScoring.CountOccurrences(
                            ref singleCtx,
                            shouldClauses[i],
                            runState
                        );
                        tally.AddTally(raw);
                        totalScore += weighted * shouldClauses[i].Score;
                    }

                    tally.Score = totalScore;

                    bool passedCutoff = totalScore >= cutoff;
                    if (passedCutoff)
                    {
                        // The seed-match channel carries identity only — the bare seed, the
                        // engine's original contract. Scores and tallies travel typed, in the
                        // scored-result channel this tally buffer feeds.
                        char* seedPtr = stackalloc char[MotelyGlobals.MaxSeedLength];
                        int seedLength = singleCtx.GetSeed(seedPtr);
                        string seedStr = new string(seedPtr, 0, seedLength);
                        tally.Seed = seedStr;
                        seedMatchCallback?.Invoke(seedStr);
                    }

                    return (passedCutoff) ? 1 : 0;
                }
            );
        }

        private static IJamlClause[] CombineForPrepareRunState(
            IJamlClause[] mustClauses,
            IJamlClause[] shouldClauses
        )
        {
            if (mustClauses.Length == 0)
                return shouldClauses;
            if (shouldClauses.Length == 0)
                return mustClauses;

            var combined = new IJamlClause[mustClauses.Length + shouldClauses.Length];
            mustClauses.CopyTo(combined, 0);
            shouldClauses.CopyTo(combined, mustClauses.Length);
            return combined;
        }
    }
}
