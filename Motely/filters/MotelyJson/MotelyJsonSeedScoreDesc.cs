using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;
using System.Numerics;

namespace Motely.Filters;


public struct MotelySeedScoreTally : IMotelySeedScore
{
    public int Score { get; }
    public List<int> TallyColumns { get; }
    public string Seed { get; }

    public MotelySeedScoreTally(string seed, int score, List<int> tallyColumns)
    {
        Seed = seed;
        Score = score;
        TallyColumns = tallyColumns;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Score(ref MotelyVectorSearchContext searchContext)
        {
            if (IsCancelled)
                return;

            // Copy fields to local variables to avoid struct closure issues
            var config = Config;
            var cutoff = Cutoff;
            var autoCutoff = AutoCutoff;
            var onResultFound = OnResultFound;
            var scoreOnlyMode = ScoreOnlyMode;
            
            // SIMPLE VECTORIZED PRE-FILTER for vouchers only
            var preFilterMask = VectorMask.AllBitsSet;
            var vectorRunState = new MotelyVectorRunState();
            
            // Try to vectorize simple voucher Must clauses
            if (config.Must?.Count > 0)
            {
                foreach (var clause in config.Must)
                {
                    if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher && 
                        clause.VoucherEnum.HasValue && 
                        clause.EffectiveAntes != null)
                    {
                        var voucherMask = VectorMask.NoBitsSet;
                        foreach (var ante in clause.EffectiveAntes)
                        {
                            var vouchers = searchContext.GetAnteFirstVoucher(ante, vectorRunState);
                            var matches = VectorEnum256.Equals(vouchers, clause.VoucherEnum.Value);
                            voucherMask |= matches; // OR - voucher can appear at any ante
                        }
                        preFilterMask &= voucherMask;
                        if (preFilterMask.IsAllFalse()) 
                            return; // No seeds pass, exit early
                    }
                }
            }
            
            // NOW process individual seeds, but ONLY those that passed vectorized checks
            searchContext.SearchIndividualSeeds(preFilterMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // var sw = System.Diagnostics.Stopwatch.StartNew(); // DISABLED FOR PERFORMANCE
                var runState = new MotelyRunState();

                // Activate all vouchers for scoring (using cached property)
                if (config.MaxVoucherAnte > 0)
                {
                    MotelyJsonScoring.ActivateAllVouchers(ref singleCtx, ref runState, config.MaxVoucherAnte);
                }

                // In scoreOnly mode, we need to validate Must clauses since there's no base filter
                if (scoreOnlyMode && config.Must?.Count > 0)
                {
                    // Validate Must clauses - ALL must be satisfied
                    foreach (var clause in config.Must)
                    {
                        bool clauseSatisfied = false;
                        
                        // Check if this requirement appears in ANY of its required antes
                        foreach (var ante in clause.EffectiveAntes ?? [])
                        {
                            switch (clause.ItemTypeEnum)
                            {
                                case MotelyFilterItemType.Voucher:
                                    if (MotelyJsonScoring.CheckVoucherSingle(ref singleCtx, clause, ante, ref runState))
                                    {
                                        clauseSatisfied = true;
                                        break;
                                    }
                                    break;
                                    
                                case MotelyFilterItemType.SoulJoker:
                                    if (MotelyJsonScoring.CountSoulJokerOccurrences(ref singleCtx, clause, ante, ref runState, earlyExit: true) > 0)
                                    {
                                        clauseSatisfied = true;
                                        break;
                                    }
                                    break;
                                    
                                case MotelyFilterItemType.Joker:
                                    if (MotelyJsonScoring.CountJokerOccurrences(ref singleCtx, clause, ante, ref runState, earlyExit: true) > 0)
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
                            }
                            
                            if (clauseSatisfied) break; // Found in one ante, move to next clause
                        }
                        
                        // If this Must clause wasn't satisfied, seed fails
                        if (!clauseSatisfied)
                            return false;
                    }
                }

                // Check all MUST NOT clauses
                if (config.MustNot?.Count > 0)
                {
                    foreach (var clause in config.MustNot)
                    {
                        if (MotelyJsonScoring.CheckSingleClause(ref singleCtx, clause, ref runState))
                            return false;
                    }
                }

                // Calculate scores for SHOULD clauses using comprehensive scanning like NegativeCopyJokers
                int totalScore = 0;  // Start at 0, not 1!
                var scores = new List<int>();

                if (config.Should?.Count > 0)
                {
                    foreach (var should in config.Should)
                    {
                        // Use comprehensive counting across all relevant antes
                        int count = MotelyJsonScoring.CountOccurrences(ref singleCtx, should, ref runState);
                        int score = count * should.Score;
                        scores.Add(count);
                        totalScore += score;
                        
                        // DebugLogger.Log($"[Should] {should.ItemTypeEnum} {should.Value}: found {count}, score {score}"); // DISABLED FOR PERFORMANCE
                    }
                }

                // Only return true if score meets threshold
                if (totalScore >= GetCurrentCutoff(totalScore, autoCutoff, cutoff))
                {
                    // Track results for rarity calculation
                    Interlocked.Increment(ref _resultsFound);

                    string seedStr;
                    unsafe
                    {
                        char* seedPtr = stackalloc char[9];
                        int length = singleCtx.GetSeed(seedPtr);
                        seedStr = new string(seedPtr, 0, length);
                    }
                    var seedScore = new MotelySeedScoreTally(seedStr, totalScore, scores);
                    onResultFound(seedScore); // RICH CALLBACK!
                    
                    return true; // Tell framework this seed passed
                }

                return false;
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
                DebugLogger.Log($"[AutoCutoff] Raised cutoff from {oldCutoff} to {currentScore}");
            }

            return _learnedCutoff;
        }
    }
}