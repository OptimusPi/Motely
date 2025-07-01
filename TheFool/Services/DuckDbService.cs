using DuckDB.NET.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheFool.Models;

namespace TheFool.Services;

public interface IDuckDbService
{
    Task<List<SearchResult>> GetResultsAsync(string configName, int limit = 1000);
    Task<bool> DatabaseExistsAsync(string configName);
    Task<DatabaseStats> GetDatabaseStatsAsync(string configName);
    Task<bool> CreateOrUpdateDatabaseAsync(string configName, string[] headers);
    Task InsertResultAsync(string configName, string[] values);
    Task<string[]> GetDatabaseHeadersAsync(string configName);
    Task BackupDatabaseAsync(string configName);
}

public class DuckDbService : IDuckDbService, IDisposable
{
    private readonly string _databasePath;
    private DuckDBConnection? _currentConnection;
    private string? _currentConfigName;
    private string[]? _currentHeaders;

    public DuckDbService()
    {
        _databasePath = "ouija_databases";
        Directory.CreateDirectory(_databasePath);
    }

    public async Task<bool> CreateOrUpdateDatabaseAsync(string configName, string[] headers)
    {
        // Close any existing connection if switching configs
        if (_currentConfigName != configName && _currentConnection != null)
        {
            _currentConnection.Dispose();
            _currentConnection = null;
        }

        _currentConfigName = configName;
        var dbPath = GetDatabasePath(configName);
        
        // Check if database exists and headers match
        if (File.Exists(dbPath))
        {
            var existingHeaders = await GetDatabaseHeadersAsync(configName);
            if (!HeadersMatch(headers, existingHeaders))
            {
                // Backup existing database
                await BackupDatabaseAsync(configName);
                
                // Close connection before deleting
                if (_currentConnection != null)
                {
                    _currentConnection.Dispose();
                    _currentConnection = null;
                }
                
                // Delete old database
                File.Delete(dbPath);
            }
            else
            {
                // Headers match, we're good
                _currentHeaders = headers;
                return true;
            }
        }

        // Create new database with dynamic schema
        var connection = GetOrCreateConnection(configName);
        var createTableSql = BuildCreateTableSql(headers);
        
        using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        await command.ExecuteNonQueryAsync();
        
        _currentHeaders = headers;
        return true;
    }

    public async Task InsertResultAsync(string configName, string[] values)
    {
        if (_currentConfigName != configName || _currentConnection == null)
        {
            throw new InvalidOperationException($"Database not initialized for {configName}. Call CreateOrUpdateDatabaseAsync first.");
        }

        if (_currentHeaders == null)
        {
            throw new InvalidOperationException("Headers not set. Call CreateOrUpdateDatabaseAsync first.");
        }

        var insertSql = BuildInsertSql(_currentHeaders, values);
        
        using var command = _currentConnection.CreateCommand();
        command.CommandText = insertSql;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<string[]> GetDatabaseHeadersAsync(string configName)
    {
        var dbPath = GetDatabasePath(configName);
        if (!File.Exists(dbPath))
            return Array.Empty<string>();

        var connection = GetOrCreateConnection(configName);
        
        // Get column information from the search_results table
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(search_results)";
        
        var headers = new List<string>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString(1);
            // Skip metadata columns
            if (columnName != "inserted_at")
            {
                headers.Add(columnName);
            }
        }
        
        return headers.ToArray();
    }

    public async Task BackupDatabaseAsync(string configName)
    {
        var dbPath = GetDatabasePath(configName);
        if (!File.Exists(dbPath))
            return;

        var backupPath = $"{dbPath}.BAK";
        
        // Close connection before backup
        if (_currentConnection != null && _currentConfigName == configName)
        {
            _currentConnection.Dispose();
            _currentConnection = null;
        }
        
        File.Copy(dbPath, backupPath, true);
        await Task.CompletedTask;
    }

    public async Task<List<SearchResult>> GetResultsAsync(string configName, int limit = 1000)
    {
        var dbPath = GetDatabasePath(configName);
        if (!File.Exists(dbPath))
            return new List<SearchResult>();

        var connection = GetOrCreateConnection(configName);
        var results = new List<SearchResult>();

        // Build dynamic query based on known columns
        var query = @"
            SELECT 
                Seed,
                Score,
                COALESCE(NaturalNegatives, 0) as NaturalNegatives,
                COALESCE(DesiredNegatives, 0) as DesiredNegatives,
                inserted_at
            FROM search_results
            ORDER BY Score DESC, inserted_at DESC
            LIMIT ?";

        using var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new DuckDBParameter(limit));

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new SearchResult
            {
                Seed = reader.GetString(0),
                Score = reader.GetInt32(1),
                NaturalNegativeJokers = reader.GetInt32(2),
                DesiredNegativeJokers = reader.GetInt32(3),
                FoundAt = reader.GetDateTime(4)
            });
        }

        return results;
    }

    private string BuildCreateTableSql(string[] headers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CREATE TABLE search_results (");
        
        for (int i = 0; i < headers.Length; i++)
        {
            var header = headers[i];
            var columnDef = GetColumnDefinition(header);
            sb.Append($"    {header} {columnDef}");
            
            if (i < headers.Length - 1)
                sb.AppendLine(",");
        }
        
        // Add metadata columns
        sb.AppendLine(",");
        sb.AppendLine("    inserted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,");
        sb.AppendLine("    PRIMARY KEY (Seed)");
        sb.AppendLine(");");
        
        return sb.ToString();
    }

    private string BuildInsertSql(string[] headers, string[] values)
    {
        var columns = string.Join(", ", headers);
        var placeholders = string.Join(", ", values.Select(v => $"'{v.Replace("'", "''")}'"));
        
        return $@"
            INSERT OR REPLACE INTO search_results ({columns})
            VALUES ({placeholders})";
    }

    private string GetColumnDefinition(string header)
    {
        // Determine column type based on header name
        return header.ToLower() switch
        {
            "seed" => "VARCHAR(16) NOT NULL",
            var h when h.Contains("score") => "INTEGER DEFAULT 0",
            var h when h.Contains("negative") => "INTEGER DEFAULT 0",
            var h when h.Contains("count") => "INTEGER DEFAULT 0",
            _ => "INTEGER DEFAULT 0"  // Default to integer for unknown columns
        };
    }

    private bool HeadersMatch(string[] headers1, string[] headers2)
    {
        if (headers1.Length != headers2.Length)
            return false;
            
        return headers1.SequenceEqual(headers2, StringComparer.OrdinalIgnoreCase);
    }

    private DuckDBConnection GetOrCreateConnection(string configName)
    {
        // Only maintain ONE connection - to the current config's database
        if (_currentConnection == null || _currentConfigName != configName)
        {
            // Close existing connection if switching configs
            if (_currentConnection != null)
            {
                _currentConnection.Dispose();
            }
            
            var dbPath = GetDatabasePath(configName);
            _currentConnection = new DuckDBConnection($"DataSource={dbPath}");
            _currentConnection.Open();
            _currentConfigName = configName;
        }
        return _currentConnection;
    }

    private string GetDatabasePath(string configName)
    {
        var baseName = configName.Replace(".ouija.json", "").Replace(".json", "");
        return Path.Combine(_databasePath, $"{baseName}.ouija.duckdb");
    }

    public async Task<DatabaseStats> GetDatabaseStatsAsync(string configName)
    {
        var dbPath = GetDatabasePath(configName);
        if (!File.Exists(dbPath))
            return new DatabaseStats();

        var connection = GetOrCreateConnection(configName);
        var stats = new DatabaseStats();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COUNT(*) as total_results,
                MAX(Score) as best_score,
                AVG(Score) as avg_score,
                MIN(inserted_at) as first_result,
                MAX(inserted_at) as last_result
            FROM search_results";

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            stats.TotalResults = reader.GetInt32(0);
            stats.BestScore = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            stats.AverageScore = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
            stats.FirstResult = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
            stats.LastResult = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
        }

        return stats;
    }

    public async Task<bool> DatabaseExistsAsync(string configName)
    {
        var dbPath = GetDatabasePath(configName);
        return await Task.FromResult(File.Exists(dbPath));
    }

    public void Dispose()
    {
        if (_currentConnection != null)
        {
            _currentConnection.Dispose();
            _currentConnection = null;
        }
    }
}

public class DatabaseStats
{
    public int TotalResults { get; set; }
    public int BestScore { get; set; }
    public double AverageScore { get; set; }
    public DateTime? FirstResult { get; set; }
    public DateTime? LastResult { get; set; }
}
