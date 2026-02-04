using System.Collections.Generic;
using DuckDB.NET.Data;

namespace Motely.DB;

/// <summary>
/// High-level abstraction for search queue database operations
/// Handles single connection pattern and all SQL internally
/// No SQL should be visible to callers
/// </summary>
public sealed class MotelySearchQueueDatabase : IDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly string _dbPath;
    private bool _disposed = false;

    public string DatabasePath => _dbPath;

    public MotelySearchQueueDatabase(string dbPath)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));

        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            Directory.CreateDirectory(dbDir);

        _connection = DuckDBConnectionFactory.CreateConnection(dbPath);
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var searchQueueSchema = DuckDBSchema.SearchQueueTableSchema();
        ExecuteNonQuery(searchQueueSchema);
    }

    public void Enqueue(
        string searchId,
        string jamlFilter,
        int threadCount = 1,
        bool isBurst = false
    )
    {
        if (_disposed)
            return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "INSERT OR REPLACE INTO SearchQueue (searchId, jamlFilter, threadCount, isBurst, status, dateCreated, lastAccessed) VALUES (?, ?, ?, ?, 'queued', current_timestamp, current_timestamp);";
        cmd.Parameters.Add(new DuckDBParameter(searchId));
        cmd.Parameters.Add(new DuckDBParameter(jamlFilter));
        cmd.Parameters.Add(new DuckDBParameter(threadCount));
        cmd.Parameters.Add(new DuckDBParameter(isBurst));
        cmd.ExecuteNonQuery();
    }

    public SearchQueueEntry? DequeueNext()
    {
        if (_disposed)
            return null;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "SELECT searchId, jamlFilter, dateCreated, lastAccessed, status, batchMarker, seedsSearched, resultsFound, threadCount, isBurst FROM SearchQueue WHERE status = 'queued' ORDER BY dateCreated ASC LIMIT 1;";
        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return null;

        var entry = new SearchQueueEntry
        {
            SearchId = reader.GetString(0),
            JamlFilter = reader.GetString(1),
            DateCreated = reader.GetDateTime(2),
            LastAccessed = reader.GetDateTime(3),
            Status = reader.GetString(4),
            BatchMarker = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
            SeedsSearched = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
            ResultsFound = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
            ThreadCount = reader.IsDBNull(8) ? 1 : reader.GetInt32(8),
            IsBurst = reader.GetBoolean(9),
        };

        MarkRunning(entry.SearchId);
        return entry;
    }

    public void MarkRunning(string searchId)
    {
        UpdateStatus(searchId, "running");
        Touch(searchId);
    }

    public void MarkCompleted(string searchId, long seedsSearched, int resultsFound)
    {
        if (_disposed)
            return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "UPDATE SearchQueue SET status = 'completed', seedsSearched = ?, resultsFound = ?, lastAccessed = current_timestamp WHERE searchId = ?;";
        cmd.Parameters.Add(new DuckDBParameter(seedsSearched));
        cmd.Parameters.Add(new DuckDBParameter(resultsFound));
        cmd.Parameters.Add(new DuckDBParameter(searchId));
        cmd.ExecuteNonQuery();
    }

    public void MarkError(string searchId, string error)
    {
        UpdateStatus(searchId, "error");
        Touch(searchId);
    }

    public void UpdateProgress(
        string searchId,
        long seedsSearched,
        int resultsFound,
        long batchMarker = 0
    )
    {
        if (_disposed)
            return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "UPDATE SearchQueue SET seedsSearched = ?, resultsFound = ?, batchMarker = ?, lastAccessed = current_timestamp WHERE searchId = ?;";
        cmd.Parameters.Add(new DuckDBParameter(seedsSearched));
        cmd.Parameters.Add(new DuckDBParameter(resultsFound));
        cmd.Parameters.Add(new DuckDBParameter(batchMarker));
        cmd.Parameters.Add(new DuckDBParameter(searchId));
        cmd.ExecuteNonQuery();
    }

    public List<SearchQueueEntry> GetAll()
    {
        if (_disposed)
            return new List<SearchQueueEntry>();

        var list = new List<SearchQueueEntry>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "SELECT searchId, jamlFilter, dateCreated, lastAccessed, status, batchMarker, seedsSearched, resultsFound, threadCount, isBurst FROM SearchQueue ORDER BY dateCreated DESC;";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            list.Add(
                new SearchQueueEntry
                {
                    SearchId = reader.GetString(0),
                    JamlFilter = reader.GetString(1),
                    DateCreated = reader.GetDateTime(2),
                    LastAccessed = reader.GetDateTime(3),
                    Status = reader.GetString(4),
                    BatchMarker = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                    SeedsSearched = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                    ResultsFound = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    ThreadCount = reader.IsDBNull(8) ? 1 : reader.GetInt32(8),
                    IsBurst = reader.GetBoolean(9),
                }
            );
        }
        return list;
    }

    public void CleanupStale(TimeSpan olderThan)
    {
        if (_disposed)
            return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            $"DELETE FROM SearchQueue WHERE lastAccessed < (current_timestamp - INTERVAL {olderThan.TotalSeconds} SECOND) AND status IN ('completed', 'error', 'cancelled');";
        cmd.ExecuteNonQuery();
    }

    public void Update(SearchQueueEntry entry)
    {
        if (_disposed)
            return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "UPDATE SearchQueue SET batchMarker = ?, seedsSearched = ?, resultsFound = ?, lastAccessed = current_timestamp WHERE searchId = ?;";
        cmd.Parameters.Add(new DuckDBParameter(entry.BatchMarker));
        cmd.Parameters.Add(new DuckDBParameter(entry.SeedsSearched));
        cmd.Parameters.Add(new DuckDBParameter(entry.ResultsFound));
        cmd.Parameters.Add(new DuckDBParameter(entry.SearchId));
        cmd.ExecuteNonQuery();
    }

    private void UpdateStatus(string searchId, string status)
    {
        if (_disposed)
            return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE SearchQueue SET status = ? WHERE searchId = ?;";
        cmd.Parameters.Add(new DuckDBParameter(status));
        cmd.Parameters.Add(new DuckDBParameter(searchId));
        cmd.ExecuteNonQuery();
    }

    private void Touch(string searchId)
    {
        if (_disposed)
            return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "UPDATE SearchQueue SET lastAccessed = current_timestamp WHERE searchId = ?;";
        cmd.Parameters.Add(new DuckDBParameter(searchId));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// INTERNAL ONLY - Schema initialization queries only
    /// Do NOT expose this method - all SQL must be encapsulated in public methods
    /// </summary>
    private void ExecuteNonQuery(string sql)
    {
        if (_disposed)
            return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _connection?.Dispose();
    }
}

public class SearchQueueEntry
{
    public string SearchId { get; set; } = string.Empty;
    public string JamlFilter { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public DateTime LastAccessed { get; set; }
    public string Status { get; set; } = "queued";
    public long BatchMarker { get; set; }
    public long SeedsSearched { get; set; }
    public int ResultsFound { get; set; }
    public int ThreadCount { get; set; } = 1;
    public bool IsBurst { get; set; }
}
