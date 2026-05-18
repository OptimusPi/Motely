using System.Runtime.CompilerServices;

namespace Motely.Enums;

public enum MotelyStandardcardRank
{
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
    Ace,
}

public static class MotelyStandardcardRankExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetOrdinal(this MotelyStandardcardRank rank)
    {
        return (int)rank;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyStandardcardRank FromOrdinal(int ordinal)
    {
        return (MotelyStandardcardRank)ordinal;
    }
}

public enum MotelyStandardcardSuit
{
    Clubs = 0b00 << MotelyGlobals.StandardcardSuitOffset,
    Diamonds = 0b01 << MotelyGlobals.StandardcardSuitOffset,
    Hearts = 0b10 << MotelyGlobals.StandardcardSuitOffset,
    Spades = 0b11 << MotelyGlobals.StandardcardSuitOffset,
}

public static class MotelyStandardcardSuitExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetOrdinal(this MotelyStandardcardSuit suit)
    {
        return (int)suit >> MotelyGlobals.StandardcardSuitOffset;
    }
}

public enum MotelyStandardCard
{
    TwoOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.Two,
    ThreeOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.Three,
    FourOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.Four,
    FiveOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.Five,
    SixOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.Six,
    SevenOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.Seven,
    EightOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.Eight,
    NineOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.Nine,
    TenOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.Ten,
    JackOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.Jack,
    QueenOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.Queen,
    KingOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.King,
    AceOfClubs = MotelyStandardcardSuit.Clubs | MotelyStandardcardRank.Ace,

    TwoOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.Two,
    ThreeOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.Three,
    FourOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.Four,
    FiveOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.Five,
    SixOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.Six,
    SevenOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.Seven,
    EightOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.Eight,
    NineOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.Nine,
    TenOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.Ten,
    JackOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.Jack,
    QueenOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.Queen,
    KingOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.King,
    AceOfDiamonds = MotelyStandardcardSuit.Diamonds | MotelyStandardcardRank.Ace,

    TwoOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.Two,
    ThreeOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.Three,
    FourOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.Four,
    FiveOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.Five,
    SixOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.Six,
    SevenOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.Seven,
    EightOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.Eight,
    NineOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.Nine,
    TenOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.Ten,
    JackOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.Jack,
    QueenOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.Queen,
    KingOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.King,
    AceOfHearts = MotelyStandardcardSuit.Hearts | MotelyStandardcardRank.Ace,

    TwoOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.Two,
    ThreeOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.Three,
    FourOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.Four,
    FiveOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.Five,
    SixOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.Six,
    SevenOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.Seven,
    EightOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.Eight,
    NineOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.Nine,
    TenOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.Ten,
    JackOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.Jack,
    QueenOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.Queen,
    KingOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.King,
    AceOfSpades = MotelyStandardcardSuit.Spades | MotelyStandardcardRank.Ace,
}

public static class MotelyStandardCardExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyStandardcardSuit GetSuit(this MotelyStandardCard card)
    {
        return (MotelyStandardcardSuit)((int)card & MotelyGlobals.StandardcardSuitMask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyStandardcardRank GetRank(this MotelyStandardCard card)
    {
        return (MotelyStandardcardRank)((int)card & MotelyGlobals.StandardcardRankMask);
    }
}
