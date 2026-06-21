using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class StartingDrawClause : JamlClause
{
    public MotelyStandardcardRank? Rank { get; set; }
    public MotelyStandardcardSuit? Suit { get; set; }
}

public struct StartingDrawFilterDesc(StartingDrawClause clause)
    : IMotelySeedFilterDesc<StartingDrawFilterDesc.StartingDrawFilter>
{
    private readonly StartingDrawClause _clause = clause;

    public StartingDrawFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        return new StartingDrawFilter(_clause);
    }

    public struct StartingDrawFilter(StartingDrawClause clause) : IMotelySeedFilter
    {
        private readonly StartingDrawClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var clause = _clause; // Capture in local for lambda
            return ctx.SearchIndividualSeeds(
                (ref MotelySingleSearchContext ctx) =>
                {
                    int matchCount = 0;

                    foreach (int ante in clause.Antes)
                    {
                        MotelyItem[] deck = new MotelyItem[
                            MotelyEnum<MotelyStandardCard>.ValueCount
                        ];
                        for (int i = 0; i < deck.Length; i++)
                        {
                            deck[i] = new(MotelyEnum<MotelyStandardCard>.Values[i]);
                        }

                        ctx.Shuffle("nr1", deck);

                        int handSize = Math.Min(8, deck.Length);
                        for (int i = 0; i < handSize; i++)
                        {
                            var card = deck[deck.Length - handSize + i];

                            bool rankMatch =
                                !clause.Rank.HasValue || card.StandardcardRank == clause.Rank.Value;
                            bool suitMatch =
                                !clause.Suit.HasValue || card.StandardcardSuit == clause.Suit.Value;

                            if (rankMatch && suitMatch)
                                matchCount++;
                        }
                    }

                    return matchCount >= clause.Min;
                }
            );
        }
    }
}
