using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

internal static class EventFilterUtils
{
    // The engine owns the stream walk. A caller only says WHAT to read — one roll, advancing
    // the stream by exactly one. No replay loop, no rollIndex: those lived in every caller as
    // an identical (and O(n²), and multi-roll-buggy) block. Now there's one linear walk here.
    internal delegate VectorMask RollRead(
        ref MotelyVectorSearchContext ctx,
        ref MotelyVectorPrngStream stream
    );

    internal delegate VectorMask RollReadWithValue(
        ref MotelyVectorSearchContext ctx,
        ref MotelyVectorPrngStream stream,
        Vector256<int> value
    );

    // Valueless variant forwards to the one real loop with an ignored value, so there is a
    // single walk body.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static VectorMask ProcessRollClause<TClause>(
        ref MotelyVectorSearchContext ctx,
        TClause clause,
        RollRead read,
        ref MotelyVectorPrngStream stream
    )
        where TClause : RollClause
            => ProcessRollClause(
                ref ctx,
                clause,
                (ref MotelyVectorSearchContext c, ref MotelyVectorPrngStream s, Vector256<int> _)
                    => read(ref c, ref s),
                ref stream,
                default
            );

    internal static VectorMask ProcessRollClause<TClause>(
        ref MotelyVectorSearchContext ctx,
        TClause clause,
        RollReadWithValue read,
        ref MotelyVectorPrngStream stream,
        Vector256<int> value
    )
        where TClause : RollClause
    {
        var rolls = clause.Rolls;
        Debug.Assert(
            rolls.Length > 0,
            "Event roll clause must provide at least one roll index."
        );

        // Sorted copy (clause-sized, stack) so we walk the stream once in index order and tick
        // off requested rolls as we pass them. Each roll is read at its TRUE stream position —
        // the old per-caller replay re-walked from 0 every time, double-advancing for multi-roll.
        Span<int> sorted = stackalloc int[rolls.Length];
        rolls.CopyTo(sorted);
        sorted.Sort();
        int maxRoll = sorted[^1];

        var matchCounts = Vector256<int>.Zero;
        var minVector = Vector256.Create(clause.Min);
        int total = rolls.Length;

        int p = 0; // pointer into sorted requested rolls
        int seen = 0; // requested rolls counted so far
        for (int idx = 0; idx <= maxRoll; idx++)
        {
            // Advance the stream by exactly one roll — single linear walk.
            var rollMask = read(ref ctx, ref stream, value);

            // Only count indices the clause actually asked for.
            if (p >= sorted.Length || idx != sorted[p])
                continue;
            while (p < sorted.Length && sorted[p] == idx)
                p++; // tolerate duplicate indices

            seen++;
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

            if (total > 8)
            {
                int rollsRemaining = total - seen;
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
