using System;
using System.Collections.Generic;
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
        // Tags don't need special caching - they're built into ante structure
        return new MotelyJsonTagFilter(_tagClauses);
    }

    public struct MotelyJsonTagFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses = clauses;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            DebugLogger.Log($"[TAG FILTER] Called with {_clauses?.Count ?? 0} clauses");
            
            if (_clauses == null || _clauses.Count == 0)
            {
                DebugLogger.Log("[TAG FILTER] No clauses - returning AllBitsSet");
                return VectorMask.AllBitsSet;
            }
            
            // Stack-allocated clause masks - accumulate results per clause across all antes
            Span<VectorMask> clauseMasks = stackalloc VectorMask[_clauses.Count];
            for (int i = 0; i < clauseMasks.Length; i++)
                clauseMasks[i] = VectorMask.NoBitsSet;

            // Process each clause across all its antes
            for (int clauseIndex = 0; clauseIndex < _clauses.Count; clauseIndex++)
            {
                var clause = _clauses[clauseIndex];
                
                // OR across all antes for this clause
                foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
                {
                    var clauseMask = VectorMask.NoBitsSet;
                    
                    if (clause.TagEnum.HasValue || (clause.TagEnums != null && clause.TagEnums.Count > 0))
                    {
                        var tagStream = ctx.CreateTagStream(ante);
                        var smallTag = ctx.GetNextTag(ref tagStream);
                        var bigTag = ctx.GetNextTag(ref tagStream);
                        
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
                        
                        clauseMask |= tagMatches;
                    }
                    
                    // Accumulate results for this clause across all antes (OR logic)
                    clauseMasks[clauseIndex] |= clauseMask;
                }
            }

            // All clauses must be satisfied (AND logic)
            var resultMask = VectorMask.AllBitsSet;
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                DebugLogger.Log($"[TAG FILTER] Clause {i} mask: {clauseMasks[i].Value:X}");
                resultMask &= clauseMasks[i];
                DebugLogger.Log($"[TAG FILTER] Result after clause {i}: {resultMask.Value:X}");
                if (resultMask.IsAllFalse()) 
                {
                    DebugLogger.Log("[TAG FILTER] All false after AND - returning NoBitsSet");
                    return VectorMask.NoBitsSet;
                }
            }
            
            DebugLogger.Log($"[TAG FILTER] Final result: {resultMask.Value:X}");
            return resultMask;
        }
    }
}