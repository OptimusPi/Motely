using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters.Jaml;

/// <summary>
/// Starting-hand poker category for a given ante/round (deck shuffle → last 8 cards → best hand).
/// Scalar-per-seed via <see cref="MotelyVectorSearchContext.SearchIndividualSeeds"/> — same shape as
/// <c>startingDraw</c> / native ShuffleFinder.
/// </summary>
[JamlDiscriminator("pokerHand", "pokerHands", ValueEnum = typeof(MotelyPokerHand))]
public sealed class PokerHandClause : IJamlClause, IAnteScopedClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public MotelyPokerHand[] Hands { get; set; } = [];
}

public struct PokerHandFilterDesc(PokerHandClause clause)
    : IMotelySeedFilterDesc<PokerHandFilterDesc.PokerHandFilter>,
      IJamlClauseDesc<PokerHandClause>
{
    private readonly PokerHandClause _clause = clause;

    /// <inheritdoc/>
    public static string[] Discriminators => ["pokerHand", "pokerHands"];

    /// <inheritdoc/>
    public static string[] ClauseKeys => ["min", "max", "score", "label", "ante", "antes"];

    /// <inheritdoc/>
    public static bool Set(PokerHandClause clause, string key, IJamlValueReader value) => false;

    /// <inheritdoc/>
    public static bool SetDiscriminatorValue(PokerHandClause clause, IJamlValueReader value)
    {
        if (!value.TryEnumArray<MotelyPokerHand>(out var hands))
            return false;
        clause.Hands = hands;
        return true;
    }

    public PokerHandFilter CreateFilter(ref MotelyFilterCreationContext ctx) =>
        new PokerHandFilter(_clause);

    public struct PokerHandFilter(PokerHandClause clause) : IMotelySeedFilter
    {
        private readonly PokerHandClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Hands.Length > 0);
            var clause = _clause;
            return ctx.SearchIndividualSeeds(
                (MotelySingleSearchContext singleCtx) =>
                    JamlScoring.ClauseMeetsMinForFilter(ref singleCtx, clause) ? 1 : 0
            );
        }
    }
}
