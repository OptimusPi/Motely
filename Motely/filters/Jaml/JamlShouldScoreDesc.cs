using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using static Motely.MotelyVectorUtils;

namespace Motely.Filters;

/// <summary>
/// Score provider built from JAML "should" clauses.
/// Also accepts must descs to re-run them in single-seed path for per-seed debug tallies.
/// Must tallies: ANSI colored ■ (green=pass, red=fail).
/// Should tallies: plain int (score weight or 0).
/// </summary>
public struct JamlShouldScoreDesc
    : IMotelySeedScoreDesc<JamlShouldScoreDesc.JamlShouldScoreProvider>
{
    private const string GreenBlock = "\u001b[32m\u25a0\u001b[0m";
    private const string RedBlock = "\u001b[31m\u25a0\u001b[0m";

    private readonly (IMotelySeedFilterDesc desc, string label)[] _mustDescs;
    private readonly (IJamlClause clause, int score, string label)[] _shouldClauses;
    private readonly Action<string>? _seedMatchCallback;

    public JamlShouldScoreDesc(
        (IMotelySeedFilterDesc desc, string label)[] mustDescs,
        (IJamlClause clause, int score, string label)[] shouldClauses,
        Action<string>? seedMatchCallback = null
    )
    {
        _mustDescs = mustDescs;
        _shouldClauses = shouldClauses;
        _seedMatchCallback = seedMatchCallback;
    }

    public JamlShouldScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx)
    {
        var mustFilters = new IMotelySeedFilter[_mustDescs.Length];
        for (int i = 0; i < _mustDescs.Length; i++)
            mustFilters[i] = _mustDescs[i].desc.CreateFilter(ref ctx);

        var shouldCompiled = new (IJamlClause clause, int score, MotelyItemType[]? targetTypes)[_shouldClauses.Length];
        for (int i = 0; i < _shouldClauses.Length; i++)
        {
            var clause = _shouldClauses[i].clause;
            MotelyItemType[]? targetTypes = null;
            
            if (clause is TarotCardClause t)
            {
                targetTypes = new MotelyItemType[t.Tarots.Length];
                for (int j = 0; j < t.Tarots.Length; j++)
                    targetTypes[j] = (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)t.Tarots[j]);
            }
            else if (clause is JokerClause j)
            {
                targetTypes = new MotelyItemType[j.Jokers.Length];
                for (int k = 0; k < j.Jokers.Length; k++)
                    targetTypes[k] = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)j.Jokers[k]);
            }
            else if (clause is PlanetCardClause p)
            {
                targetTypes = new MotelyItemType[p.Planets.Length];
                for (int k = 0; k < p.Planets.Length; k++)
                    targetTypes[k] = (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)p.Planets[k]);
            }
            else if (clause is SpectralCardClause s)
            {
                targetTypes = new MotelyItemType[s.Spectrals.Length];
                for (int k = 0; k < s.Spectrals.Length; k++)
                    targetTypes[k] = (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)s.Spectrals[k]);
            }

            shouldCompiled[i] = (clause, _shouldClauses[i].score, targetTypes);
        }

        return new JamlShouldScoreProvider(
            mustFilters,
            shouldCompiled,
            _seedMatchCallback ?? ctx.SeedMatchCallback
        );
    }

    public struct JamlShouldScoreProvider : IMotelySeedScoreProvider
    {
        private readonly IMotelySeedFilter[] _mustFilters;
        private readonly (IJamlClause clause, int score, MotelyItemType[]? targetTypes)[] _shouldClauses;
        private readonly Action<string>? _seedMatchCallback;

        public JamlShouldScoreProvider(
            IMotelySeedFilter[] mustFilters,
            (IJamlClause clause, int score, MotelyItemType[]? targetTypes)[] shouldClauses,
            Action<string>? seedMatchCallback
        )
        {
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
            if (baseFilterMask.IsAllFalse())
                return baseFilterMask;

            int shouldCount = _shouldClauses.Length;
            char* seedStrBuffer = stackalloc char[MotelyCore.MaxSeedLength];

            // If we have a callback, pre-calculate MUST filter results across the vector
            VectorMask[]? mustMasks = null;
            if (_seedMatchCallback != null && _mustFilters.Length > 0)
            {
                mustMasks = new VectorMask[_mustFilters.Length];
                for (int m = 0; m < _mustFilters.Length; m++)
                {
                    mustMasks[m] = _mustFilters[m].Filter(ref searchContext);
                }
            }

            var provider = this; // copy struct to avoid issues in delegate if captured

            searchContext.SearchIndividualSeeds(baseFilterMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                int lane = singleCtx.VectorLane;
                int laneScore = 0;
                var runState = new MotelyRunState();
                
                string? seedStr = null;
                if (provider._seedMatchCallback != null)
                {
                    int length = singleCtx.GetSeed(seedStrBuffer);
                    seedStr = new Span<char>(seedStrBuffer, length).ToString();
                }

                var tally = new MotelySeedScoreTally(seedStr ?? "", 0);

                for (int i = 0; i < shouldCount; i++)
                {
                    var c = provider._shouldClauses[i];
                    int count = 0;
                    
                    switch (c.clause)
                    {
                        case TarotCardClause t:
                            count = JamlScoring.CountTarotCardOccurrences(ref singleCtx, t, c.targetTypes!, ref runState);
                            break;
                        case JokerClause j:
                            count = JamlScoring.CountJokerOccurrences(ref singleCtx, j, c.targetTypes!, ref runState);
                            break;
                        case PlanetCardClause p:
                            count = JamlScoring.CountPlanetCardOccurrences(ref singleCtx, p, c.targetTypes!, ref runState);
                            break;
                        case SpectralCardClause s:
                            count = JamlScoring.CountSpectralCardOccurrences(ref singleCtx, s, c.targetTypes!, ref runState);
                            break;
                        case VoucherClause v:
                            count = JamlScoring.CountVoucherOccurrences(ref singleCtx, v, ref runState);
                            break;
                        case StandardCardClause st:
                            count = JamlScoring.CountStandardCardOccurrences(ref singleCtx, st, ref runState);
                            break;
                        case BossClause b:
                            if (runState.CachedBosses == null)
                            {
                                int maxBossAnte = JamlScoring.ArrayMax(b.Antes);
                                if (maxBossAnte > 0)
                                {
                                    var cachedBosses = new MotelyBossBlind[maxBossAnte + 1];
                                    var bossStream = singleCtx.CreateBossStream();
                                    var bossState = new MotelyRunState();
                                    for (int ante = 0; ante <= maxBossAnte; ante++)
                                    {
                                        cachedBosses[ante] = singleCtx.GetBossForAnte(ref bossStream, ante, ref bossState);
                                    }
                                    runState.CachedBosses = cachedBosses;
                                }
                            }
                            count = JamlScoring.CountBossOccurrences(ref singleCtx, b, ref runState);
                            break;
                    }
                    
                    int clauseScore = count * c.score;
                    laneScore += clauseScore;
                    tally.AddTally(clauseScore);
                }
                
                tally.Score = laneScore;
                buffer[lane] = tally;

                if (provider._seedMatchCallback != null)
                {
                    int mustCount = provider._mustFilters.Length;
                    var sb = new System.Text.StringBuilder(
                        seedStr!.Length + 8 + mustCount * 4 + shouldCount * 2
                    );
                    sb.Append(seedStr);
                    sb.Append(',');
                    sb.Append(laneScore);

                    for (int m = 0; m < mustCount; m++)
                    {
                        sb.Append(',');
                        sb.Append(mustMasks![m][lane] ? GreenBlock : RedBlock);
                    }

                    for (int c = 0; c < shouldCount; c++)
                    {
                        sb.Append(',');
                        sb.Append(tally.GetTally(c));
                    }

                    provider._seedMatchCallback(sb.ToString());
                }

                return false; // return of delegate isn't strictly used for early-exit boolean unless using conditional searcher pattern, continuing search
            });

            return baseFilterMask;
        }
    }
}
