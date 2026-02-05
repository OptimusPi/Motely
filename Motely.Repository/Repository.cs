namespace Motely.Repository;

public static class RepositoryHost
{
    private static IMotelyRepository? _instance;

    public static IMotelyRepository Instance
    {
        get =>
            _instance
            ?? throw new InvalidOperationException("Repository not initialized. Call Set() first.");
        private set => _instance = value;
    }

    public static void Set(IMotelyRepository repository)
    {
        _instance = repository ?? throw new ArgumentNullException(nameof(repository));
    }
}
