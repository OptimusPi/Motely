using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;
using System.Numerics;

namespace Motely.Filters;


public unsafe struct MotelySeedScoreTally : IMotelySeedScore
{
    public int Score { get; set; } // Made mutable for easier scoring logic
    public string Seed { get; }
    
    private fixed int _tallyValues[128];
    private int _tallyCount;

    public MotelySeedScoreTally(string seed, int score)
    {
        Seed = seed;
        Score = score;
        _tallyCount = 0;
    }

    public void AddTally(int value)
    {
        if (_tallyCount < 128)
        {
            _tallyValues[_tallyCount++] = value;
        }
    }
    
    public int GetTally(int index)
    {
        // Return 0 for out-of-bounds indices (graceful degradation)
        if (index < 0 || index >= _tallyCount)
            return 0;
        return _tallyValues[index];
    }
    
    public int TallyCount => _tallyCount;
    
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
    private static int _learnedCutoff = 0;

    // Track seeds that passed filter (before cutoff check)
    private static long _seedsFiltered = 0;
    public static long SeedsFiltered => _seedsFiltered;

    // Callback to return the score object to (the caller can print, send to a db, I don't care)
    private readonly Action<MotelySeedScoreTally> _onResultFound = OnResultFound;

    public MotelyJsonSeedScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx)
    {
        // Reset for new search
        // Auto-cutoff starts at 0, manual cutoff uses specified value
        _learnedCutoff = AutoCutoff ? 0 : Cutoff;
        _seedsFiltered = 0;

        // Cache voucher streams for vectorizable Must clauses
        if (Config.Must != null)
        {
            foreach (var clause in Config.Must)
            {
                if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher && clause.EffectiveAntes != null)
                {
                    foreach (var ante in clause.EffectiveAntes)
                    {
                        ctx.CacheAnteFirstVoucher(ante);
                    }
                }
            }
        }

        return new MotelyJsonSeedScoreProvider(Config, Cutoff, AutoCutoff, _onResultFound);
    }

    public static long GetFilteredSeeds() => _seedsFiltered;

    public struct MotelyJsonSeedScoreProvider(MotelyJsonConfig Config, int Cutoff, bool AutoCutoff, Action<MotelySeedScoreTally> OnResultFound) : IMotelySeedScoreProvider
    {
        public static bool IsCancelled;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Score(ref MotelyVectorSearchContext searchContext, MotelySeedScoreTally[] buffer, VectorMask baseFilterMask = default, int scoreThreshold = 0)
        {
            if (IsCancelled)
                return VectorMask.NoBitsSet;

            // Copy fields to local variables to avoid struct closure issues
            var config = Config;
            var cutoff = scoreThreshold > 0 ? scoreThreshold : Cutoff;
            var autoCutoff = AutoCutoff;
            var onResultFound = OnResultFound;

            // Base filter already checked MUST clauses - we only score seeds that passed
            // If no seeds passed the base filter, exit early
            if (baseFilterMask.IsAllFalse())
                return VectorMask.NoBitsSet;

            // Score individual seeds that passed the base filter
            // NOTE: Scoring is intentionally SCALAR - we don't need vectorized performance here
            // Track filtered count locally to batch Interlocked operation
            int localFiltered = 0;

            var resultMask = searchContext.SearchIndividualSeeds(baseFilterMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                var runState = new MotelyRunState();

                // Activate all vouchers for scoring (vouchers are cached, so this is fast enough)
                if (config.MaxVoucherAnte > 0)
                {
                    MotelyJsonScoring.ActivateAllVouchers(ref singleCtx, ref runState, config.MaxVoucherAnte);
                }
                
                // Pre-generate all bosses to maintain state across scoring checks
                // Find max ante needed for boss checks
                int maxBossAnte = 0;
                if (config.Must != null)
                {
                    foreach (var clause in config.Must)
                    {
                        if (clause.ItemTypeEnum == MotelyFilterItemType.Boss && clause.EffectiveAntes != null)
                        {
                            foreach (var ante in clause.EffectiveAntes)
                            {
                                if (ante > maxBossAnte) maxBossAnte = ante;
                            }
                        }
                    }
                }
                if (config.Should != null)
                {
                    foreach (var clause in config.Should)
                    {
                        if (clause.ItemTypeEnum == MotelyFilterItemType.Boss && clause.EffectiveAntes != null)
                        {
                            foreach (var ante in clause.EffectiveAntes)
                            {
                                if (ante > maxBossAnte) maxBossAnte = ante;
                            }
                        }
                    }
                }
                
                // REMOVED: Don't re-verify MUST clauses! The base filter already checked them!
                // Re-verifying with a fresh state breaks voucher state tracking.
                // The score provider should trust the base filter and only score Should clauses.
                
                // Generate and cache all bosses if needed
                MotelyBossBlind[]? cachedBosses = null;
                if (maxBossAnte > 0)
                {
                    cachedBosses = new MotelyBossBlind[maxBossAnte + 1]; // +1 to handle 0-based indexing
                    var bossStream = singleCtx.CreateBossStream();
                    var bossState = new MotelyRunState(); // Separate state for boss generation
                    for (int ante = 0; ante <= maxBossAnte; ante++)
                    {
                        cachedBosses[ante] = singleCtx.GetBossForAnte(ref bossStream, ante, ref bossState);
                    }
                    
                    // Store cached bosses in runState for use by scoring functions
                    runState.CachedBosses = cachedBosses;
                }

                // Always validate Must clauses - either as the only filter (scoreOnlyMode) 
                // or as additional requirements on top of the base filter
                if (config.Must?.Count > 0)
                {
                    // SMART: Process vouchers FIRST in order, then other requirements
                    // This ensures Telescope is activated before checking Observatory
                    
                    // Step 1: Check all voucher requirements (they depend on each other)
                    // PERFORMANCE: Avoid LINQ in hot path - iterate directly
                    foreach (var clause in config.Must)
                    {
                        if (clause.ItemTypeEnum != MotelyFilterItemType.Voucher)
                            continue;
                            
                        bool clauseSatisfied = false;
                        
                        // Check if voucher is already active from ActivateAllVouchers
                        if (clause.VoucherEnum.HasValue && runState.IsVoucherActive(clause.VoucherEnum.Value))
                        {
                            clauseSatisfied = true;
                        }
                        else
                        {
                            // Check if it appears in any required ante
                            foreach (var ante in clause.EffectiveAntes ?? [])
                            {
                                if (MotelyJsonScoring.CheckVoucherSingle(ref singleCtx, clause, ante, ref runState))
                                {
                                    clauseSatisfied = true;
                                    break;
                                }
                            }
                        }
                        
                        if (!clauseSatisfied)
                        {
                            // DebugLogger.Log($"[Score] Voucher clause not satisfied: {clause.Value}"); // DISABLED FOR PERFORMANCE
                            return false;
                        }
                        else
                        {
                            // DebugLogger.Log($"[Score] Voucher clause satisfied: {clause.Value}"); // DISABLED FOR PERFORMANCE
                        }
                    }
                    
                    // Step 2: Check all other requirements
                    // PERFORMANCE: Avoid LINQ in hot path - iterate directly
                    foreach (var clause in config.Must)
                    {
                        if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher)
                            continue;
                            
                        bool clauseSatisfied = false;
                        
                        // Check if this requirement appears in ANY of its required antes
                        foreach (var ante in clause.EffectiveAntes ?? [])
                        {
                            switch (clause.ItemTypeEnum)
                            {
                                    
                                case MotelyFilterItemType.SoulJoker:
                                    #if DEBUG
                                    System.Console.WriteLine($"[DEBUG] Processing SoulJoker Must clause - Value: {clause.Value}, JokerEnum: {clause.JokerEnum}, Ante: {ante}");
                                    #endif
                                    if (MotelyJsonScoring.CheckSoulJokerForSeed(new List<MotelyJsonSoulJokerFilterClause> { MotelyJsonSoulJokerFilterClause.FromJsonClause(clause) }, ref singleCtx, earlyExit: true))
                                    {
                                        clauseSatisfied = true;
                                        break;
                                    }
                                    break;
                                    
                                case MotelyFilterItemType.Joker:
                                    if (MotelyJsonScoring.CountJokerOccurrences(ref singleCtx, MotelyJsonJokerFilterClause.FromJsonClause(clause), ante, ref runState, earlyExit: true, originalClause: clause) > 0)
                                    {
                                        clauseSatisfied = true;
                                        break;
                                    }
                                    break;
                                    
                                case MotelyFilterItemType.TarotCard:
                                    if (MotelyJsonScoring.TarotCardsTally(ref singleCtx, clause, ante, ref runState, earlyExit: true) > 0)
                                    {
                                        clauseSatisfied = true;
                                        break;
                                    }
                                    break;
                                    
                                case MotelyFilterItemType.PlanetCard:
                                    if (MotelyJsonScoring.CountPlanetOccurrences(ref singleCtx, clause, ante, earlyExit: true) > 0)
                                    {
                                        clauseSatisfied = true;
                                        break;
                                    }
                                    break;
                                    
                                case MotelyFilterItemType.SpectralCard:
                                    if (MotelyJsonScoring.CountSpectralOccurrences(ref singleCtx, clause, ante, earlyExit: true) > 0)
                                    {
                                        clauseSatisfied = true;
                                        break;
                                    }
                                    break;
                                    
                                case MotelyFilterItemType.PlayingCard:
                                    if (MotelyJsonScoring.CountPlayingCardOccurrences(ref singleCtx, clause, ante, earlyExit: true) > 0)
                                    {
                                        clauseSatisfied = true;
                                        break;
                                    }
                                    break;
                                    
                                case MotelyFilterItemType.SmallBlindTag:
                                case MotelyFilterItemType.BigBlindTag:
                                    if (MotelyJsonScoring.CheckTagSingle(ref singleCtx, clause, ante))
                                    {
                                        clauseSatisfied = true;
                                        break;
                                    }
                                    break;
                            }
                            
                            if (clauseSatisfied) break; // Found in one ante, move to next clause
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

                // Score Should clauses and add tallies
                int totalScore = 0;
                var seedScore = new MotelySeedScoreTally(seedStr, 0);

                if (config.Should?.Count > 0)
                {
                    foreach (var should in config.Should)
                    {
                        int count = MotelyJsonScoring.CountOccurrences(ref singleCtx, should, ref runState);
                        int score = count * should.Score;
                        totalScore += score;

                        seedScore.AddTally(count);
                    }
                }

                // Set final score
                seedScore.Score = totalScore;
                buffer[singleCtx.VectorLane] = seedScore;

                // Increment local counter (batch Interlocked operation later)
                localFiltered++;

                // Apply cutoff filtering - return true/false, caller will count results
                var currentCutoff = GetCurrentCutoff(totalScore, autoCutoff, cutoff);
                return totalScore >= currentCutoff;
            });

            // Batch update filtered counter ONCE per vector (instead of 8 times per seed!)
            if (localFiltered > 0)
            {
                Interlocked.Add(ref _seedsFiltered, localFiltered);
            }

            // Return the mask - caller will count how many passed and invoke callbacks
            return resultMask;
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
                // DebugLogger.Log($"[AutoCutoff] Raised cutoff from {oldCutoff} to {currentScore}"); // DISABLED FOR PERFORMANCE
            }

            return _learnedCutoff;
        }
    }
}