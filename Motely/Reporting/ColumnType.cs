namespace Motely.Reporting;

/// <summary>
/// Discriminator for column output types and behaviors
/// </summary>
public enum ColumnType
{
    /// <summary>
    /// Arithmetic sum of a specific signal. Output: Integer.
    /// Example: WeeJoker_Count: 2
    /// </summary>
    ScoreTally,

    /// <summary>
    /// Outputs a static or dynamic string based on a condition, or joins multiple string matches.
    /// Output: String (quoted CSV value).
    /// Example: Wee_Edition: "Negative" or Deck_Tag: "Anaglyph"
    /// </summary>
    InlineLabel,

    /// <summary>
    /// Reports the Ante number(s) where a specific event occurred.
    /// Output: Integer or Array String [2, 4].
    /// Example: Perkeo_Ante: 2
    /// </summary>
    AnteDisplay,

    /// <summary>
    /// Reports the specific names of items found (Jokers, Tarots, etc.) passing a filter.
    /// Output: String.
    /// Example: Rare_Joker_Found: "Blueprint" (where filter was Rarity == Rare)
    /// </summary>
    ItemDisplay,

    /// <summary>
    /// Outputs a string value directly (value mode) or from a function (function mode).
    /// Output: String (CSV-safe, JSON for multi-ante).
    /// Example: value mode: "Custom Label"
    /// Example: function mode: "StartingDeck" -> "2_H,2_H,2_H,5_C"
    /// Example: function mode: "CardDraw" with antes: [1], cards: [0,1,2,3,4] -> "5_H,6_C,7_D,8_S,9_H"
    /// </summary>
    ValueFunction,
}
