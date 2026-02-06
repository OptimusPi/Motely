namespace Motely.Repository;

/// <summary>
/// Abstraction for persisting sequential search meta (resume position, active state).
/// Desktop: Motely.DB implements this (DuckDB). Browser: host sets a no-op or does not set (optional).
/// </summary>
public interface ISequentialSearchMetaStore
{
    SearchMeta? GetSearchMeta(string searchId);
    void UpsertSearchMeta(SearchMeta meta);
    void UpdateLastSeed(string searchId, string lastSeed, long totalSeedsSearched, long matchingSeeds);
    void SetSearchActive(string searchId, bool active);
    List<string> GetAllActiveSearchIds();
}
