using System.Text.Json;

namespace Motely.DB;

public interface IFavoritesDb : IDisposable
{
    void AddFavorite(string seed, string note);
    void RemoveFavorite(string seed);
    bool IsFavorite(string seed);
    IReadOnlyList<(string Seed, string Note, DateTime AddedUtc)> GetFavorites();
}

public sealed class MotelyFavoritesDb : IFavoritesDb
{
    private readonly object _lock = new();
    private readonly Dictionary<string, (string Note, DateTime AddedUtc)> _favorites = new();

    public MotelyFavoritesDb(string dbPath)
    {
        // TODO: Map this to IndexedDB / Interop in the browser
    }

    public void AddFavorite(string seed, string note)
    {
        lock (_lock)
        {
            _favorites[seed] = (note, DateTime.UtcNow);
        }
    }

    public void RemoveFavorite(string seed)
    {
        lock (_lock)
        {
            _favorites.Remove(seed);
        }
    }

    public bool IsFavorite(string seed)
    {
        lock (_lock)
        {
            return _favorites.ContainsKey(seed);
        }
    }

    public IReadOnlyList<(string Seed, string Note, DateTime AddedUtc)> GetFavorites()
    {
        lock (_lock)
        {
            return _favorites
                .Select(kv => (kv.Key, kv.Value.Note, kv.Value.AddedUtc))
                .OrderByDescending(f => f.AddedUtc)
                .ToList();
        }
    }

    public void Dispose()
    {
    }
}
