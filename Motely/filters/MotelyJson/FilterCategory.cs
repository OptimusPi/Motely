namespace Motely.Filters;

/// <summary>
/// Categories for specialized JSON filter implementations
/// </summary>
public enum FilterCategory
{
    Voucher,
    Boss,
    Tag,
    TarotCard,
    PlanetCard,
    SpectralCard,
    PlayingCard,
    Joker,
    JokerRarityEditionPreFilter, // Rarity+edition pre-filter for shop jokers (ultra-fast early-exit before precise slot checking)
    SoulJoker,
    SoulJokerEditionOnly, // Edition-only soul joker checks (Value="Any" + edition) for instant early-exit
    SoulJokerTypeOnly, // Type-specific soul joker checks (Value="Perkeo") for fast verification
    Event,
    ErraticRank, // Erratic Deck starting composition - rank filter
    ErraticSuit, // Erratic Deck starting composition - suit filter
    ErraticRankAndSuit, // Combined Erratic Deck rank+suit filter (single loop for max performance)
    And,
    Or,
}
