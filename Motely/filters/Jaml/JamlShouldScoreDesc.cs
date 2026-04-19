using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Motely.Filters;

public struct JamlShouldScoreDesc
    : IMotelySeedScoreDesc<JamlShouldScoreDesc.JamlShouldScoreProvider>
{
    private readonly IJamlClause[] _shouldClauses;
    private readonly Action<string>? _seedMatchCallback;
    private readonly int _minimumTotalScore;
    private readonly int _mustClauseCount;

    public JamlShouldScoreDesc(
        IJamlClause[] shouldClauses,
        Action<string>? seedMatchCallback = null,
        int minimumTotalScore = 0,
        int mustClauseCount = 0
    )
    {
        _shouldClauses = shouldClauses;
        _seedMatchCallback = seedMatchCallback;
        _minimumTotalScore = minimumTotalScore;
        _mustClauseCount = mustClauseCount;
    }

    public JamlShouldScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx)
        => new(
            _shouldClauses,
            _seedMatchCallback ?? ctx.SeedMatchCallback,
            _minimumTotalScore,
            _mustClauseCount
        );

    public struct JamlShouldScoreProvider : IMotelySeedScoreProvider
    {
        private readonly IJamlClause[] _shouldClauses;
        private readonly Action<string>? _seedMatchCallback;
        private readonly int _minimumTotalScore;
        private readonly int _mustClauseCount;

        public JamlShouldScoreProvider(
            IJamlClause[] shouldClauses,
            Action<string>? seedMatchCallback,
            int minimumTotalScore = 0,
            int mustClauseCount = 0
        )
        {
            Debug.Assert(
                shouldClauses.Length <= MotelySeedScoreTally.MAX_TALLY_COUNT,
                $"Should clause count {shouldClauses.Length} exceeds MotelySeedScoreTally.MAX_TALLY_COUNT ({MotelySeedScoreTally.MAX_TALLY_COUNT}); fix JAML / builder before search."
            );
            Debug.Assert(
                mustClauseCount <= shouldClauses.Length,
                $"mustClauseCount ({mustClauseCount}) exceeds shouldClauses.Length ({shouldClauses.Length}); CreatePlan wiring bug."
            );

            _shouldClauses = shouldClauses;
            _seedMatchCallback = seedMatchCallback;
            _minimumTotalScore = minimumTotalScore;
            _mustClauseCount = mustClauseCount;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining
        )]
        public unsafe VectorMask Score(
            ref MotelyVectorSearchContext searchContext,
            MotelySeedScoreTally[] buffer,
            VectorMask baseFilterMask,
            int scoreThreshold = 0
        )
        {
            if (baseFilterMask.IsAllFalse())
                return VectorMask.NoBitsSet;

            var shouldClauses = _shouldClauses;
            var seedMatchCallback = _seedMatchCallback;
            int cutoff = Math.Max(_minimumTotalScore, scoreThreshold);
            int mustCount = _mustClauseCount;

            return searchContext.SearchIndividualSeeds(
                baseFilterMask,
                (ref MotelySingleSearchContext singleCtx) =>
                {
                    var runState = new MotelyRunState();
                    JamlScoring.PrepareRunState(ref singleCtx, shouldClauses, ref runState);

                    int totalScore = 0;
                    ref var tally = ref buffer[singleCtx.VectorLane];
                    tally.Reset(string.Empty);

                    // Must clauses first — early-exit if any fails
                    for (int i = 0; i < mustCount; i++)
                    {
                        int raw = JamlScoring.CountRawOccurrences(ref singleCtx, shouldClauses[i], ref runState);
                        int weighted = JamlScoring.CountOccurrences(ref singleCtx, shouldClauses[i], ref runState);
                        tally.AddTally(raw);
                        totalScore += weighted * shouldClauses[i].Score;

                        if (raw < shouldClauses[i].Min)
                            return false;
                    }

                    // Should clauses — score but never reject
                    for (int i = mustCount; i < shouldClauses.Length; i++)
                    {
                        int raw = JamlScoring.CountRawOccurrences(ref singleCtx, shouldClauses[i], ref runState);
                        int weighted = JamlScoring.CountOccurrences(ref singleCtx, shouldClauses[i], ref runState);
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
    }
}
