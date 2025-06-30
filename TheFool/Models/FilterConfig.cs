using System.Text.Json.Serialization;

namespace TheFool.Models;

public class FilterConfig
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public List<string> Keywords { get; set; } = new();
    public FilterSettings Settings { get; set; } = new();
}

public class FilterSettings
{
    public string Deck { get; set; } = "";
    public string Stake { get; set; } = "";
    public bool ScoreNaturalNegatives { get; set; }
    public bool ScoreDesiredNegatives { get; set; }
    public int MinSearchAnte { get; set; } = 1;
    public int MaxSearchAnte { get; set; } = 8;
    public List<FilterCondition> Needs { get; set; } = new();
    public List<FilterCondition> Wants { get; set; } = new();
}

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
}