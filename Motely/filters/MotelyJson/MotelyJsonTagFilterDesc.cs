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
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            var anteClauses = new Dictionary<int, List<MotelyJsonConfig.MotleyJsonFilterClause>>();
            foreach (var clause in _clauses)
            {
                foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
                {
                    if (!anteClauses.ContainsKey(ante))
                        anteClauses[ante] = new List<MotelyJsonConfig.MotleyJsonFilterClause>();
                    anteClauses[ante].Add(clause);
                }
            }
            
            var resultMask = VectorMask.AllBitsSet;
            
            foreach (var ante in anteClauses.Keys.OrderBy(x => x))
            {
                var clausesForThisAnte = anteClauses[ante];
                
                foreach (var clause in clausesForThisAnte)
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
                    
                    resultMask &= clauseMask;
                    if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
                }
            }
            
            return resultMask;
        }
    }
}