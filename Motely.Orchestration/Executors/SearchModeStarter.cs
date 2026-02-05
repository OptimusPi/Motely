using Motely.DB;
using Motely.Filters;

namespace Motely.Executors;

/// <summary>
/// Consolidates the repeated "choose search mode and start" pattern.
/// Eliminates 4 copies of the same if/else chain across CreateSearch().
/// </summary>
public static class SearchModeStarter
{
    /// <summary>
    /// Start the search with the appropriate mode based on params and seed source.
    /// </summary>
    public static IMotelySearch Start<T>(
        MotelySearchSettings<T> settings,
        JsonSearchParams p,
        string? duckDbPath
    )
        where T : struct, IMotelySeedFilter
    {
        if (p.RandomSeeds.HasValue)
            return settings.WithRandomSearch(p.RandomSeeds.Value).Start();

        if (p.SeedList != null)
            return settings.WithListSearch(p.SeedList, seedCount: -1).Start();

        if (!string.IsNullOrEmpty(duckDbPath))
            return settings
                .WithProviderSearch(new global::Motely.DB.DataLakeSeedProvider(duckDbPath))
                .Start();

        return settings.WithSequentialSearch().Start();
    }
}
