using System;
using System.Collections.Generic;
using Motely.Filters;
using Motely.Utils;

namespace Motely.Filters
{
    /// <summary>
    /// Negation filter descriptor: matches seeds that DO NOT match ANY of the provided clauses.
    /// Used to implement 'mustNot' semantics from JSON configs.
    /// </summary>
    public struct MotelyJsonNegationFilterDesc(
        List<MotelyJsonConfig.MotleyJsonFilterClause> clauses
    ) : IMotelySeedFilterDesc<MotelyJsonNegationFilterDesc.MotelyJsonNegationFilter>
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses = clauses;

        public MotelyJsonNegationFilter CreateFilter(ref MotelyFilterCreationContext ctx)
        {
            // Create a concrete IMotelySeedFilter for each individual clause (so we can OR them)
            var innerFilters = new List<IMotelySeedFilter>();

            foreach (var clause in _clauses)
            {
                DebugLogger.Log(
                    $"[NEGATION] Creating inner filter for clause Type={clause.Type}, ItemTypeEnum={clause.ItemTypeEnum}, Value={clause.Value}"
                );
                // Group single clause into a list and select specialized filter by category
                var category = FilterCategoryMapper.GetCategory(clause.ItemTypeEnum);

                // Create a specialized filter desc for this single clause
                IMotelySeedFilterDesc singleDesc = SpecializedFilterFactory.CreateSpecializedFilter(
                    category,
                    [clause]
                );

                // Create the concrete filter instance.
                // NOTE: We must create inner filters with IsAdditionalFilter = false so they register
                // any required pseudo-hash key lengths on the same context object that MotelySearch
                // will later read. Temporarily flip the flag, create the filter, then restore it.
                bool prevFlag = ctx.IsAdditionalFilter;
                ctx.IsAdditionalFilter = false;
                var concrete = singleDesc.CreateFilter(ref ctx);
                ctx.IsAdditionalFilter = prevFlag;
                innerFilters.Add(concrete);
            }

            return new MotelyJsonNegationFilter(innerFilters);
        }

        public readonly struct MotelyJsonNegationFilter : IMotelySeedFilter
        {
            private readonly List<IMotelySeedFilter> _innerFilters;

            public MotelyJsonNegationFilter(List<IMotelySeedFilter> innerFilters)
            {
                _innerFilters = innerFilters ?? new List<IMotelySeedFilter>();
            }

            public VectorMask Filter(ref MotelyVectorSearchContext ctx)
            {
                // Compute OR across all inner filters (seeds that match any mustNot clause)
                VectorMask combined = VectorMask.NoBitsSet;
                foreach (var f in _innerFilters)
                {
                    var m = f.Filter(ref ctx);
                    combined |= m;

                    // Early exit if all bits are set
                    if (combined.IsAllTrue())
                        break;
                }

                // Return complement: seeds that do NOT match any mustNot clause
                uint inverted = ~combined.Value & 0xFFu; // mask to 8 bits
                return new VectorMask(inverted);
            }
        }
    }
}
