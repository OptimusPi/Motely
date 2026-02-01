namespace Motely.Repository;

public static class RepositoryHost
{
    public static IMotelyRepository? Instance { get; private set; }

    public static void Set(IMotelyRepository repository)
    {
        Instance = repository ?? throw new ArgumentNullException(nameof(repository));
    }
}
