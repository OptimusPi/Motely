using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Motely.Filters.Jaml;

/// <summary>
/// Per-seed scoring pass: <c>must</c> clauses are re-evaluated precisely (SIMD is coarse),
/// then <c>should</c> clauses contribute score and CSV tallies.
/// </summary>
public struct JamlShouldScoreDesc
    : IMotelySeedScoreDesc<JamlShouldScoreDesc.JamlShouldScoreProvider>
{
    private readonly JamlClauseBase[] _mustClauses;
    private readonly JamlClauseBase[] _shouldClauses;
    private readonly Action<string>? _seedMatchCallback;
    private readonly int _minimumTotalScore;

    public JamlShouldScoreDesc(
        JamlClauseBase[] mustClauses,
        JamlClauseBase[] shouldClauses,
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
        private readonly JamlClauseBase[] _mustClauses;
        private readonly JamlClauseBase[] _shouldClauses;
        private readonly Action<string>? _seedMatchCallback;
        private readonly int _minimumTotalScore;

        public JamlShouldScoreProvider(
            JamlClauseBase[] mustClauses,
            JamlClauseBase[] shouldClauses,
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
                (ref MotelySingleSearchContext singleCtx) =>
                {
                    var runState = new MotelyRunState();
                    JamlScoring.PrepareRunState(
                        ref singleCtx,
                        CombineForPrepareRunState(mustClauses, shouldClauses),
                        ref runState
                    );

                    int totalScore = 0;
                    ref var tally = ref buffer[singleCtx.VectorLane];
                    tally.Reset(string.Empty);

                    for (int i = 0; i < mustClauses.Length; i++)
                    {
                        int raw = JamlScoring.CountRawOccurrences(
                            ref singleCtx,
                            mustClauses[i],
                            ref runState
                        );

                        if (raw < mustClauses[i].Min)
                            return false;
                    }

                    for (int i = 0; i < shouldClauses.Length; i++)
                    {
                        int raw = JamlScoring.CountRawOccurrences(
                            ref singleCtx,
                            shouldClauses[i],
                            ref runState
                        );
                        int weighted = JamlScoring.CountOccurrences(
                            ref singleCtx,
                            shouldClauses[i],
                            ref runState
                        );
                        tally.AddTally(raw);
                        totalScore += weighted * shouldClauses[i].Score;
                    }

                    tally.Score = totalScore;

                    bool passedCutoff = totalScore >= cutoff;
                    if (passedCutoff && seedMatchCallback != null)
                    {
                        char* seedPtr = stackalloc char[MotelyGlobals.MaxSeedLength];
                        int seedLength = singleCtx.GetSeed(seedPtr);
                        string seedStr = new string(seedPtr, 0, seedLength);
                        tally.Seed = seedStr;

                        var sb = new StringBuilder(seedStr.Length + 16 + shouldClauses.Length * 4);
                        sb.Append(seedStr);
                        sb.Append(',');
                        sb.Append(totalScore);
                        for (int i = 0; i < tally.TallyCount; i++)
                        {
                            sb.Append(',');
                            sb.Append(tally.GetTally(i));
                        }

                        seedMatchCallback(sb.ToString());
                    }
                    else if (passedCutoff)
                    {
                        char* seedPtr = stackalloc char[MotelyGlobals.MaxSeedLength];
                        int seedLength = singleCtx.GetSeed(seedPtr);
                        tally.Seed = new string(seedPtr, 0, seedLength);
                    }

                    return passedCutoff;
                }
            );
        }

        private static JamlClauseBase[] CombineForPrepareRunState(
            JamlClauseBase[] mustClauses,
            JamlClauseBase[] shouldClauses
        )
        {
            if (mustClauses.Length == 0)
                return shouldClauses;
            if (shouldClauses.Length == 0)
                return mustClauses;

            var combined = new JamlClauseBase[mustClauses.Length + shouldClauses.Length];
            mustClauses.CopyTo(combined, 0);
            shouldClauses.CopyTo(combined, mustClauses.Length);
            return combined;
        }
    }
}
