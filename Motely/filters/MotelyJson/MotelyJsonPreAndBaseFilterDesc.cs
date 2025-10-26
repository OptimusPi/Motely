using System;
using System.Collections.Generic;
using Motely.Filters;

namespace Motely.Filters
{
    /// <summary>
    /// Descriptor that ensures a set of "pre" filters (e.g. MustNot inverted filters) run BEFORE the base filter.
    /// Final pass mask = (AND of all pre filter masks) & (base filter mask).
    /// This guarantees exclusion filters that rely on untouched PRNG streams execute prior to any stream-consuming base filter.
    /// </summary>
    public readonly struct MotelyJsonPreAndBaseFilterDesc(
        IReadOnlyList<IMotelySeedFilterDesc> preFilterDescs,
        IMotelySeedFilterDesc baseFilterDesc
    ) : IMotelySeedFilterDesc<MotelyJsonPreAndBaseFilterDesc.MotelyJsonPreAndBaseFilter>
    {
        private readonly IReadOnlyList<IMotelySeedFilterDesc> _preFilterDescs = preFilterDescs;
        private readonly IMotelySeedFilterDesc _baseFilterDesc = baseFilterDesc;

        public MotelyJsonPreAndBaseFilter CreateFilter(ref MotelyFilterCreationContext ctx)
        {
            // Create pre filters with IsAdditionalFilter=false so they can cache required streams.
            bool prev = ctx.IsAdditionalFilter;
            ctx.IsAdditionalFilter = false;
            var concretePre = new List<IMotelySeedFilter>(_preFilterDescs.Count);
            foreach (var desc in _preFilterDescs)
            {
                concretePre.Add(desc.CreateFilter(ref ctx));
            }
            // Create base filter after pre filters (still with IsAdditionalFilter=false)
            var concreteBase = _baseFilterDesc.CreateFilter(ref ctx);
            ctx.IsAdditionalFilter = prev;
            return new MotelyJsonPreAndBaseFilter(concretePre, concreteBase);
        }

        public readonly struct MotelyJsonPreAndBaseFilter : IMotelySeedFilter
        {
            private readonly IReadOnlyList<IMotelySeedFilter> _preFilters;
            private readonly IMotelySeedFilter _baseFilter;

            public MotelyJsonPreAndBaseFilter(IReadOnlyList<IMotelySeedFilter> preFilters, IMotelySeedFilter baseFilter)
            {
                _preFilters = preFilters;
                _baseFilter = baseFilter;
            }

            public VectorMask Filter(ref MotelyVectorSearchContext ctx)
            {
                VectorMask preMask = VectorMask.AllBitsSet;
                // AND all pre filter masks; early exit if fully false
                foreach (var pf in _preFilters)
                {
                    var m = pf.Filter(ref ctx);
                    preMask &= m;
                    if (preMask.IsAllFalse())
                        return preMask; // nothing can pass
                }

                // Run base filter only if pre filters passed something
                var baseMask = _baseFilter.Filter(ref ctx);
                return baseMask & preMask;
            }
        }
    }
}