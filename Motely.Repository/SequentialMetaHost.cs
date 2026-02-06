namespace Motely.Repository;

/// <summary>
/// Optional host for sequential search meta store. When not set (e.g. browser), persist/restore is skipped.
/// Desktop hosts (CLI, API) set the store from Motely.DB implementation.
/// </summary>
public static class SequentialMetaHost
{
    private static ISequentialSearchMetaStore? _instance;

    public static ISequentialSearchMetaStore? Instance => _instance;

    public static void Set(ISequentialSearchMetaStore? store)
    {
        _instance = store;
    }
}
