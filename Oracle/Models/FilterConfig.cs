using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Oracle.Models;

public record FilterConfig(
    string name,
    string description,
    string author,
    List<string> keywords,
    FilterSettings filter_config
);

public record FilterSettings(
    int numNeeds,
    int numWants,
    List<FilterCondition> Needs,
    List<FilterCondition> Wants,
    int minSearchAnte,
    int maxSearchAnte,
    string deck,
    string stake,
    bool scoreNaturalNegatives,
    bool scoreDesiredNegatives
);

public class FilterCondition
{
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Edition { get; set; }
    public string? Rank { get; set; }
    public string? Suit { get; set; }
    public int[]? SearchAntes { get; set; }
    public List<int> JokerStickers { get; set; } = new();
    public bool DesireByAnte { get; set; }

    public FilterCondition() { }
    
    public FilterCondition(string type, string value, int[]? searchAntes = null, string? edition = null, string? rank = null, string? suit = null)
    {
        this.Type = type;
        this.Value = value;
        this.SearchAntes = searchAntes;
        this.Edition = edition;
        this.Rank = rank;
        this.Suit = suit;
    }

    public override string ToString()
    {
        var result = $"{Type}: {Value}";
        if (!string.IsNullOrEmpty(Edition) && Edition != "None")
            result += $" ({Edition})";
        if (!string.IsNullOrEmpty(Rank))
            result += $" {Rank}";
        if (!string.IsNullOrEmpty(Suit))
            result += $" of {Suit}";
        return result;
    }
}
