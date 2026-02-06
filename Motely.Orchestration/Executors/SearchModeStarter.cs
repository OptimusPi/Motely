using Motely.Filters;
using Motely.Repository;

namespace Motely.Executors;

/// <summary>
/// Consolidates the repeated "choose search mode and start" pattern.
/// Eliminates 4 copies of the same if/else chain across CreateSearch().
/// Seed source is resolved via the repository (GetSource); no DB types here.
/// </summary>
public static class SearchModeStarter
{
    /// <summary>
    /// Start the search with the appropriate mode based on params and seed source.
    /// When seedSourceMoniker is set, the provider is obtained from RepositoryHost (desktop: file/DB, browser: throws or custom impl).
    /// </summary>
    public static IMotelySearch Start<T>(
        MotelySearchSettings<T> settings,
        JsonSearchParams p,
        string? seedSourceMoniker
    )
        where T : struct, IMotelySeedFilter
    {
        if (p.RandomSeeds.HasValue)
            return settings.WithRandomSearch(p.RandomSeeds.Value).Start();

        if (p.SeedList != null)
            return settings.WithListSearch(p.SeedList, seedCount: -1).Start();

        if (!string.IsNullOrEmpty(seedSourceMoniker))
            return settings
                .WithProviderSearch(RepositoryHost.Instance.GetSource(seedSourceMoniker))
                .Start();

        return settings.WithSequentialSearch().Start();
    }
}
