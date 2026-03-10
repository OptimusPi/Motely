using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using static Motely.MotelyVectorUtils;

namespace Motely.Filters;

/// <summary>
/// Score provider built from JAML "should" clauses.
/// Also accepts must descs to re-run them in single-seed path for per-seed debug tallies.
/// Must tallies: 1 (pass) / 0 (fail).
/// Should tallies: plain int (score weight or 0).
/// </summary>
public struct JamlShouldScoreDesc
    : IMotelySeedScoreDesc<JamlShouldScoreDesc.JamlShouldScoreProvider>
{
    private const char PassChar = 'Y';
    private const char FailChar = 'N';

    private readonly (IMotelySeedFilterDesc desc, string label)[] _mustDescs;
    private readonly (IMotelySeedFilterDesc desc, int score, string label)[] _shouldDescs;
    private readonly Action<string>? _seedMatchCallback;

    public JamlShouldScoreDesc(
        (IMotelySeedFilterDesc desc, string label)[] mustDescs,
        (IMotelySeedFilterDesc desc, int score, string label)[] shouldDescs,
        Action<string>? seedMatchCallback = null
    )
    {
        _mustDescs = mustDescs;
        _shouldDescs = shouldDescs;
        _seedMatchCallback = seedMatchCallback;
    }

    public JamlShouldScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx)
    {
        var mustFilters = new IMotelySeedFilter[_mustDescs.Length];
        for (int i = 0; i < _mustDescs.Length; i++)
            mustFilters[i] = _mustDescs[i].desc.CreateFilter(ref ctx);

        var shouldFilters = new (IMotelySeedFilter filter, int score)[_shouldDescs.Length];
        for (int i = 0; i < _shouldDescs.Length; i++)
            shouldFilters[i] = (_shouldDescs[i].desc.CreateFilter(ref ctx), _shouldDescs[i].score);

        return new JamlShouldScoreProvider(
            mustFilters,
            shouldFilters,
            _seedMatchCallback ?? ctx.SeedMatchCallback
        );
    }

    public struct JamlShouldScoreProvider : IMotelySeedScoreProvider
    {
        private readonly IMotelySeedFilter[] _mustFilters;
        private readonly (IMotelySeedFilter filter, int score)[] _shouldClauses;
        private readonly Action<string>? _seedMatchCallback;

        public JamlShouldScoreProvider(
            IMotelySeedFilter[] mustFilters,
            (IMotelySeedFilter filter, int score)[] shouldClauses,
            Action<string>? seedMatchCallback
        )
        {
            Debug.Assert(shouldClauses.Length <= MotelySeedScoreTally.MAX_TALLY_COUNT, 
            $"Too many should clauses: {shouldClauses.Length}. Maximum allowed: {MotelySeedScoreTally.MAX_TALLY_COUNT}");

            _mustFilters = mustFilters;
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
            int shouldCount = _shouldClauses.Length;

            // Per-should-clause match masks + accumulated scores
            Span<VectorMask> shouldMasks = stackalloc VectorMask[shouldCount];
            var scores = Vector256<int>.Zero;

            for (int i = 0; i < shouldCount; i++)
            {
                shouldMasks[i] = _shouldClauses[i].filter.Filter(ref searchContext);
                var scoreVec = Vector256.Create(_shouldClauses[i].score);
                scores = Vector256.Add(
                    scores,
                    Vector256.ConditionalSelect(
                        VectorMaskToConditionalSelectMask(shouldMasks[i]),
                        scoreVec,
                        Vector256<int>.Zero
                    )
                );
            }

            var resultMask = baseFilterMask;
            char* seed = stackalloc char[MotelyCore.MaxSeedLength];

            for (int lane = 0; lane < MotelyCore.MaxVectorWidth; lane++)
            {
                if (!baseFilterMask[lane] || !searchContext.IsLaneValid(lane))
                    continue;

                int laneScore = scores.GetElement(lane);
                int length = searchContext.GetSeed(lane, seed);
                string seedStr = new Span<char>(seed, length).ToString();

                buffer[lane] = new MotelySeedScoreTally(seedStr, laneScore);

                // Store individual should clause results as tallies
                for (int c = 0; c < shouldCount; c++)
                {
                    buffer[lane].AddTally(shouldMasks[c][lane] ? _shouldClauses[c].score : 0);
                }

                if (_seedMatchCallback != null)
                {
                    int mustCount = _mustFilters.Length;
                    var sb = new System.Text.StringBuilder(
                        seedStr.Length + 8 + mustCount * 4 + shouldCount * 2
                    );
                    sb.Append(seedStr);
                    sb.Append(',');
                    sb.Append(laneScore);

                    // Must tallies: re-run each must filter on the same context, check bit lane
                    for (int m = 0; m < mustCount; m++)
                    {
                        VectorMask mustResult = _mustFilters[m].Filter(ref searchContext);
                        sb.Append(',');
                        sb.Append(mustResult[lane] ? PassChar : FailChar);
                    }

                    // Should tallies: plain int
                    for (int c = 0; c < shouldCount; c++)
                    {
                        sb.Append(',');
                        sb.Append(shouldMasks[c][lane] ? _shouldClauses[c].score : 0);
                    }

                    _seedMatchCallback(sb.ToString());
                }
            }

            return resultMask;
        }
    }
}
