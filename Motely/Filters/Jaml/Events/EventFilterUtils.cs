using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

internal static class EventFilterUtils
{
    internal delegate VectorMask RollChecker(
        ref MotelyVectorSearchContext ctx,
        ref MotelyVectorPrngStream stream,
        int rollIndex
    );

    internal delegate VectorMask RollCheckerWithValue(
        ref MotelyVectorSearchContext ctx,
        ref MotelyVectorPrngStream stream,
        int rollIndex,
        Vector256<int> value
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static VectorMask ProcessRollClause<TClause>(
        ref MotelyVectorSearchContext ctx,
        TClause clause,
        RollChecker checker,
        ref MotelyVectorPrngStream stream
    )
        where TClause : RollClause
    {
        Debug.Assert(
            clause.Rolls.Length > 0,
            "Event roll clause must provide at least one roll index."
        );

        var matchCounts = Vector256<int>.Zero;
        var minVector = Vector256.Create(clause.Min);
        var rolls = clause.Rolls;
        for (int i = 0; i < rolls.Length; i++)
        {
            var rollIndex = rolls[i];
            var rollMask = checker(ref ctx, ref stream, rollIndex);
            matchCounts = Vector256.Add(
                matchCounts,
                Vector256.Create(
                    rollMask[0] ? 1 : 0,
                    rollMask[1] ? 1 : 0,
                    rollMask[2] ? 1 : 0,
                    rollMask[3] ? 1 : 0,
                    rollMask[4] ? 1 : 0,
                    rollMask[5] ? 1 : 0,
                    rollMask[6] ? 1 : 0,
                    rollMask[7] ? 1 : 0
                )
            );

            if (rolls.Length > 8)
            {
                int rollsRemaining = rolls.Length - 1 - i;
                var possibleMax = Vector256.Add(matchCounts, Vector256.Create(rollsRemaining));

                var maskHit = Vector256.GreaterThanOrEqual(matchCounts, minVector);
                var maskFail = Vector256.LessThan(possibleMax, minVector);

                var combined = Vector256.BitwiseOr(maskHit, maskFail);
                if (combined.ExtractMostSignificantBits() == 0xFF)
                    break;
            }
        }

        return new VectorMask(
            MotelyVectorUtils.VectorizedComparisonToMask(
                Vector256.GreaterThan(
                    matchCounts,
                    Vector256.Subtract(Vector256.Create(clause.Min), Vector256.Create(1))
                )
            )
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static VectorMask ProcessRollClause<TClause>(
        ref MotelyVectorSearchContext ctx,
        TClause clause,
        RollCheckerWithValue checker,
        ref MotelyVectorPrngStream stream,
        Vector256<int> value
    )
        where TClause : RollClause
    {
        Debug.Assert(
            clause.Rolls.Length > 0,
            "Event roll clause must provide at least one roll index."
        );

        var matchCounts = Vector256<int>.Zero;
        var minVector = Vector256.Create(clause.Min);
        var rolls = clause.Rolls;
        for (int i = 0; i < rolls.Length; i++)
        {
            var rollIndex = rolls[i];
            var rollMask = checker(ref ctx, ref stream, rollIndex, value);
            matchCounts = Vector256.Add(
                matchCounts,
                Vector256.Create(
                    rollMask[0] ? 1 : 0,
                    rollMask[1] ? 1 : 0,
                    rollMask[2] ? 1 : 0,
                    rollMask[3] ? 1 : 0,
                    rollMask[4] ? 1 : 0,
                    rollMask[5] ? 1 : 0,
                    rollMask[6] ? 1 : 0,
                    rollMask[7] ? 1 : 0
                )
            );

            if (rolls.Length > 8)
            {
                int rollsRemaining = rolls.Length - 1 - i;
                var possibleMax = Vector256.Add(matchCounts, Vector256.Create(rollsRemaining));
                var maskHit = Vector256.GreaterThanOrEqual(matchCounts, minVector);
                var maskFail = Vector256.LessThan(possibleMax, minVector);
                var combined = Vector256.BitwiseOr(maskHit, maskFail);
                if (combined.ExtractMostSignificantBits() == 0xFF)
                    break;
            }
        }

        return new VectorMask(
            MotelyVectorUtils.VectorizedComparisonToMask(
                Vector256.GreaterThan(
                    matchCounts,
                    Vector256.Subtract(Vector256.Create(clause.Min), Vector256.Create(1))
                )
            )
        );
    }
}
