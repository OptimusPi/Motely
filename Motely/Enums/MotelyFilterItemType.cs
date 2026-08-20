namespace Motely.Enums;

/// <summary>
/// Types of items that can be filtered in searches
/// </summary>
public enum MotelyFilterItemType
{
    Joker,
    CommonJoker,
    UncommonJoker,
    RareJoker,
    MixedJoker,
    LegendaryJoker,
    TarotCard,
    PlanetCard,
    SpectralCard,
    SmallBlindTag,
    BigBlindTag,
    Voucher,
    Standardcard,
    Boss,
    Event, // Random events (Lucky, Wheel of Fortune, Bananas, Misprint)
    ErraticRank, // Erratic Deck starting composition - rank filter
    ErraticSuit, // Erratic Deck starting composition - suit filter
    ErraticCard, // Erratic Deck starting composition - specific card filter (e.g., "K_C", "2_H")
    CavendishExtinct, // Cavendish banana extinction check
    GrosMichelExtinct, // Gros Michel banana extinction check
    StartingDraw, // Starting hand draw filter
    PokerHand, // Starting-hand poker category (Pair … StraightFlush)
    And, // Logical AND - all nested clauses must match
    Or, // Logical OR - at least one nested clause must match
}
