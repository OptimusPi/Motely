using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace Motely.Filters;

/// <summary>
/// Configuration for JAML-powered Minigames (UI Layout, Hero Cards, Metadata).
/// </summary>
public class MinigameConfig
{
    [JsonPropertyName("id")]
    [YamlMember(Alias = "id")]
    public string? Id { get; set; }

    [JsonPropertyName("epoch")]
    [YamlMember(Alias = "epoch")]
    public DateTime? Epoch { get; set; }

    [JsonPropertyName("title")]
    [YamlMember(Alias = "title")]
    public string? Title { get; set; }

    [JsonPropertyName("layout")]
    [YamlMember(Alias = "layout")]
    public List<string>? Layout { get; set; }

    [JsonPropertyName("heroCards")]
    [YamlMember(Alias = "heroCards")]
    public List<MinigameHeroCard>? HeroCards { get; set; }

    [JsonPropertyName("rules")]
    [YamlMember(Alias = "rules")]
    public List<MinigameRule>? Rules { get; set; }

    [JsonPropertyName("scoring")]
    [YamlMember(Alias = "scoring")]
    public List<MinigameScoring>? Scoring { get; set; }
}

public class MinigameRule
{
    [JsonPropertyName("id")]
    [YamlMember(Alias = "id")]
    public string? Id { get; set; }

    [JsonPropertyName("text")]
    [YamlMember(Alias = "text")]
    public string? Text { get; set; }
}

public class MinigameScoring
{
    [JsonPropertyName("type")]
    [YamlMember(Alias = "type")]
    public string? Type { get; set; }

    [JsonPropertyName("points")]
    [YamlMember(Alias = "points")]
    public int Points { get; set; }

    [JsonPropertyName("mult")]
    [YamlMember(Alias = "mult")]
    public int Mult { get; set; }
}

public class MinigameHeroCard
{
    [JsonPropertyName("ante")]
    [YamlMember(Alias = "ante")]
    public int Ante { get; set; }

    [JsonPropertyName("title")]
    [YamlMember(Alias = "title")]
    public string? Title { get; set; }

    [JsonPropertyName("items")]
    [YamlMember(Alias = "items")]
    public List<string>? Items { get; set; }
}
