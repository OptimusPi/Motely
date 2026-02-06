namespace Motely.Repository;

/// <summary>
/// DTO for sequential search persistence (resume position, active state).
/// Implementation lives in Motely.DB (DuckDB); browser host may use a no-op store.
/// </summary>
public sealed class SearchMeta
{
    public string SearchId { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string? JamlFilter { get; set; }
    public string? Deck { get; set; }
    public string? Stake { get; set; }
    public string? SeedSource { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastAccessed { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? LastSeed { get; set; }
}
