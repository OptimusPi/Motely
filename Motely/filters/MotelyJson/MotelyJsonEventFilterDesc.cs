using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on random event criteria from JSON configuration.
/// Supports: LuckyMoney, LuckyMult, MisprintMult, WheelOfFortune, CavendishExtinct, GrosMichelExtinct
/// </summary>
public struct MotelyJsonEventFilterDesc(MotelyJsonEventFilterCriteria criteria)
    : IMotelySeedFilterDesc<MotelyJsonEventFilterDesc.MotelyJsonEventFilter>
{
    private readonly MotelyJsonEventFilterCriteria _criteria = criteria;

    public MotelyJsonEventFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        return new MotelyJsonEventFilter(_criteria.Clauses);
    }

    public struct MotelyJsonEventFilter : IMotelySeedFilter
    {
        private readonly MotelyJsonEventFilterClause[] _clauses;

        public MotelyJsonEventFilter(List<MotelyJsonEventFilterClause> clauses)
        {
            _clauses = [.. clauses];
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(
                _clauses != null && _clauses.Length > 0,
                "Event filter created with empty clauses - this is a programming error!"
            );

            // Stack-allocated clause masks
            Span<VectorMask> clauseMasks = stackalloc VectorMask[_clauses.Length];
            for (int i = 0; i < clauseMasks.Length; i++)
                clauseMasks[i] = VectorMask.NoBitsSet;

            // Check each clause
            for (int clauseIndex = 0; clauseIndex < _clauses.Length; clauseIndex++)
            {
                var clause = _clauses[clauseIndex];

                // For each wanted ante
                for (int anteIndex = 0; anteIndex < clause.WantedAntes.Length; anteIndex++)
                {
                    if (!clause.WantedAntes[anteIndex])
                        continue;

                    // Check the specified roll indices for this event type (Rolls is never null; set at clause creation)
                    var rolls = clause.Rolls;
                    foreach (var rollIndex in rolls)
                    {
                        VectorMask rollMask = CheckEventRoll(
                            ref ctx,
                            clause.EventTypeEnum,
                            rollIndex
                        );
                        clauseMasks[clauseIndex] |= rollMask;
                    }
                }
            }

            // Combine all clause masks (AND logic - all clauses must match)
            VectorMask finalMask = VectorMask.AllBitsSet;
            for (int i = 0; i < _clauses.Length; i++)
            {
                finalMask &= clauseMasks[i];
            }

            return finalMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckEventRoll(
            ref MotelyVectorSearchContext ctx,
            MotelyEventType eventType,
            int rollIndex
        )
        {
            switch (eventType)
            {
                case MotelyEventType.LuckyMoney:
                {
                    var stream = ctx.CreateLuckyCardMoneyStream(true);
                    // Advance to the specific roll index
                    for (int i = 0; i < rollIndex; i++)
                        ctx.GetNextLuckyMoney(ref stream);
                    return ctx.GetNextLuckyMoney(ref stream);
                }

                case MotelyEventType.LuckyMult:
                {
                    var stream = ctx.CreateLuckyCardMultStream(true);
                    for (int i = 0; i < rollIndex; i++)
                        ctx.GetNextLuckyMult(ref stream);
                    return ctx.GetNextLuckyMult(ref stream);
                }

                case MotelyEventType.MisprintMult:
                {
                    var stream = ctx.CreateMisprintPrngStream(true);
                    // For Misprint, we check if the mult value is within a specific range
                    // User can extend this to check for specific mult values if needed
                    for (int i = 0; i < rollIndex; i++)
                        ctx.GetNextMisprintMult(ref stream);
                    var multValue = ctx.GetNextMisprintMult(ref stream);
                    // Return true if mult >= 0 (always true, user can add more specific checks)
                    return Vector256.GreaterThanOrEqual(multValue, Vector256<int>.Zero);
                }

                case MotelyEventType.WheelOfFortune:
                {
                    var stream = ctx.CreateWheelOfFortuneStream(true);
                    for (int i = 0; i < rollIndex; i++)
                        ctx.GetNextWheelOfFortune(ref stream);
                    var edition = ctx.GetNextWheelOfFortune(ref stream);
                    // Return true if any edition was applied (not None)
                    return ~VectorEnum256.Equals(edition, MotelyItemEdition.None);
                }

                case MotelyEventType.CavendishExtinct:
                {
                    var stream = ctx.CreateCavendishPrngStream(true);
                    for (int i = 0; i < rollIndex; i++)
                        ctx.GetNextCavendishExtinct(ref stream);
                    return ctx.GetNextCavendishExtinct(ref stream);
                }

                case MotelyEventType.GrosMichelExtinct:
                {
                    var stream = ctx.CreateGrosMichelPrngStream(true);
                    for (int i = 0; i < rollIndex; i++)
                        ctx.GetNextGrosMichelExtinct(ref stream);
                    return ctx.GetNextGrosMichelExtinct(ref stream);
                }

                default:
                    return VectorMask.NoBitsSet;
            }
        }
    }
}
