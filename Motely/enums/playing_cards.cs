using System.Runtime.CompilerServices;

namespace Motely.Core;

public enum MotelyPlayingCardRank
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

public static class MotelyPlayingCardRankExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetOrdinal(this MotelyPlayingCardRank rank)
    {
        return (int)rank;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyPlayingCardRank FromOrdinal(int ordinal)
    {
        return (MotelyPlayingCardRank)ordinal;
    }
}

public enum MotelyPlayingCardSuit
{
    Clubs = 0b00 << Motely.PlayingCardSuitOffset,
    Diamonds = 0b01 << Motely.PlayingCardSuitOffset,
    Hearts = 0b10 << Motely.PlayingCardSuitOffset,
    Spades = 0b11 << Motely.PlayingCardSuitOffset,
}

public static class MotelyPlayingCardSuitExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetOrdinal(this MotelyPlayingCardSuit suit)
    {
        return (int)suit >> Motely.PlayingCardSuitOffset;
    }
}

public enum MotelyPlayingCard
{
    C2 = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.Two,
    C3 = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.Three,
    C4 = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.Four,
    C5 = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.Five,
    C6 = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.Six,
    C7 = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.Seven,
    C8 = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.Eight,
    C9 = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.Nine,
    CA = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.Ace,
    CJ = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.Jack,
    CK = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.King,
    CQ = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.Queen,
    C10 = MotelyPlayingCardSuit.Clubs | MotelyPlayingCardRank.Ten,

    D2 = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.Two,
    D3 = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.Three,
    D4 = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.Four,
    D5 = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.Five,
    D6 = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.Six,
    D7 = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.Seven,
    D8 = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.Eight,
    D9 = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.Nine,
    DA = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.Ace,
    DJ = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.Jack,
    DK = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.King,
    DQ = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.Queen,
    D10 = MotelyPlayingCardSuit.Diamonds | MotelyPlayingCardRank.Ten,

    H2 = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.Two,
    H3 = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.Three,
    H4 = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.Four,
    H5 = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.Five,
    H6 = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.Six,
    H7 = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.Seven,
    H8 = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.Eight,
    H9 = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.Nine,
    HA = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.Ace,
    HJ = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.Jack,
    HK = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.King,
    HQ = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.Queen,
    H10 = MotelyPlayingCardSuit.Hearts | MotelyPlayingCardRank.Ten,

    S2 = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.Two,
    S3 = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.Three,
    S4 = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.Four,
    S5 = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.Five,
    S6 = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.Six,
    S7 = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.Seven,
    S8 = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.Eight,
    S9 = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.Nine,
    SA = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.Ace,
    SJ = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.Jack,
    SK = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.King,
    SQ = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.Queen,
    S10 = MotelyPlayingCardSuit.Spades | MotelyPlayingCardRank.Ten,
}

public static class MotelyPlayingCardExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyPlayingCardSuit GetSuit(this MotelyPlayingCard card)
    {
        return (MotelyPlayingCardSuit)((int)card & Motely.PlayingCardSuitMask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyPlayingCardRank GetRank(this MotelyPlayingCard card)
    {
        return (MotelyPlayingCardRank)((int)card & Motely.PlayingCardRankMask);
    }
}
