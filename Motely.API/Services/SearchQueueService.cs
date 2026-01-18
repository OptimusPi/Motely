using System.Data;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using Motely.API.Models;
using Motely.DuckDB;

namespace Motely.API.Services;

public class SearchQueueService
{
    private readonly string _dbPath;
    private readonly ILogger<SearchQueueService> _logger;

    public SearchQueueService(ILogger<SearchQueueService> logger, string dbPath = "searchqueue.db")
    {
        _logger = logger;
        _dbPath = dbPath;
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = DuckDBConnectionFactory.CreateConnection(_dbPath);
        var searchQueueSchema = DuckDBSchema.SearchQueueTableSchema();
        DuckDBTableManager.EnsureTableExists(conn, searchQueueSchema);
    }

    public void Enqueue(
        string searchId,
        string jamlFilter,
        int threadCount = 1,
        bool isBurst = false
    )
    {
        using var conn = DuckDBConnectionFactory.CreateConnection(_dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"
            INSERT OR REPLACE INTO SearchQueue 
            (searchId, jamlFilter, threadCount, isBurst, status, dateCreated, lastAccessed)
            VALUES (?, ?, ?, ?, 'queued', current_timestamp, current_timestamp);
        ";
        cmd.Parameters.Add(new DuckDBParameter(searchId));
        cmd.Parameters.Add(new DuckDBParameter(jamlFilter));
        cmd.Parameters.Add(new DuckDBParameter(threadCount));
        cmd.Parameters.Add(new DuckDBParameter(isBurst));
        cmd.ExecuteNonQuery();
        _logger.LogInformation("Enqueued search {SearchId}", searchId);
    }

    public SearchQueueEntry? DequeueNext()
    {
        using var conn = DuckDBConnectionFactory.CreateConnection(_dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"
            SELECT searchId, jamlFilter, dateCreated, lastAccessed, status,
                   batchMarker, seedsSearched, resultsFound, threadCount, isBurst
            FROM SearchQueue
            WHERE status = 'queued'
            ORDER BY dateCreated ASC
            LIMIT 1;
        ";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        var entry = new SearchQueueEntry
        {
            SearchId = reader.GetString("searchId"),
            JamlFilter = reader.GetString("jamlFilter"),
            DateCreated = reader.GetDateTime("dateCreated"),
            LastAccessed = reader.GetDateTime("lastAccessed"),
            Status = reader.GetString("status"),
            BatchMarker = reader.IsDBNull("batchMarker") ? 0 : reader.GetInt64("batchMarker"),
            SeedsSearched = reader.IsDBNull("seedsSearched") ? 0 : reader.GetInt64("seedsSearched"),
            ResultsFound = reader.IsDBNull("resultsFound") ? 0 : reader.GetInt32("resultsFound"),
            ThreadCount = reader.IsDBNull("threadCount") ? 1 : reader.GetInt32("threadCount"),
            IsBurst = reader.GetBoolean("isBurst"),
        };

        // Mark as running
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
        using var conn = DuckDBConnectionFactory.CreateConnection(_dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"
            UPDATE SearchQueue
            SET status = 'completed',
                seedsSearched = ?,
                resultsFound = ?,
                lastAccessed = current_timestamp
            WHERE searchId = ?;
        ";
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
        using var conn = DuckDBConnectionFactory.CreateConnection(_dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"
            UPDATE SearchQueue
            SET seedsSearched = ?,
                resultsFound = ?,
                batchMarker = ?,
                lastAccessed = current_timestamp
            WHERE searchId = ?;
        ";
        cmd.Parameters.Add(new DuckDBParameter(seedsSearched));
        cmd.Parameters.Add(new DuckDBParameter(resultsFound));
        cmd.Parameters.Add(new DuckDBParameter(batchMarker));
        cmd.Parameters.Add(new DuckDBParameter(searchId));
        cmd.ExecuteNonQuery();
    }

    public void CleanupStale(TimeSpan olderThan)
    {
        using var conn = DuckDBConnectionFactory.CreateConnection(_dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $@"
            DELETE FROM SearchQueue
            WHERE lastAccessed < (current_timestamp - INTERVAL {olderThan.TotalSeconds} SECOND)
              AND status IN ('completed', 'error', 'cancelled');
        ";
        var deleted = cmd.ExecuteNonQuery();
        if (deleted > 0)
            _logger.LogInformation("Cleaned up {Count} stale search queue entries", deleted);
    }

    public List<SearchQueueEntry> GetAll()
    {
        var list = new List<SearchQueueEntry>();
        using var conn = DuckDBConnectionFactory.CreateConnection(_dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"
            SELECT searchId, jamlFilter, dateCreated, lastAccessed, status,
                   batchMarker, seedsSearched, resultsFound, threadCount, isBurst
            FROM SearchQueue
            ORDER BY dateCreated DESC;
        ";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(
                new SearchQueueEntry
                {
                    SearchId = reader.GetString("searchId"),
                    JamlFilter = reader.GetString("jamlFilter"),
                    DateCreated = reader.GetDateTime("dateCreated"),
                    LastAccessed = reader.GetDateTime("lastAccessed"),
                    Status = reader.GetString("status"),
                    BatchMarker = reader.IsDBNull("batchMarker")
                        ? 0
                        : reader.GetInt64("batchMarker"),
                    SeedsSearched = reader.IsDBNull("seedsSearched")
                        ? 0
                        : reader.GetInt64("seedsSearched"),
                    ResultsFound = reader.IsDBNull("resultsFound")
                        ? 0
                        : reader.GetInt32("resultsFound"),
                    ThreadCount = reader.IsDBNull("threadCount")
                        ? 1
                        : reader.GetInt32("threadCount"),
                    IsBurst = reader.GetBoolean("isBurst"),
                }
            );
        }
        return list;
    }

    private void UpdateStatus(string searchId, string status)
    {
        using var conn = DuckDBConnectionFactory.CreateConnection(_dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE SearchQueue SET status = ? WHERE searchId = ?;";
        cmd.Parameters.Add(new DuckDBParameter(status));
        cmd.Parameters.Add(new DuckDBParameter(searchId));
        cmd.ExecuteNonQuery();
    }

    private void Touch(string searchId)
    {
        using var conn = DuckDBConnectionFactory.CreateConnection(_dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE SearchQueue SET lastAccessed = current_timestamp WHERE searchId = ?;";
        cmd.Parameters.Add(new DuckDBParameter(searchId));
        cmd.ExecuteNonQuery();
    }

    public void Update(SearchQueueEntry entry)
    {
        using var conn = DuckDBConnectionFactory.CreateConnection(_dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"
            UPDATE SearchQueue
            SET batchMarker = ?,
                seedsSearched = ?,
                resultsFound = ?,
                lastAccessed = current_timestamp
            WHERE searchId = ?;
        ";
        cmd.Parameters.Add(new DuckDBParameter(entry.BatchMarker));
        cmd.Parameters.Add(new DuckDBParameter(entry.SeedsSearched));
        cmd.Parameters.Add(new DuckDBParameter(entry.ResultsFound));
        cmd.Parameters.Add(new DuckDBParameter(entry.SearchId));
        cmd.ExecuteNonQuery();
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
