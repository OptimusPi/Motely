namespace Motely.Filters.Jaml;

public sealed class JamlConfig
{
    public string? Name { get; init; }
    public string? Author { get; init; }
    public string? Description { get; init; }
    public MotelyDeck Deck { get; init; } = MotelyDeck.Red;
    public MotelyStake Stake { get; init; } = MotelyStake.White;
    public List<string> Seeds { get; init; } = [];
    public JamlClauseSet Must { get; init; } = new();
    public JamlClauseSet Should { get; init; } = new();
    public JamlClauseSet MustNot { get; init; } = new();

    public bool HasAnyClauses => Must.HasAnyClauses || Should.HasAnyClauses || MustNot.HasAnyClauses;
}

public sealed class JamlClauseSet
{
    public List<IJamlClause> OrderedClauses { get; init; } = [];
    public bool HasAnyClauses => OrderedClauses.Count > 0;
}
