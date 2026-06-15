using System.Runtime.CompilerServices;
using System.Text;
using Motely.Analysis;

namespace Motely.Filters.Jaml;

/// <summary>
/// Score provider that runs JAML must/should scoring then immediately calls
/// <see cref="JamlyzerFilterDesc.JamlyzerFilter.CheckSeed"/> on the same open
/// <see cref="MotelySingleSearchContext"/> for each seed that passes the cutoff.
/// This collapses the 2N interop pattern (OnScoredResult + separate Jamlyze round-trip)
/// into a single N-call inline walk — no nested search spawned.
/// </summary>
public struct JamlyzerScoreDesc
    : IMotelySeedScoreDesc<JamlyzerScoreDesc.JamlyzerScoreProvider>
{
    private readonly IJamlClause[] _mustClauses;
    private readonly IJamlClause[] _shouldClauses;
    private readonly Action<string>? _seedMatchCallback;
    private readonly int _minimumTotalScore;
    private readonly Action<JamlyzerSnapshot>? _onJamlyzerResult;

    public JamlyzerScoreDesc(
        IJamlClause[] mustClauses,
        IJamlClause[] shouldClauses,
        Action<string>? seedMatchCallback = null,
        int minimumTotalScore = 0,
        Action<JamlyzerSnapshot>? onJamlyzerResult = null
    )
    {
        _mustClauses = mustClauses;
        _shouldClauses = shouldClauses;
        _seedMatchCallback = seedMatchCallback;
        _minimumTotalScore = minimumTotalScore;
        _onJamlyzerResult = onJamlyzerResult;
    }

    public JamlyzerScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx) =>
        new(
            _mustClauses,
            _shouldClauses,
            _seedMatchCallback ?? ctx.SeedMatchCallback,
            _minimumTotalScore,
            _onJamlyzerResult
        );

    public struct JamlyzerScoreProvider : IMotelySeedScoreProvider
    {
        private readonly IJamlClause[] _mustClauses;
        private readonly IJamlClause[] _shouldClauses;
        private readonly Action<string>? _seedMatchCallback;
        private readonly int _minimumTotalScore;
        private readonly Action<JamlyzerSnapshot>? _onJamlyzerResult;

        public JamlyzerScoreProvider(
            IJamlClause[] mustClauses,
            IJamlClause[] shouldClauses,
            Action<string>? seedMatchCallback,
            int minimumTotalScore,
            Action<JamlyzerSnapshot>? onJamlyzerResult
        )
        {
            _mustClauses = mustClauses;
            _shouldClauses = shouldClauses;
            _seedMatchCallback = seedMatchCallback;
            _minimumTotalScore = minimumTotalScore;
            _onJamlyzerResult = onJamlyzerResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe VectorMask Score(
            ref MotelyVectorSearchContext searchContext,
            MotelySeedScoreTally[] buffer,
            VectorMask baseFilterMask,
            int scoreThreshold = 0
        )
        {
            if (baseFilterMask.IsAllFalse())
                return VectorMask.NoBitsSet;

            var mustClauses = _mustClauses;
            var shouldClauses = _shouldClauses;
            var seedMatchCallback = _seedMatchCallback;
            var onJamlyzerResult = _onJamlyzerResult;
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
                    if (passedCutoff)
                    {
                        char* seedPtr = stackalloc char[MotelyGlobals.MaxSeedLength];
                        int seedLength = singleCtx.GetSeed(seedPtr);
                        string seedStr = new string(seedPtr, 0, seedLength);
                        tally.Seed = seedStr;

                        if (seedMatchCallback != null)
                        {
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

                        if (onJamlyzerResult != null)
                        {
                            // Walk every stream inline on the already-open singleCtx —
                            // no nested search spawned. Stream creation is keyed (seed + stream-type
                            // + ante), so this call is independent of the scoring-pass streams above.
                            var filterDesc = new JamlyzerFilterDesc();
                            new JamlyzerFilterDesc.JamlyzerFilter(filterDesc).CheckSeed(ref singleCtx);
                            onJamlyzerResult(filterDesc.LastSnapshot!);
                        }
                    }

                    return passedCutoff;
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
