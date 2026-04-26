using System.Diagnostics;

namespace Motely;

public struct ShuffleFinderFilterDesc()
    : IMotelySeedFilterDesc<ShuffleFinderFilterDesc.ShuffleFinderFilter>
{
    public readonly ShuffleFinderFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        return new ShuffleFinderFilter();
    }

    public struct ShuffleFinderFilter() : IMotelySeedFilter
    {
        private static readonly MotelyStandardcardRank[] straightRankOrder =
        [
            MotelyStandardcardRank.Ace,
            MotelyStandardcardRank.Two,
            MotelyStandardcardRank.Three,
            MotelyStandardcardRank.Four,
            MotelyStandardcardRank.Five,
            MotelyStandardcardRank.Six,
            MotelyStandardcardRank.Seven,
            MotelyStandardcardRank.Eight,
            MotelyStandardcardRank.Nine,
            MotelyStandardcardRank.Ten,
            MotelyStandardcardRank.Jack,
            MotelyStandardcardRank.Queen,
            MotelyStandardcardRank.King,
            MotelyStandardcardRank.Ace,
        ];

        public enum HandType
        {
            HighCard,
            Pair,
            TwoPair,
            ThreeOfAKind,
            Straight,
            Flush,
            FullHouse,
            FourOfAKind,
            StriaghtFlush,
        }

        public struct HandInfo(HandType type, int chips, int mult)
        {
            public HandType Type = type;
            public int Chips = chips,
                Mult = mult;

            public readonly double Score => Chips * Mult;

            public readonly double PlasmaScore
            {
                get
                {
                    double floor = Math.Floor((double)(Chips + Mult));
                    return floor * floor;
                }
            }
        }

        public static HandInfo BestScore(Span<MotelyItem> hand)
        {
            hand.Sort((a, b) => ((int)a.StandardcardRank) - ((int)b.StandardcardRank));

            int clubSuitCount = 0;
            int diamondSuitCount = 0;
            int heartSuitCount = 0;
            int spadeSuitCount = 0;

            int bestScore = 0;
            int bestScoreChips = 0,
                bestScoreMult = 0;
            HandType bestHand = HandType.HighCard;

            int[] cardCounts = new int[MotelyEnum<MotelyStandardcardRank>.ValueCount];

            for (int i = 0; i < hand.Length; i++)
            {
                switch (hand[i].StandardcardSuit)
                {
                    case MotelyStandardcardSuit.Clubs:
                        ++clubSuitCount;
                        break;
                    case MotelyStandardcardSuit.Diamonds:
                        ++diamondSuitCount;
                        break;
                    case MotelyStandardcardSuit.Hearts:
                        ++heartSuitCount;
                        break;
                    case MotelyStandardcardSuit.Spades:
                        ++spadeSuitCount;
                        break;
                }

                MotelyStandardcardRank rank = hand[i].StandardcardRank;

                int rankCount = ++cardCounts[(int)rank];

                int chips = 0,
                    mult = 0;
                HandType handType = HandType.HighCard;

                switch (rankCount)
                {
                    case 1:
                        {
                            // High card
                            chips = 5 + GetCardChips(rank);
                            mult = 1;
                            handType = HandType.HighCard;
                            break;
                        }
                    case 2:
                        {
                            // Pair
                            chips = 10 + 2 * GetCardChips(rank);
                            mult = 2;
                            handType = HandType.Pair;
                            break;
                        }
                    case 3:
                        {
                            // Three of a kind
                            chips = 30 + 3 * GetCardChips(rank);
                            mult = 3;
                            handType = HandType.ThreeOfAKind;
                            break;
                        }
                    case 4:
                        {
                            // Four of a kind
                            chips = 60 + 4 * GetCardChips(rank);
                            mult = 7;
                            handType = HandType.FourOfAKind;
                            break;
                        }
                }

                if (mult * chips > bestScore)
                {
                    bestScoreChips = chips;
                    bestScoreMult = mult;
                    bestScore = bestScoreChips * bestScoreMult;
                    bestHand = handType;
                }
            }

            const int strightStartingCardCount = 10;

            bool[] straightStart = new bool[strightStartingCardCount];
            bool hasStraight = false;

            for (int i = 0; i < strightStartingCardCount; i++)
            {
                bool matches = true;

                for (int j = 0; j < 5; j++)
                {
                    if (cardCounts[(int)straightRankOrder[i + j]] == 0)
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    // Straight

                    straightStart[i] = true;
                    hasStraight |= true;

                    int chips = 30;

                    for (int j = 0; j < 5; j++)
                    {
                        chips += GetCardChips(straightRankOrder[i + j]);
                    }

                    if (chips * 4 > bestScore)
                    {
                        bestScoreChips = chips;
                        bestScoreMult = 4;
                        bestScore = bestScoreChips * bestScoreMult;
                        bestHand = HandType.Straight;
                    }
                }
            }

            void ScoreFlush(Span<MotelyItem> hand, MotelyStandardcardSuit suit)
            {
                // Flush

                int chips = 35;

                int cardCount = 0;

                for (int j = 0; j < hand.Length; j++)
                {
                    if (hand[j].StandardcardSuit == suit)
                    {
                        ++cardCount;

                        chips += GetCardChips(hand[j].StandardcardRank);

                        if (cardCount == 5)
                            break;
                    }
                }

                Debug.Assert(cardCount == 5);

                if (chips * 4 > bestScore)
                {
                    bestScoreChips = chips;
                    bestScoreMult = 4;
                    bestScore = bestScoreChips * bestScoreMult;
                    bestHand = HandType.Flush;
                }
            }

            if (clubSuitCount >= 5)
                ScoreFlush(hand, MotelyStandardcardSuit.Clubs);
            if (diamondSuitCount >= 5)
                ScoreFlush(hand, MotelyStandardcardSuit.Diamonds);
            if (heartSuitCount >= 5)
                ScoreFlush(hand, MotelyStandardcardSuit.Hearts);
            if (spadeSuitCount >= 5)
                ScoreFlush(hand, MotelyStandardcardSuit.Spades);

            void SearchForStraightFlush(Span<MotelyItem> hand, MotelyStandardcardSuit suit)
            {
                for (int i = 0; i < strightStartingCardCount; i++)
                {
                    bool matches = true;

                    for (int j = 0; j < 5; j++)
                    {
                        if (
                            !CardMatches(
                                hand,
                                card =>
                                    card.StandardcardRank == straightRankOrder[i + j]
                                    && card.StandardcardSuit == suit
                            )
                        )
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                    {
                        // Straight flush

                        int chips = 100;

                        for (int j = 0; j < 5; j++)
                        {
                            chips += GetCardChips(straightRankOrder[i + j]);
                        }

                        if (chips * 8 > bestScore)
                        {
                            bestScoreChips = chips;
                            bestScoreMult = 8;
                            bestScore = bestScoreChips * bestScoreMult;
                            bestHand = HandType.StriaghtFlush;
                        }
                    }
                }
            }

            if (hasStraight)
            {
                if (clubSuitCount >= 5)
                    SearchForStraightFlush(hand, MotelyStandardcardSuit.Clubs);
                if (diamondSuitCount >= 5)
                    SearchForStraightFlush(hand, MotelyStandardcardSuit.Diamonds);
                if (heartSuitCount >= 5)
                    SearchForStraightFlush(hand, MotelyStandardcardSuit.Hearts);
                if (spadeSuitCount >= 5)
                    SearchForStraightFlush(hand, MotelyStandardcardSuit.Spades);
            }

            {
                int threeRank = -1;
                int twoRankA = -1;
                int twoRankB = -1;

                for (int i = cardCounts.Length - 1; i >= 0; i--)
                {
                    int cardCount = cardCounts[i];

                    if (cardCount >= 2)
                    {
                        if (twoRankA == -1)
                            twoRankA = i;
                        else if (twoRankB == -1)
                            twoRankB = i;

                        if (cardCount == 3)
                        {
                            if (threeRank == -1)
                                threeRank = i;
                        }
                    }
                }

                if (threeRank != -1 && twoRankA != -1)
                {
                    // Full House
                    int chips =
                        40
                        + 3 * GetCardChips((MotelyStandardcardRank)threeRank)
                        + 2 * GetCardChips((MotelyStandardcardRank)twoRankA);
                    if (chips * 4 > bestScore)
                    {
                        bestScoreChips = chips;
                        bestScoreMult = 4;
                        bestScore = bestScoreChips * bestScoreMult;
                        bestHand = HandType.FullHouse;
                    }
                }

                if (twoRankA != -1 && twoRankB != -1)
                {
                    // Two Pair
                    int chips =
                        20
                        + 2 * GetCardChips((MotelyStandardcardRank)twoRankA)
                        + 2 * GetCardChips((MotelyStandardcardRank)twoRankB);
                    if (chips * 2 > bestScore)
                    {
                        bestScoreChips = chips;
                        bestScoreMult = 2;
                        bestScore = bestScoreChips * bestScoreMult;
                        bestHand = HandType.TwoPair;
                    }
                }
            }

            return new(bestHand, bestScoreChips, bestScoreMult);
        }

        private static int GetCardChips(MotelyStandardcardRank rank)
        {
            return rank switch
            {
                MotelyStandardcardRank.Ace => 11,
                MotelyStandardcardRank.King => 10,
                MotelyStandardcardRank.Queen => 10,
                MotelyStandardcardRank.Jack => 10,
                MotelyStandardcardRank.Ten => 10,
                MotelyStandardcardRank.Nine => 9,
                MotelyStandardcardRank.Eight => 8,
                MotelyStandardcardRank.Seven => 7,
                MotelyStandardcardRank.Six => 6,
                MotelyStandardcardRank.Five => 5,
                MotelyStandardcardRank.Four => 4,
                MotelyStandardcardRank.Three => 3,
                MotelyStandardcardRank.Two => 2,
                _ => throw new InvalidOperationException(),
            };
        }

        private static bool CardMatches(Span<MotelyItem> hand, Predicate<MotelyItem> predicate)
        {
            foreach (MotelyItem item in hand)
            {
                if (predicate(item))
                    return true;
            }
            return false;
        }

        /*

        576 23 8
        596 20 14
        

        */

        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            return searchContext.SearchIndividualSeeds(
                (ref MotelySingleSearchContext searchContext) =>
                {
                    MotelyItem[] deck = new MotelyItem[MotelyEnum<MotelyStandardCard>.ValueCount];

                    for (int i = 0; i < deck.Length; i++)
                    {
                        deck[i] = new(MotelyEnum<MotelyStandardCard>.Values[i]);
                    }

                    searchContext.Shuffle("nr1", deck);

                    // Span<MotelyItem> hand = deck.AsSpan().Slice(deck.Length - 8, 8);
                    // double handScore = BestScore(hand).Score;

                    // if (handScore < 285 || handScore > 294)
                    //     return false;

                    // hand = deck.AsSpan().Slice(deck.Length - 16, 8);

                    // handScore = BestScore(hand).Score;

                    // return handScore == 1208;

                    Span<MotelyItem> hand = deck.AsSpan().Slice(deck.Length - 13, 13);

                    // int sixCount = 0, fiveCount = 0, sevenCount = 0, threeCount = 0;
                    int fiveCount = 0,
                        sevenCount = 0,
                        threeCount = 0;

                    foreach (MotelyItem item in hand)
                    {
                        switch (item.StandardcardRank)
                        {
                            case MotelyStandardcardRank.Seven:
                                ++sevenCount;
                                break;
                            case MotelyStandardcardRank.Five:
                                ++fiveCount;
                                break;
                            // case MotelyStandardcardRank.Six:
                            //     ++sixCount;
                            //     break;
                            case MotelyStandardcardRank.Three:
                                ++threeCount;
                                break;
                        }
                    }
                    // double handScore = BestScore(hand).Score;

                    if (fiveCount < 2 || sevenCount < 2 || threeCount < 2)
                        return false;

                    hand = deck.AsSpan().Slice(deck.Length - 21, 8);

                    return BestScore(hand).Score == 1208; // Royal flush
                }
            );
        }
    }
}
