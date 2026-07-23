using System.Diagnostics;
using System.Runtime.CompilerServices;
using Motely;

namespace Motely.Filters.Jaml;

/// <summary>
/// Fast vector filter: legendary soul stream edition only (ExcludeJokerType | ExcludeStickers).
///
/// <b>KEEP — real SIMD, not dead code.</b> Live path today often uses
/// <see cref="LegendarySoulEditionPrefilter"/> inside <see cref="LegendaryJokerFilterDesc"/> rather
/// than composing this desc as a separate filter. This type remains a valid composition form for
/// edition-only soul-stream work; do not delete. Rewire / alternate hook only with Nat OK (T4).
/// </summary>
public struct LegendarySoulEditionFilterDesc(LegendaryJokerClause clause)
    : IMotelySeedFilterDesc<LegendarySoulEditionFilterDesc.LegendarySoulEditionFilter>
{
    private readonly LegendaryJokerClause _clause = clause;

    public readonly LegendarySoulEditionFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        Debug.Assert(
            _clause.Edition.HasValue,
            "Soul edition filter requires an edition on the clause."
        );

        foreach (var ante in _clause.Antes)
            ctx.CacheLegendaryJokerStream(
                ante,
                MotelyJokerFixedRarityStreamFlags.ExcludeJokerType
                    | MotelyJokerFixedRarityStreamFlags.ExcludeStickers,
                force: true
            );

        return new LegendarySoulEditionFilter(_clause);
    }

    public readonly struct LegendarySoulEditionFilter(LegendaryJokerClause clause)
        : IMotelySeedFilter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            uint laneMask = 0;
            for (int lane = 0; lane < MotelyGlobals.MaxVectorWidth; lane++)
            {
                if (ctx.IsLaneValid(lane))
                    laneMask |= 1u << lane;
            }

            return LegendarySoulEditionPrefilter.Apply(ref ctx, clause, laneMask);
        }
    }
}
