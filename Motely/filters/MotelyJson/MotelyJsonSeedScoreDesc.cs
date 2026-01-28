using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Motely.Reporting;

namespace Motely.Filters;

public unsafe struct MotelySeedScoreTally : IMotelySeedScore
{
    public int Score { get; set; } // Made mutable for easier scoring logic
    public string Seed { get; set; }

    private fixed int _tallyValues[1024];
    private int _tallyCount;

    // New: Support for string/object column values (for InlineLabel, AnteDisplay, ItemDisplay)
    // This is a managed array, so it's separate from the unsafe fixed array
    private string?[]? _columnValues;

    public MotelySeedScoreTally(string seed, int score)
    {
        Seed = seed;
        Score = score;
        _tallyCount = 0;
        _columnValues = null;
    }

    public void AddTally(int value)
    {
        if (_tallyCount < 1024)
        {
            _tallyValues[_tallyCount] = value;
            if (_columnValues != null) _columnValues[_tallyCount] = null;
            _tallyCount++;
        }
    }

    /// <summary>
    /// Add a string value for a column (used for InlineLabel, AnteDisplay, ItemDisplay types)
    /// </summary>
    public void AddColumnValue(string? value)
    {
        if (_columnValues == null)
        {
            _columnValues = new string?[1024];
        }

        if (_tallyCount < 1024)
        {
            _columnValues[_tallyCount] = value;
            _tallyValues[_tallyCount] = 0;
            _tallyCount++;
        }
    }

    public int GetTally(int index)
    {
        // Return 0 for out-of-bounds indices (graceful degradation)
        if (index < 0 || index >= _tallyCount)
            return 0;
        return _tallyValues[index];
    }

    /// <summary>
    /// Get the string value for a column at the given index
    /// </summary>
    public string? GetColumnValue(int index)
    {
        if (index < 0 || index >= _tallyCount || _columnValues == null)
            return null;
        return _columnValues[index];
    }

    public int TallyCount => _tallyCount;

    /// <summary>
    /// Zero-allocation span over tally values (for .NET 10+ performance)
    /// </summary>
    public ReadOnlySpan<int> TallyValuesSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new ReadOnlySpan<int>((int*)Unsafe.AsPointer(ref _tallyValues[0]), _tallyCount);
    }

    /// <summary>
    /// Get tally values as a list. Consider TallyValuesSpan for zero-allocation access.
    /// </summary>
    public List<int> TallyColumns
    {
        get
        {
            var list = new List<int>(_tallyCount);
            for (int i = 0; i < _tallyCount; i++)
            {
                list.Add(_tallyValues[i]);
            }
            return list;
        }
    }

    /// <summary>
    /// Get all column values as strings (for CSV export)
    /// Returns null for columns that have integer values (backward compatibility)
    /// </summary>
    /// <summary>
    /// Get all column values as unified strings (for CSV export/Display)
    /// Merges explicit string values with integer tallies
    /// </summary>
    public List<string?> ColumnValues
    {
        get
        {
            var list = new List<string?>(_tallyCount);
            for (int i = 0; i < _tallyCount; i++)
            {
                // Prioritize explicit string value if it exists
                if (_columnValues != null && i < _columnValues.Length && _columnValues[i] != null)
                {
                    list.Add(_columnValues[i]);
                }
                // Otherwise use the integer tally value
                else
                {
                    list.Add(_tallyValues[i].ToString());
                }
            }
            return list;
        }
    }
}

public enum ScoreCutoffMode
{
    None = 0, // No cutoff (0)
    Manual = 1, // User defined
    AutoBest = 2, // Strict High Score
    AutoSmart = 3, // Smart (80% of High Score)
}

public class SharedScoreState
{
    public int LearnedCutoff;
    public long SeedsFiltered;
    public long StartTime;

    public SharedScoreState()
    {
        StartTime = DateTime.UtcNow.Ticks;
    }
}

/// <summary>
/// Clean filter descriptor for MongoDB-style queries
/// </summary>
public struct MotelyJsonSeedScoreDesc(
    MotelyJsonConfig Config,
    int Cutoff,
    ScoreCutoffMode Mode,
    Action<MotelySeedScoreTally> OnResultFound
) : IMotelySeedScoreDesc<MotelyJsonSeedScoreDesc.MotelyJsonSeedScoreProvider>
{
    // Callback to return the score object to (the caller can print, send to a db, I don't care)
    private readonly Action<MotelySeedScoreTally> _onResultFound = OnResultFound;

    public MotelyJsonSeedScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx)
    {
        // Initialize shared state for this search instance
        var state = new SharedScoreState
        {
            LearnedCutoff = (Mode == ScoreCutoffMode.Manual) ? Cutoff : 0,
            SeedsFiltered = 0,
        };

        return new MotelyJsonSeedScoreProvider(Config, Cutoff, Mode, _onResultFound, state);
    }

    public static long FilteredSeedCount => 0; // Deprecated static access

    public struct MotelyJsonSeedScoreProvider(
        MotelyJsonConfig Config,
        int Cutoff,
        ScoreCutoffMode Mode,
        Action<MotelySeedScoreTally> OnResultFound,
        SharedScoreState State
    ) : IMotelySeedScoreProvider
    {
        public static bool IsCancelled;
        private readonly SharedScoreState _state = State;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Score(
            ref MotelyVectorSearchContext searchContext,
            MotelySeedScoreTally[] buffer,
            VectorMask baseFilterMask = default,
            int scoreThreshold = 0
        )
        {
            // Base filter already checked MUST clauses - we only score seeds that passed
            // If no seeds passed the base filter, exit early
            if (baseFilterMask.IsAllFalse())
                return VectorMask.NoBitsSet;

            // Copy fields to local variables to avoid struct closure issues
            var config = Config;
            var cutoff = scoreThreshold > 0 ? scoreThreshold : Cutoff;
            var mode = Mode;
            var onResultFound = OnResultFound;
            var state = _state;

            // Score individual seeds that passed the base filter
            // NOTE: Scoring is intentionally SCALAR - we don't need vectorized performance here
            // Track filtered count locally to batch Interlocked operation
            int localFiltered = 0;

            var resultMask = searchContext.SearchIndividualSeeds(
                baseFilterMask,
                (ref MotelySingleSearchContext singleCtx) =>
                {
                    var runState = new MotelyRunState();

                    // Activate all vouchers for scoring
                    if (config.MaxVoucherAnte > 0)
                    {
                        MotelyJsonScoring.ActivateAllVouchers(
                            ref singleCtx,
                            ref runState,
                            config.MaxVoucherAnte
                        );
                    }

                    // Use pre-computed MaxBossAnte from PostProcess()
                    int maxBossAnte = config.MaxBossAnte;

                    // Generate and cache all bosses if needed
                    // Game starts at Ante 1, so bosses are generated from ante 1 onwards
                    MotelyBossBlind[]? cachedBosses = null;
                    if (maxBossAnte > 0)
                    {
                        cachedBosses = new MotelyBossBlind[maxBossAnte + 1]; // +1 to handle 1-based indexing
                        var bossStream = singleCtx.CreateBossStream();
                        var bossState = new MotelyRunState(); // Separate state for boss generation
                        for (int ante = 1; ante <= maxBossAnte; ante++)
                        {
                            cachedBosses[ante] = singleCtx.GetBossForAnte(
                                ref bossStream,
                                ante,
                                ref bossState
                            );
                        }

                        // Store cached bosses in runState for use by scoring functions
                        runState.CachedBosses = cachedBosses;
                    }

                    // Always validate Must clauses - either as the only filter (scoreOnlyMode)
                    // or as additional requirements on top of the base filter
                    if (config.Must?.Count > 0)
                    {
                        // SMART: Process vouchers FIRST in order, then other requirements

                        // Step 1: Check all voucher requirements (they depend on each other)
                        // PERFORMANCE: Use pre-partitioned array (no filtering needed!)
                        foreach (var clause in config.MustVouchers)
                        {
                            bool clauseSatisfied = false;

                            // Check if voucher is already active from ActivateAllVouchers
                            if (
                                clause.VoucherEnum.HasValue
                                && runState.IsVoucherActive(clause.VoucherEnum.Value)
                            )
                            {
                                clauseSatisfied = true;
                            }
                            else
                            {
                                // Check if it appears in any required ante
                                foreach (var ante in clause.EffectiveAntes ?? [])
                                {
                                    if (
                                        MotelyJsonScoring.CheckVoucherSingle(
                                            ref singleCtx,
                                            clause,
                                            ante,
                                            ref runState
                                        )
                                    )
                                    {
                                        clauseSatisfied = true;
                                        break;
                                    }
                                }
                            }

                            if (!clauseSatisfied)
                            {
                                return false;
                            }
                        }

                        // Step 2: Check all other requirements
                        // PERFORMANCE: Use pre-partitioned array (no filtering needed!)
                        foreach (var clause in config.MustNonVouchers)
                        {
                            bool clauseSatisfied = false;

                            // Check if this requirement appears in ANY of its required antes
                            foreach (var ante in clause.EffectiveAntes ?? [])
                            {
                                switch (clause.ItemTypeEnum)
                                {
                                    case MotelyFilterItemType.SoulJoker:
                                        if (
                                            MotelyJsonScoring.CheckSoulJokerForSeed(
                                                new List<MotelyJsonSoulJokerFilterClause>
                                                {
                                                    MotelyJsonSoulJokerFilterClause.FromJsonClause(
                                                        clause
                                                    ),
                                                },
                                                ref singleCtx,
                                                earlyExit: true
                                            )
                                        )
                                        {
                                            clauseSatisfied = true;
                                            break;
                                        }
                                        break;

                                    case MotelyFilterItemType.Joker:
                                        var mustCount = MotelyJsonScoring.CountJokerOccurrences(
                                            ref singleCtx,
                                            MotelyJsonJokerFilterClause.FromJsonClause(clause),
                                            ante,
                                            ref runState,
                                            earlyExit: true,
                                            originalClause: clause
                                        );
                                        if (mustCount > 0)
                                        {
                                            clauseSatisfied = true;
                                            break;
                                        }
                                        break;

                                    case MotelyFilterItemType.TarotCard:
                                        if (
                                            MotelyJsonScoring.TarotCardsTally(
                                                ref singleCtx,
                                                MotelyJsonTarotFilterClause.FromJsonClause(clause),
                                                ante,
                                                ref runState,
                                                earlyExit: true
                                            ) > 0
                                        )
                                        {
                                            clauseSatisfied = true;
                                            break;
                                        }
                                        break;

                                    case MotelyFilterItemType.PlanetCard:
                                        if (
                                            MotelyJsonScoring.CountPlanetOccurrences(
                                                ref singleCtx,
                                                MotelyJsonPlanetFilterClause.FromJsonClause(clause),
                                                ante,
                                                earlyExit: true
                                            ) > 0
                                        )
                                        {
                                            clauseSatisfied = true;
                                            break;
                                        }
                                        break;

                                    case MotelyFilterItemType.SpectralCard:
                                        if (
                                            MotelyJsonScoring.CountSpectralOccurrences(
                                                ref singleCtx,
                                                MotelyJsonSpectralFilterClause.FromJsonClause(
                                                    clause
                                                ),
                                                ante,
                                                earlyExit: true
                                            ) > 0
                                        )
                                        {
                                            clauseSatisfied = true;
                                            break;
                                        }
                                        break;

                                    case MotelyFilterItemType.PlayingCard:
                                        if (
                                            MotelyJsonScoring.CountPlayingCardOccurrences(
                                                ref singleCtx,
                                                clause,
                                                ante,
                                                earlyExit: true
                                            ) > 0
                                        )
                                        {
                                            clauseSatisfied = true;
                                            break;
                                        }
                                        break;

                                    case MotelyFilterItemType.SmallBlindTag:
                                    case MotelyFilterItemType.BigBlindTag:
                                        if (
                                            MotelyJsonScoring.CheckTagSingle(
                                                ref singleCtx,
                                                clause,
                                                ante
                                            )
                                        )
                                        {
                                            clauseSatisfied = true;
                                            break;
                                        }
                                        break;

                                    case MotelyFilterItemType.Boss:
                                        if (
                                            MotelyJsonScoring.CheckBossSingle(
                                                ref singleCtx,
                                                clause,
                                                ante,
                                                ref runState
                                            )
                                        )
                                        {
                                            clauseSatisfied = true;
                                            break;
                                        }
                                        break;

                                    case MotelyFilterItemType.ErraticRank:
                                        // Need to verify minimum count requirement
                                        if (clause.RankEnum.HasValue)
                                        {
                                            var count =
                                                MotelyJsonScoring.CountErraticRankOccurrences(
                                                    ref singleCtx,
                                                    clause.RankEnum.Value
                                                );
                                            clauseSatisfied = count >= (clause.Min ?? 0);
                                        }
                                        else
                                        {
                                            clauseSatisfied = false;
                                        }
                                        break;

                                    case MotelyFilterItemType.ErraticSuit:
                                        // Need to verify minimum count requirement
                                        if (clause.SuitEnum.HasValue)
                                        {
                                            var count =
                                                MotelyJsonScoring.CountErraticSuitOccurrences(
                                                    ref singleCtx,
                                                    clause.SuitEnum.Value
                                                );
                                            clauseSatisfied = count >= (clause.Min ?? 0);
                                        }
                                        else
                                        {
                                            clauseSatisfied = false;
                                        }
                                        break;

                                    case MotelyFilterItemType.Event:
                                        // Events should be handled by Event filters, not in per-ante scoring
                                        // If an Event clause appears here, it's a configuration error
                                        throw new InvalidOperationException(
                                            $"Event clauses should not be in MustNonVouchers. "
                                                + $"Event filtering is handled separately by Event filter system."
                                        );

                                    case MotelyFilterItemType.And:
                                    case MotelyFilterItemType.Or:
                                        // And/Or clauses are handled at the filter level, so if we reach scoring,
                                        // they've already passed. Just mark as satisfied.
                                        clauseSatisfied = true;
                                        break;

                                    case MotelyFilterItemType.Voucher:
                                        // Vouchers should be in MustVouchers, not MustNonVouchers
                                        throw new InvalidOperationException(
                                            $"Voucher clauses should be in MustVouchers, not MustNonVouchers. "
                                                + $"This is a configuration error."
                                        );

                                    default:
                                        throw new NotImplementedException(
                                            $"MUST clause verification not implemented for type: {clause.ItemTypeEnum}. "
                                                + $"Add a case to the switch in MotelyJsonSeedScoreDesc.cs (line ~231)"
                                        );
                                }

                                if (clauseSatisfied)
                                    break; // Found in one ante, move to next clause
                            }

                            // If this Must clause wasn't satisfied, seed fails
                            if (!clauseSatisfied)
                            {
                                // DebugLogger.Log($"[Score] Non-voucher clause not satisfied: {clause.ItemTypeEnum} {clause.Value}"); // DISABLED FOR PERFORMANCE
                                return false;
                            }
                            else
                            {
                                // DebugLogger.Log($"[Score] Non-voucher clause satisfied: {clause.ItemTypeEnum} {clause.Value}"); // DISABLED FOR PERFORMANCE
                            }
                        }
                    }

                    // Get seed string first
                    string seedStr;
                    unsafe
                    {
                        char* seedPtr = stackalloc char[9];
                        int length = singleCtx.GetSeed(seedPtr);
                        seedStr = new string(seedPtr, 0, length);
                    }

                    // Score Should clauses and add tallies (aggregation controlled by top-level mode)
                    // Use new column architecture if available, otherwise fall back to legacy behavior
                    int totalScore = 0;
                    var seedScore = new MotelySeedScoreTally(seedStr, 0);

                    // Check if config has explicit column definitions (future feature)
                    // For now, create columns from Should clauses (backward compatibility)
                    var columnDefinitions = ColumnDefinitionHelper.CreateFromShouldClauses(config);

                    if (columnDefinitions.Count > 0 && config.Should != null)
                    {
                        // New column-based scoring system
                        // Match columns to Should clauses by index
                        for (int i = 0; i < columnDefinitions.Count && i < config.Should.Count; i++)
                        {
                            var column = columnDefinitions[i];
                            var shouldClause = config.Should[i];

                            // Create a copy of runState for each column evaluation
                            var columnRunState = runState;

                            // Evaluate the column
                            var columnValue = column.Evaluate(ref singleCtx, ref columnRunState);

                            // Add to score if it's a ScoreTally column
                            if (column.Type == ColumnType.ScoreTally)
                            {
                                // Try to parse as integer for scoring
                                if (int.TryParse(columnValue, out var intValue))
                                {
                                    var scoreMultiplier = shouldClause.Score;
                                    totalScore += intValue * scoreMultiplier;

                                    // Add as integer tally (backward compatibility)
                                    seedScore.AddTally(intValue);
                                }
                                else
                                {
                                    seedScore.AddTally(0);
                                }
                            }
                            else
                            {
                                // For non-ScoreTally columns, add as string value
                                // AddColumnValue already adds 0 to tally internally for index alignment
                                seedScore.AddColumnValue(columnValue);
                            }
                        }
                    }
                    else if (config.Should?.Count > 0)
                    {
                        // Legacy scoring system (backward compatibility)
                        switch (config.ScoreAggregationMode)
                        {
                            case MotelyScoreAggregationMode.Sum:
                                foreach (var should in config.Should)
                                {
                                    // CRITICAL FIX: Create a COPY of runState for each clause evaluation
                                    // runState is mutated by counting functions (e.g., AddOwnedJoker),
                                    // and this mutation was affecting subsequent clause evaluations!
                                    var clauseRunState = runState; // Struct copy - preserves original state

                                    int count = MotelyJsonScoring.CountOccurrences(
                                        ref singleCtx,
                                        should,
                                        ref clauseRunState
                                    );
                                    int score = count * should.Score;
                                    totalScore += score;

                                    // DEBUG: Log what's being added
                                    // System.Console.WriteLine($"[DEBUG] Adding tally for {should.Type}/{should.Value}: {count}"); // DISABLED FOR PERFORMANCE
                                    seedScore.AddTally(count);
                                }
                                break;
                            case MotelyScoreAggregationMode.MaxCount:
                            {
                                int maxCount = 0;
                                foreach (var should in config.Should)
                                {
                                    // CRITICAL FIX: Create a COPY of runState for each clause evaluation
                                    var clauseRunState = runState; // Struct copy - preserves original state

                                    int count = MotelyJsonScoring.CountOccurrences(
                                        ref singleCtx,
                                        should,
                                        ref clauseRunState
                                    );
                                    if (count > maxCount)
                                        maxCount = count;
                                    seedScore.AddTally(count);
                                }
                                totalScore = maxCount;
                                break;
                            }
                            default:
                                // Future-proofing: default to Sum behavior for unknown modes
                                DebugLogger.Log(
                                    $"[Score] Unknown ScoreAggregationMode: {config.ScoreAggregationMode}; defaulting to Sum"
                                );
                                foreach (var should in config.Should)
                                {
                                    // CRITICAL FIX: Create a COPY of runState for each clause evaluation
                                    var clauseRunState = runState; // Struct copy - preserves original state

                                    int count = MotelyJsonScoring.CountOccurrences(
                                        ref singleCtx,
                                        should,
                                        ref clauseRunState
                                    );
                                    int score = count * should.Score;
                                    totalScore += score;
                                    seedScore.AddTally(count);
                                }
                                break;
                        }
                    }

                    // Set final score
                    seedScore.Score = totalScore;
                    buffer[singleCtx.VectorLane] = seedScore;

                    // Increment local counter (batch Interlocked operation later)
                    localFiltered++;

                    // Apply cutoff filtering - return true/false, caller will count results
                    var currentCutoff = GetCurrentCutoff(totalScore, mode, cutoff, state);
                    bool passedCutoff = totalScore >= currentCutoff;

                    // Invoke callback for seeds that passed cutoff
                    if (passedCutoff && onResultFound != null)
                    {
                        onResultFound(seedScore);
                    }

                    return passedCutoff;
                }
            );

            // Batch update filtered counter ONCE per vector (instead of 8 times per seed!)
            if (localFiltered > 0)
            {
                Interlocked.Add(ref state.SeedsFiltered, localFiltered);
            }

            // Return the mask - caller will count how many passed and invoke callbacks
            return resultMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetCurrentCutoff(
            int currentScore,
            ScoreCutoffMode mode,
            int cutoff,
            SharedScoreState state
        )
        {
            // Thread-safe auto cutoff: Start at 1, raise to highest score found
            if (mode == ScoreCutoffMode.AutoBest || mode == ScoreCutoffMode.AutoSmart)
            {
                // IMPORTANT: must be monotonic (max-only). A check-then-exchange can
                // overwrite a higher cutoff with a lower value under contention.
                int observed;
                while (true)
                {
                    observed = Volatile.Read(ref state.LearnedCutoff);
                    if (currentScore <= observed)
                        break;

                    if (
                        Interlocked.CompareExchange(ref state.LearnedCutoff, currentScore, observed)
                        == observed
                    )
                        break;
                }
            }

            return mode switch
            {
                ScoreCutoffMode.None => 0,
                ScoreCutoffMode.Manual => cutoff,
                ScoreCutoffMode.AutoBest => state.LearnedCutoff,
                ScoreCutoffMode.AutoSmart => GetSmartCutoff(state),
                _ => 0,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetSmartCutoff(SharedScoreState state)
        {
            // Smart Mode:
            // 1. Warmup: Keep cutoff 0 for first 1 second
            // 2. Filter: If best score > 0, set cutoff to at least 1 (or 80% of best)

            long elapsedTicks = DateTime.UtcNow.Ticks - state.StartTime;
            if (elapsedTicks < TimeSpan.TicksPerSecond * 10)
            {
                return 0; // Warmup period
            }

            int best = state.LearnedCutoff;
            if (best > 0)
            {
                // If we found something, filter out garbage (0s)
                // Use 67% rule but ensure at least 1
                return Math.Max(1, (int)(best * 0.67)); // 🤲🏻
            }

            return 0; // Nothing found yet, keep searching everything
        }
    }
}
