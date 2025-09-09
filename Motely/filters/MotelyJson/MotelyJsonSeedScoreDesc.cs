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
    public int Score { get; }
    public string Seed { get; }
    
    private fixed int _tallyValues[32];
    private int _tallyCount;
    
    public MotelySeedScoreTally(string seed, int score)
    {
        Seed = seed;
        Score = score;
        _tallyCount = 0;
    }
    
    public void AddTally(int value)
    {
        if (_tallyCount < 32)
        {
            _tallyValues[_tallyCount++] = value;
        }
    }
    
    public int GetTally(int index)
    {
        return index < _tallyCount ? _tallyValues[index] : 0;
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
    Action<MotelySeedScoreTally> OnResultFound,
    bool ScoreOnlyMode = false
)
    : IMotelySeedScoreDesc<MotelyJsonSeedScoreDesc.MotelyJsonSeedScoreProvider>
{

    // Auto cutoff state
    private static int _learnedCutoff = 1;

    // Results tracking for rarity calculation
    private static long _resultsFound = 0;
    public static long ResultsFound => _resultsFound;
    
    // Debug: Track how many seeds reach scoring
    private static long _seedsScored = 0;
    public static long SeedsScored => _seedsScored;

    // Callback to return the score object to (the caller can print, send to a db, I don't care)
    private readonly Action<MotelySeedScoreTally> _onResultFound = OnResultFound;

    public MotelyJsonSeedScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx)
    {
        // Reset for new search
        _learnedCutoff = Cutoff;
        _resultsFound = 0;

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

        return new MotelyJsonSeedScoreProvider(Config, Cutoff, AutoCutoff, _onResultFound, ScoreOnlyMode);
    }

    public struct MotelyJsonSeedScoreProvider(MotelyJsonConfig Config, int Cutoff, bool AutoCutoff, Action<MotelySeedScoreTally> OnResultFound, bool ScoreOnlyMode = false)
        : IMotelySeedScoreProvider
    {
        public static bool IsCancelled;
        
        private const int MaxShouldClauses = 32;
        [ThreadStatic] private static int[]? _threadLocalScoresBuffer;

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
            var scoreOnlyMode = ScoreOnlyMode;
            
            // SIMPLE VECTORIZED PRE-FILTER for vouchers only
            var preFilterMask = baseFilterMask;
            var vectorRunState = new MotelyVectorRunState();
            
            // DebugLogger.Log($"[Score] Starting vectorized pre-filter, Must clauses: {config.Must?.Count ?? 0}"); // DISABLED FOR PERFORMANCE
            
            // Process vouchers in ante order to build up state correctly
            var voucherClauseMasks = new Dictionary<MotelyJsonConfig.MotleyJsonFilterClause, VectorMask>();
            
            // Pre-compute which clauses apply to which antes to avoid LINQ in hot path
            var clausesByAnte = new List<MotelyJsonConfig.MotleyJsonFilterClause>[config.MaxVoucherAnte + 1];
            
            // Initialize masks for voucher clauses and build ante mapping
            if (config.Must?.Count > 0)
            {
                foreach (var clause in config.Must)
                {
                    if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher && clause.VoucherEnum.HasValue)
                    {
                        voucherClauseMasks[clause] = VectorMask.NoBitsSet;
                        
                        // Pre-compute which antes this clause cares about
                        if (clause.EffectiveAntes != null)
                        {
                            foreach (var ante in clause.EffectiveAntes)
                            {
                                if (ante <= config.MaxVoucherAnte)
                                {
                                    clausesByAnte[ante] ??= new List<MotelyJsonConfig.MotleyJsonFilterClause>();
                                    clausesByAnte[ante].Add(clause);
                                }
                            }
                        }
                    }
                }
            }
            
            // Process each ante in order, activating vouchers and checking clauses
            if (config.MaxVoucherAnte > 0)
            {
                for (int ante = 1; ante <= config.MaxVoucherAnte; ante++)
                {
                    var vouchers = searchContext.GetAnteFirstVoucher(ante, vectorRunState);
                    
                    // Check only the clauses that care about this ante (pre-computed!)
                    var clausesForThisAnte = clausesByAnte[ante];
                    if (clausesForThisAnte != null)
                    {
                        foreach (var clause in clausesForThisAnte)
                        {
                            var matches = VectorEnum256.Equals(vouchers, clause.VoucherEnum!.Value);
                            voucherClauseMasks[clause] |= matches; // OR - voucher can appear at any ante
                        }
                    }
                    
                    // THEN activate the voucher for future antes
                    // Note: ActivateVoucher already respects lanes - it only activates where voucher != None
                    vectorRunState.ActivateVoucher(vouchers);
                }
            }
            
            // Now apply all voucher clause masks to the pre-filter
            foreach (var kvp in voucherClauseMasks)
            {
                preFilterMask &= kvp.Value;
                if (preFilterMask.IsAllFalse()) 
                    return VectorMask.NoBitsSet; // No seeds pass, exit early
            }
            
            // If we have a base filter mask (from --native filter), combine it with our pre-filter
            if (!baseFilterMask.Equals(default(VectorMask)))
            {
                preFilterMask &= baseFilterMask;
                if (preFilterMask.IsAllFalse()) 
                    return VectorMask.NoBitsSet; // No seeds pass, exit early
            }
            
            // Process individual seeds - use preFilterMask for additional filtering if needed
            // When not in ScoreOnlyMode, this should be AllBitsSet for voucher-filtered seeds
            return searchContext.SearchIndividualSeeds(preFilterMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // DebugLogger.Log($"[Score] Processing individual seed"); // DISABLED FOR PERFORMANCE
                // var sw = System.Diagnostics.Stopwatch.StartNew(); // DISABLED FOR PERFORMANCE
                var runState = new MotelyRunState();

                // Activate all vouchers for scoring (using cached property)
#if DEBUG
                DebugLogger.Log($"[Score] MaxVoucherAnte: {config.MaxVoucherAnte}");
#endif
                if (config.MaxVoucherAnte > 0)
                {
#if DEBUG
                    DebugLogger.Log($"[Score] Activating all vouchers up to ante {config.MaxVoucherAnte}");
#endif
                    MotelyJsonScoring.ActivateAllVouchers(ref singleCtx, ref runState, config.MaxVoucherAnte);
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
                                    if (MotelyJsonScoring.CountSoulJokerOccurrences(ref singleCtx, clause, ante, ref runState, earlyExit: true) > 0)
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


                int totalScore = 0;
                
                _threadLocalScoresBuffer ??= new int[MaxShouldClauses];
                var scoresBuffer = _threadLocalScoresBuffer;
                int scoreCount = 0;

                if (config.Should?.Count > 0)
                {
                    foreach (var should in config.Should)
                    {
                        int count = MotelyJsonScoring.CountOccurrences(ref singleCtx, should, ref runState);
                        int score = count * should.Score;
                        
                        if (scoreCount < MaxShouldClauses)
                        {
                            scoresBuffer[scoreCount++] = count;
                        }
                        totalScore += score;
                    }
                }

                string seedStr;
                unsafe
                {
                    char* seedPtr = stackalloc char[9];
                    int length = singleCtx.GetSeed(seedPtr);
                    seedStr = new string(seedPtr, 0, length);
                }
                
                var seedScore = new MotelySeedScoreTally(seedStr, totalScore);
                for (int i = 0; i < scoreCount; i++)
                {
                    seedScore.AddTally(scoresBuffer[i]);
                }
                
                buffer[singleCtx.VectorLane] = seedScore;
                
                // Apply cutoff filtering - only pass seeds that meet the threshold
                var currentCutoff = GetCurrentCutoff(totalScore, autoCutoff, cutoff);
                if (totalScore >= currentCutoff)
                {
                    Interlocked.Increment(ref _resultsFound);
                    return true; // Pass this seed
                }
                
                return false; // Filter out this seed
            });
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