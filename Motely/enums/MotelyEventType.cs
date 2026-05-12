namespace Motely;

/// <summary>
/// Types of random events that can be filtered in searches
/// </summary>
public enum MotelyEventType
{
    /// <summary>Lucky card triggers $1 money drop</summary>
    LuckyMoney,

    /// <summary>Lucky card triggers +mult</summary>
    LuckyMult,

    /// <summary>Misprint joker mult value roll</summary>
    MisprintMult,

    /// <summary>Wheel of Fortune Tarot gives edition to random joker</summary>
    WheelOfFortune,

    /// <summary>Cavendish banana goes extinct (destroys itself)</summary>
    CavendishExtinct,

    /// <summary>Gros Michel banana goes extinct (destroys itself and gives Cavendish)</summary>
    GrosMichelExtinct,

    /// <summary>Space Joker levels up the played hand (1/4, PRNG key "space")</summary>
    SpaceLevelup,

    /// <summary>Business Card pays $2 when a face scores (1/2, PRNG key "business")</summary>
    BusinessPayout,

    /// <summary>Bloodstone retriggers on Hearts scored (1/2, PRNG key "bloodstone")</summary>
    BloodstoneTrigger,

    /// <summary>Reserved Parking pays $1 when a face is held in hand (1/2, PRNG key "parking")</summary>
    ParkingPayout,

    /// <summary>Glass card self-destructs after scoring (1/4, PRNG key "glass")</summary>
    GlassDestroy,

    /// <summary>The Wheel boss-blind — a card stays flipped in hand (1/7, PRNG key "wheel")</summary>
    WheelStaysFlipped,

    // NOTE: 8-Ball (spawns Tarot) and Omen Globe (substitutes Spectral) are NOT events.
    // They're item sources — wire them as TarotCard / SpectralCard source modifiers, not here.
}
