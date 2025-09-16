using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on tag criteria from JSON configuration.
/// </summary>
public struct MotelyJsonTagFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> tagClauses)
    : IMotelySeedFilterDesc<MotelyJsonTagFilterDesc.MotelyJsonTagFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _tagClauses = tagClauses;

    public MotelyJsonTagFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        Debug.Assert(_tagClauses != null, "Tag filter clauses should not be null");
        Debug.Assert(_tagClauses.Count > 0, "Tag filter clauses should not be empty");

        // Tags don't use pack streams themselves, but we need to cache them
        // in case this is the base filter and subsequent filters need them
        if (_tagClauses != null && _tagClauses.Count > 0)
        {
            // Find all antes used by tag clauses
            var allAntes = new HashSet<int>();
            foreach (var clause in _tagClauses)
            {
                if (clause.EffectiveAntes != null)
                {
                    foreach (var ante in clause.EffectiveAntes)
                    {
                        allAntes.Add(ante);
                    }
                }
            }

            // Cache pack streams for all antes to support chained filters
            // This prevents NullReferenceException when Tag is the base filter
            foreach (var ante in allAntes)
            {
                ctx.CacheBoosterPackStream(ante);
                ctx.CacheTagStream(ante);  // Also cache tag stream for efficiency
            }
        }
        
        return new MotelyJsonTagFilter(_tagClauses!);
    }

    public struct MotelyJsonTagFilter : IMotelySeedFilter
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses;
        private readonly int _minAnte;
        private readonly int _maxAnte;

        public MotelyJsonTagFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses)
        {
            _clauses = clauses;
            // Calculate ante range ONCE during filter creation, not in every Filter() call
            (_minAnte, _maxAnte) = CalculateAnteRange(_clauses);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            DebugLogger.Log($"[TAG FILTER] Called with {_clauses?.Count ?? 0} clauses");
            
            if (_clauses == null || _clauses.Count == 0)
            {
                DebugLogger.Log("[TAG FILTER] No clauses - returning AllBitsSet");
                return VectorMask.AllBitsSet;
            }
            
            // Use pre-calculated ante range for maximum performance
            // Stack-allocated clause masks - accumulate results per clause across all antes
            Span<VectorMask> clauseMasks = stackalloc VectorMask[_clauses.Count];
            for (int i = 0; i < clauseMasks.Length; i++)
                clauseMasks[i] = VectorMask.NoBitsSet;

            // OPTIMIZED: Loop antes first (like joker filter), then clauses - ensures one stream per ante!
            for (int ante = _minAnte; ante <= _maxAnte; ante++)
            {
                // Use non-cached tag stream (working version)
                var tagStream = ctx.CreateTagStream(ante);
                var smallTag = ctx.GetNextTag(ref tagStream);
                var bigTag = ctx.GetNextTag(ref tagStream);
                
                // DEBUG: Log tag generation for specific seed
                if (DebugLogger.IsEnabled)
                {
                    DebugLogger.Log($"[TAG FILTER] Ante {ante}: smallTag={smallTag}, bigTag={bigTag}");
                }
                
                for (int clauseIndex = 0; clauseIndex < _clauses.Count; clauseIndex++)
                {
                    var clause = _clauses[clauseIndex];
                    
                    // Skip if this ante isn't in clause's effective antes
                    if (clause.EffectiveAntes != null && !clause.EffectiveAntes.Contains(ante))
                        continue;
                    
                    if (clause.TagEnum.HasValue || (clause.TagEnums != null && clause.TagEnums.Count > 0))
                    {
                        VectorMask tagMatches;
                        
                        // Handle multiple values (OR logic) or single value
                        if (clause.TagEnums != null && clause.TagEnums.Count > 0)
                        {
                            // Multi-value: any tag in the list matches (OR logic)
                            tagMatches = VectorMask.NoBitsSet;
                            foreach (var tagEnum in clause.TagEnums)
                            {
                                var singleTagMatches = clause.TagTypeEnum switch
                                {
                                    MotelyTagType.SmallBlind => VectorEnum256.Equals(smallTag, tagEnum),
                                    MotelyTagType.BigBlind => VectorEnum256.Equals(bigTag, tagEnum),
                                    _ => VectorEnum256.Equals(smallTag, tagEnum) | VectorEnum256.Equals(bigTag, tagEnum)
                                };
                                tagMatches |= singleTagMatches;
                            }
                        }
                        else if (clause.TagEnum.HasValue)
                        {
                            // Single value: original logic
                            tagMatches = clause.TagTypeEnum switch
                            {
                                MotelyTagType.SmallBlind => VectorEnum256.Equals(smallTag, clause.TagEnum.Value),
                                MotelyTagType.BigBlind => VectorEnum256.Equals(bigTag, clause.TagEnum.Value),
                                _ => VectorEnum256.Equals(smallTag, clause.TagEnum.Value) | VectorEnum256.Equals(bigTag, clause.TagEnum.Value)
                            };
                        }
                        else
                        {
                            tagMatches = VectorMask.NoBitsSet;
                        }
                        
                        // Accumulate results for this clause across all antes (OR logic)
                        clauseMasks[clauseIndex] |= tagMatches;
                    }
                }
                
                // OPTIMIZED: Early exit check after each ante (like joker filter)
                bool canEarlyExit = false;
                for (int i = 0; i < _clauses.Count; i++)
                {
                    var clause = _clauses[i];
                    // Check if this clause has any antes left to check
                    bool hasAntesRemaining = false;
                    if (clause.EffectiveAntes != null)
                    {
                        foreach (var futureAnte in clause.EffectiveAntes)
                        {
                            if (futureAnte > ante)
                            {
                                hasAntesRemaining = true;
                                break;
                            }
                        }
                    }
                    
                    // If this clause has no matches and no antes left to check, we can exit
                    if (clauseMasks[i].IsAllFalse() && !hasAntesRemaining)
                    {
                        canEarlyExit = true;
                        break;
                    }
                }
                
                if (canEarlyExit)
                {
                    DebugLogger.Log("[TAG FILTER] Early exit - clause cannot be satisfied");
                    return VectorMask.NoBitsSet;
                }
            }

            // All clauses must be satisfied (AND logic)
            // CRITICAL FIX: If any clause found nothing (NoBitsSet), the entire filter fails!
            var resultMask = VectorMask.AllBitsSet;
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                DebugLogger.Log($"[TAG FILTER] Clause {i} mask: {clauseMasks[i].Value:X}");
                
                // FIX: If this clause found nothing across all antes, fail immediately
                if (clauseMasks[i].IsAllFalse())
                {
                    DebugLogger.Log($"[TAG FILTER] Clause {i} found no matches - failing all seeds");
                    return VectorMask.NoBitsSet;
                }
                
                resultMask &= clauseMasks[i];
                DebugLogger.Log($"[TAG FILTER] Result after clause {i}: {resultMask.Value:X}");
                if (resultMask.IsAllFalse()) 
                {
                    DebugLogger.Log("[TAG FILTER] All false after AND - returning NoBitsSet");
                    return VectorMask.NoBitsSet;
                }
            }
            
            DebugLogger.Log($"[TAG FILTER] Vectorized result: {resultMask.Value:X}");
            
            // OPTIMIZED: Add individual seed verification (like joker filter) to ensure correctness
            if (resultMask.IsAllFalse())
            {
                return VectorMask.NoBitsSet;
            }
            
            // Verify each passing seed individually to avoid SIMD bugs
            var clauses = _clauses; // Copy to local for lambda capture
            return ctx.SearchIndividualSeeds(resultMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // Re-check all clauses for this individual seed
                foreach (var clause in clauses)
                {
                    bool clauseSatisfied = false;
                    
                    // Check all antes for this clause
                    foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
                    {
                        var tagStream = singleCtx.CreateTagStream(ante);
                        var smallTag = singleCtx.GetNextTag(ref tagStream);
                        var bigTag = singleCtx.GetNextTag(ref tagStream);
                        
                        bool tagMatches = false;
                        
                        if (clause.TagEnums != null && clause.TagEnums.Count > 0)
                        {
                            // Multi-value check
                            foreach (var tagEnum in clause.TagEnums)
                            {
                                bool singleMatch = clause.TagTypeEnum switch
                                {
                                    MotelyTagType.SmallBlind => smallTag == tagEnum,
                                    MotelyTagType.BigBlind => bigTag == tagEnum,
                                    _ => smallTag == tagEnum || bigTag == tagEnum
                                };
                                if (singleMatch)
                                {
                                    tagMatches = true;
                                    break;
                                }
                            }
                        }
                        else if (clause.TagEnum.HasValue)
                        {
                            // Single value check
                            tagMatches = clause.TagTypeEnum switch
                            {
                                MotelyTagType.SmallBlind => smallTag == clause.TagEnum.Value,
                                MotelyTagType.BigBlind => bigTag == clause.TagEnum.Value,
                                _ => smallTag == clause.TagEnum.Value || bigTag == clause.TagEnum.Value
                            };
                        }
                        
                        if (tagMatches)
                        {
                            clauseSatisfied = true;
                            break;
                        }
                    }
                    
                    if (!clauseSatisfied)
                        return false; // This seed doesn't satisfy this clause
                }
                
                return true; // All clauses satisfied
            });
        }
        
        private static (int minAnte, int maxAnte) CalculateAnteRange(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses)
        {
            int minAnte = int.MaxValue;
            int maxAnte = int.MinValue;
            
            foreach (var clause in clauses)
            {
                if (clause.EffectiveAntes != null)
                {
                    foreach (var ante in clause.EffectiveAntes)
                    {
                        minAnte = Math.Min(minAnte, ante);
                        maxAnte = Math.Max(maxAnte, ante);
                    }
                }
            }
            
            // Default to reasonable range if no antes specified
            if (minAnte == int.MaxValue)
            {
                minAnte = 1;
                maxAnte = 8;
            }
            
            return (minAnte, maxAnte);
        }
    }
}