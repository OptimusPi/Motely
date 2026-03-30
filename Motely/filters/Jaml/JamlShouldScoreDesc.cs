using System.Runtime.CompilerServices;
using System.Text;

namespace Motely.Filters;

public struct JamlShouldScoreDesc
    : IMotelySeedScoreDesc<JamlShouldScoreDesc.JamlShouldScoreProvider>
{
    private readonly IJamlClause[] _shouldClauses;
    private readonly Action<string>? _seedMatchCallback;

    public JamlShouldScoreDesc(IJamlClause[] shouldClauses, Action<string>? seedMatchCallback = null)
    {
        _shouldClauses = shouldClauses;
        _seedMatchCallback = seedMatchCallback;
    }

    public JamlShouldScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx)
        => new(_shouldClauses, _seedMatchCallback ?? ctx.SeedMatchCallback);

    public struct JamlShouldScoreProvider : IMotelySeedScoreProvider
    {
        private readonly IJamlClause[] _shouldClauses;
        private readonly Action<string>? _seedMatchCallback;

        public JamlShouldScoreProvider(IJamlClause[] shouldClauses, Action<string>? seedMatchCallback)
        {
            if (shouldClauses.Length > MotelySeedScoreTally.MAX_TALLY_COUNT)
                throw new InvalidOperationException(
                    $"Too many should clauses: {shouldClauses.Length}. Maximum allowed: {MotelySeedScoreTally.MAX_TALLY_COUNT}"
                );

            _shouldClauses = shouldClauses;
            _seedMatchCallback = seedMatchCallback;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
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
            int cutoff = scoreThreshold;

            return searchContext.SearchIndividualSeeds(
                baseFilterMask,
                (ref MotelySingleSearchContext singleCtx) =>
                {
                    var runState = new MotelyRunState();
                    JamlScoring.PrepareRunState(ref singleCtx, shouldClauses, ref runState);

                    int totalScore = 0;
                    ref var tally = ref buffer[singleCtx.VectorLane];
                    tally.Reset(string.Empty);

                    for (int i = 0; i < shouldClauses.Length; i++)
                    {
                        int count = JamlScoring.CountOccurrences(ref singleCtx, shouldClauses[i], ref runState);
                        tally.AddTally(count);
                        totalScore += count * shouldClauses[i].Score;
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
