using System.Linq;
using Motely.Filters;

namespace Motely.Reporting;

/// <summary>
/// Scorer for ValueFunction columns that supports value mode (direct string) and function mode (computed values)
/// </summary>
public class ValueFunctionScorer : IScorer
{
    private readonly MotelyJsonConfig.MotelyJsonFilterClause _clause;

    public ValueFunctionScorer(MotelyJsonConfig.MotelyJsonFilterClause clause)
    {
        _clause = clause ?? throw new ArgumentNullException(nameof(clause));
    }

    public object? Evaluate(ref MotelySingleSearchContext ctx, ref MotelyRunState runState)
    {
        var mode = _clause.Mode?.ToLowerInvariant();

        // Value mode: return the value directly
        if (mode == "value")
        {
            return _clause.Value ?? "";
        }

        // Function mode: call the specified function
        if (mode == "function")
        {
            var functionName = _clause.Function?.ToLowerInvariant();
            return functionName switch
            {
                "startingdeck" => GetStartingDeck(ref ctx),
                "carddraw" => GetCardDraw(ref ctx, _clause),
                _ => throw new ArgumentException($"Unknown function: {_clause.Function}"),
            };
        }

        // Default: treat as value mode if no mode specified but has value
        if (!string.IsNullOrEmpty(_clause.Value))
        {
            return _clause.Value;
        }

        return "";
    }

    /// <summary>
    /// Gets the starting deck composition for Erratic Deck
    /// Format: "2_H,2_H,2_H,5_C" (comma-separated card strings)
    /// </summary>
    private static string GetStartingDeck(ref MotelySingleSearchContext ctx)
    {
        if (ctx.Deck != MotelyDeck.Erratic)
        {
            return ""; // Only Erratic Deck has a starting deck
        }

        var deckStream = ctx.CreateErraticDeckPrngStream(isCached: false);
        var deckCards = new List<string>();

        for (int i = 0; i < 52; i++)
        {
            var card = ctx.GetNextErraticDeckCard(ref deckStream);
            deckCards.Add(FormatCardString(card.PlayingCardRank, card.PlayingCardSuit));
        }

        return string.Join(",", deckCards);
    }

    /// <summary>
    /// Gets the card draw order for specified antes and card positions
    /// Format: "5_H,6_C,7_D" (comma-separated card strings)
    /// For multiple antes, returns Dictionary keyed by ante number
    /// </summary>
    private static object GetCardDraw(
        ref MotelySingleSearchContext ctx,
        MotelyJsonConfig.MotelyJsonFilterClause clause
    )
    {
        var antes = clause.Antes ?? new[] { 1, 2, 3, 4, 5, 6, 7, 8 }; // Default to all antes
        var cardPositions = clause.Cards; // Optional: if null, return all 52 cards

        // If only one ante, return string directly
        if (antes.Length == 1)
        {
            return GetCardDrawForAnte(ref ctx, antes[0], cardPositions);
        }

        // Multiple antes: return Dictionary<int, string>
        var result = new Dictionary<int, string>();
        foreach (var ante in antes)
        {
            var drawOrder = GetCardDrawForAnte(ref ctx, ante, cardPositions);
            if (!string.IsNullOrEmpty(drawOrder))
            {
                result[ante] = drawOrder;
            }
        }

        return result.Count > 0 ? result : (object)"";
    }

    /// <summary>
    /// Gets the card draw order for a specific ante
    /// </summary>
    private static string GetCardDrawForAnte(
        ref MotelySingleSearchContext ctx,
        int ante,
        int[]? cardPositions
    )
    {
        var drawCards = new List<string>();

        if (ctx.Deck == MotelyDeck.Erratic)
        {
            // For Erratic Deck, cards are drawn from the starting deck in order
            var deckStream = ctx.CreateErraticDeckPrngStream(isCached: false);

            if (cardPositions != null && cardPositions.Length > 0)
            {
                // Get specific card positions
                var allCards = new List<MotelyItem>();
                for (int i = 0; i < 52; i++)
                {
                    allCards.Add(ctx.GetNextErraticDeckCard(ref deckStream));
                }

                foreach (var pos in cardPositions.OrderBy(p => p))
                {
                    if (pos >= 0 && pos < allCards.Count)
                    {
                        var card = allCards[pos];
                        drawCards.Add(FormatCardString(card.PlayingCardRank, card.PlayingCardSuit));
                    }
                }
            }
            else
            {
                // Get all 52 cards
                for (int i = 0; i < 52; i++)
                {
                    var card = ctx.GetNextErraticDeckCard(ref deckStream);
                    drawCards.Add(FormatCardString(card.PlayingCardRank, card.PlayingCardSuit));
                }
            }
        }
        else
        {
            // For standard decks, cards come from Standard Packs opened in this ante
            var packStream = ctx.CreateBoosterPackStream(ante);
            int maxPacks = ante == 1 ? 4 : 6;

            var standardStream = ctx.CreateStandardPackCardStream(ante);
            var allCards = new List<MotelyItem>();

            // Collect all cards from Standard Packs in this ante
            for (int i = 0; i < maxPacks; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (pack.GetPackType() == MotelyBoosterPackType.Standard)
                {
                    var packSize = pack.GetPackSize();
                    var packContents = ctx.GetNextStandardPackContents(
                        ref standardStream,
                        packSize
                    );

                    foreach (var item in packContents.AsArray())
                    {
                        if (item.TypeCategory == MotelyItemTypeCategory.PlayingCard)
                        {
                            allCards.Add(item);
                        }
                    }
                }
            }

            if (cardPositions != null && cardPositions.Length > 0)
            {
                // Get specific card positions
                foreach (var pos in cardPositions.OrderBy(p => p))
                {
                    if (pos >= 0 && pos < allCards.Count)
                    {
                        var card = allCards[pos];
                        drawCards.Add(FormatCardString(card.PlayingCardRank, card.PlayingCardSuit));
                    }
                }
            }
            else
            {
                // Get all cards
                foreach (var card in allCards)
                {
                    drawCards.Add(FormatCardString(card.PlayingCardRank, card.PlayingCardSuit));
                }
            }
        }

        return string.Join(",", drawCards);
    }

    /// <summary>
    /// Formats a card as "2_H" or "K_C" format
    /// </summary>
    private static string FormatCardString(MotelyPlayingCardRank rank, MotelyPlayingCardSuit suit)
    {
        var rankStr = rank switch
        {
            MotelyPlayingCardRank.Two => "2",
            MotelyPlayingCardRank.Three => "3",
            MotelyPlayingCardRank.Four => "4",
            MotelyPlayingCardRank.Five => "5",
            MotelyPlayingCardRank.Six => "6",
            MotelyPlayingCardRank.Seven => "7",
            MotelyPlayingCardRank.Eight => "8",
            MotelyPlayingCardRank.Nine => "9",
            MotelyPlayingCardRank.Ten => "10",
            MotelyPlayingCardRank.Jack => "J",
            MotelyPlayingCardRank.Queen => "Q",
            MotelyPlayingCardRank.King => "K",
            MotelyPlayingCardRank.Ace => "A",
            _ => rank.ToString(),
        };
        var suitStr = suit switch
        {
            MotelyPlayingCardSuit.Clubs => "C",
            MotelyPlayingCardSuit.Diamonds => "D",
            MotelyPlayingCardSuit.Hearts => "H",
            MotelyPlayingCardSuit.Spades => "S",
            _ => suit.ToString(),
        };
        return $"{rankStr}_{suitStr}";
    }
}
