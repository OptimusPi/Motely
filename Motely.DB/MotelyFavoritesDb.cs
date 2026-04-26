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
    private readonly DuckLakeConnection _lake;
    private readonly object _lock = new();

    public MotelyFavoritesDb(string dbPath)
    {
        _lake = new DuckLakeConnection(dbPath, "favorites_lake");
        _lake.Execute("CREATE TABLE IF NOT EXISTS favorites (seed TEXT PRIMARY KEY, note TEXT, added_utc TIMESTAMP)");
    }

    public void AddFavorite(string seed, string note)
    {
        lock (_lock)
        {
            _lake.Execute(
                $"INSERT INTO favorites (seed, note, added_utc) VALUES ('{DuckLakeConnection.EscapeLiteral(seed)}', '{DuckLakeConnection.EscapeLiteral(note)}', current_timestamp) ON CONFLICT(seed) DO UPDATE SET note = '{DuckLakeConnection.EscapeLiteral(note)}'");
        }
    }

    public void RemoveFavorite(string seed)
    {
        lock (_lock)
        {
            _lake.Execute($"DELETE FROM favorites WHERE seed = '{DuckLakeConnection.EscapeLiteral(seed)}'");
        }
    }

    public bool IsFavorite(string seed)
    {
        lock (_lock)
        {
            using var cmd = _lake.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM favorites WHERE seed = '{DuckLakeConnection.EscapeLiteral(seed)}'";
            return (long)cmd.ExecuteScalar()! > 0;
        }
    }

    public IReadOnlyList<(string Seed, string Note, DateTime AddedUtc)> GetFavorites()
    {
        lock (_lock)
        {
            using var cmd = _lake.CreateCommand();
            cmd.CommandText = "SELECT seed, note, added_utc FROM favorites ORDER BY added_utc DESC";
            using var reader = cmd.ExecuteReader();
            var results = new List<(string, string, DateTime)>();
            while (reader.Read())
            {
                var seed = reader.GetString(0);
                var note = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var dt = reader.GetDateTime(2);
                results.Add((seed, note, dt));
            }
            return results;
        }
    }

    public void Dispose() => _lake.Dispose();
}
