using System.Text;
using DuckDB.NET.Data;

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
    private readonly DuckDBConnection _conn;
    private readonly object _lock = new();

    public MotelyFavoritesDb(string dbPath)
    {
        _conn = new DuckDBConnection("Data Source=:memory:");
        _conn.Open();

        using var cmd = _conn.CreateCommand();

        if (dbPath != ":memory:")
        {
            var (lakeDir, metaFile, dataDir) = ResolveLakePaths(dbPath);
            Directory.CreateDirectory(lakeDir);
            Directory.CreateDirectory(dataDir);

            cmd.CommandText = "INSTALL ducklake; LOAD ducklake;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = $"ATTACH 'ducklake:{EscapeSqlPath(metaFile)}' AS favorites_lake (DATA_PATH '{EscapeSqlPath(dataDir)}');";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "USE favorites_lake;";
            cmd.ExecuteNonQuery();
        }

        CreateTable();
    }

    private static (string LakeDir, string MetaFile, string DataDir) ResolveLakePaths(string dbPath)
    {
        var fullPath = Path.GetFullPath(dbPath);

        if (!Path.HasExtension(fullPath))
        {
            return (
                fullPath,
                Path.Combine(fullPath, "metadata.ducklake"),
                Path.Combine(fullPath, "data")
            );
        }

        var directory = Path.GetDirectoryName(fullPath);
        var baseName = Path.GetFileNameWithoutExtension(fullPath);
        var basePath = string.IsNullOrWhiteSpace(directory) ? Path.Combine(Directory.GetCurrentDirectory(), baseName) : Path.Combine(directory, baseName);
        var lakeDir = $"{basePath}_lake";
        return (lakeDir, Path.Combine(lakeDir, "metadata.ducklake"), Path.Combine(lakeDir, "data"));
    }

    private void CreateTable()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS favorites (seed TEXT PRIMARY KEY, note TEXT, added_utc TIMESTAMP)";
        cmd.ExecuteNonQuery();
    }

    public void AddFavorite(string seed, string note)
    {
        lock (_lock)
        {
            // Simple replace or insert for favorites
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"INSERT INTO favorites (seed, note, added_utc) VALUES ('{EscapeSqlLiteral(seed)}', '{EscapeSqlLiteral(note)}', current_timestamp) ON CONFLICT(seed) DO UPDATE SET note = '{EscapeSqlLiteral(note)}'";
            cmd.ExecuteNonQuery();
        }
    }

    public void RemoveFavorite(string seed)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM favorites WHERE seed = '{EscapeSqlLiteral(seed)}'";
            cmd.ExecuteNonQuery();
        }
    }

    public bool IsFavorite(string seed)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM favorites WHERE seed = '{EscapeSqlLiteral(seed)}'";
            var count = (long)cmd.ExecuteScalar()!;
            return count > 0;
        }
    }

    public IReadOnlyList<(string Seed, string Note, DateTime AddedUtc)> GetFavorites()
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
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

    public void Dispose()
    {
        _conn.Dispose();
    }

    private static string EscapeSqlPath(string path) => path.Replace("\\", "/").Replace("'", "''");
    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");
}
