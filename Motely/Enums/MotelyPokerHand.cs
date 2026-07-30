namespace Motely.Enums;

/// <summary>
/// Poker hand categories for starting-hand / shuffle evaluation.
/// Strength order matches Balatro base chips×mult ranking used by <see cref="Motely.MotelyPokerHandEval"/>.
/// </summary>
public enum MotelyPokerHand
{
    HighCard,
    Pair,
    TwoPair,
    ThreeOfAKind,
    Straight,
    Flush,
    FullHouse,
    FourOfAKind,
    StraightFlush,
}
