using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class MisprintMultClause : RollClause
{
    /// <summary>
    /// Specific mult value to match (0-23). If null, matches any value (always succeeds).
    /// </summary>
    public int? Value { get; set; }
}

public struct MisprintMultFilterDesc(MisprintMultClause clause)
    : IMotelySeedFilterDesc<MisprintMultFilterDesc.MisprintMultFilter>
{
    private readonly MisprintMultClause _clause = clause;

    public MisprintMultFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Pre-compute at creation time - no branching in SIMD hot path
        var targetValue = _clause.Value.HasValue
            ? Vector256.Create(_clause.Value.Value)
            : Vector256<int>.Zero;
        return new MisprintMultFilter(_clause, _clause.Value.HasValue, targetValue);
    }

    public struct MisprintMultFilter(
        MisprintMultClause clause,
        bool hasValue,
        Vector256<int> targetValue
    ) : IMotelySeedFilter
    {
        private readonly MisprintMultClause _clause = clause;
        private readonly bool _hasValue = hasValue;
        private readonly Vector256<int> _targetValue = targetValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateMisprintPrngStream();

            if (_hasValue)
            {
                return EventFilterUtils.ProcessRollClause(
                    ref ctx,
                    _clause,
                    static (
                        ref MotelyVectorSearchContext sctx,
                        ref MotelyVectorPrngStream stream,
                        int rollIndex,
                        Vector256<int> target
                    ) =>
                    {
                        for (int i = 0; i < rollIndex; i++)
                            sctx.GetNextMisprintMult(ref stream);
                        var multValue = sctx.GetNextMisprintMult(ref stream);
                        return Vector256.Equals(multValue, target);
                    },
                    ref stream,
                    _targetValue
                );
            }

            // Original behavior: matches any value (always succeeds if roll exists)
            return EventFilterUtils.ProcessRollClause(
                ref ctx,
                _clause,
                static (
                    ref MotelyVectorSearchContext sctx,
                    ref MotelyVectorPrngStream stream,
                    int rollIndex
                ) =>
                {
                    for (int i = 0; i < rollIndex; i++)
                        sctx.GetNextMisprintMult(ref stream);
                    var multValue = sctx.GetNextMisprintMult(ref stream);
                    return Vector256.GreaterThanOrEqual(multValue, Vector256<int>.Zero);
                },
                ref stream
            );
        }
    }
}
