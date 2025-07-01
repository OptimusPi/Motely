using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using TheFool.Models;

namespace TheFool.Services;

public class DatabaseService : IDisposable
{
    private readonly DuckDBConnection _connection;

    public DatabaseService(ConfigService config)
    {
        var dbPath = Path.Combine(config.DataDirectory, "results.duckdb");
        _connection = new DuckDBConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE SEQUENCE IF NOT EXISTS seed_results_seq START 1;
            
            CREATE TABLE IF NOT EXISTS seed_results (
                id BIGINT PRIMARY KEY DEFAULT nextval('seed_results_seq'),
                seed VARCHAR NOT NULL,
                score DOUBLE NOT NULL,
                search_id VARCHAR,
                metadata JSON,
                natural_negative_jokers VARCHAR,
                desired_negative_jokers VARCHAR,
                found_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
            
            CREATE INDEX IF NOT EXISTS idx_score ON seed_results (score DESC);
            CREATE INDEX IF NOT EXISTS idx_seed ON seed_results (seed);
            CREATE INDEX IF NOT EXISTS idx_found_at ON seed_results (found_at DESC);
        ";
        cmd.ExecuteNonQuery();
        Console.WriteLine("Database initialized successfully");
    }

    public async Task BulkInsertResultsAsync(IEnumerable<SeedResult> results)
    {
        if (!results.Any()) return;

        using var transaction = _connection.BeginTransaction();
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO seed_results 
                (seed, score, search_id, metadata, natural_negative_jokers, desired_negative_jokers, found_at)
                VALUES (?, ?, ?, ?, ?, ?, ?)";

            foreach (var result in results)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.Add(new DuckDBParameter(result.Seed));
                cmd.Parameters.Add(new DuckDBParameter(result.Score));
                cmd.Parameters.Add(new DuckDBParameter((object?)result.SearchId ?? DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter(
                    result.Metadata != null ? JsonSerializer.Serialize(result.Metadata) : (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter((object?)result.NaturalNegativeJokers ?? DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter((object?)result.DesiredNegativeJokers ?? DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter(result.FoundAt));
                
                await Task.Run(() => cmd.ExecuteNonQuery());
            }
            
            transaction.Commit();
            Console.WriteLine($"Inserted {results.Count()} results");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"Failed to insert results: {ex.Message}");
            throw;
        }
    }

    public async Task<IList<SeedResult>> GetTopResultsAsync(int limit = 100)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT seed, score, search_id, metadata, natural_negative_jokers, desired_negative_jokers, found_at 
            FROM seed_results 
            ORDER BY score DESC 
            LIMIT ?";
        cmd.Parameters.Add(new DuckDBParameter(limit));

        var results = new List<SeedResult>();
        using var reader = await Task.Run(() => cmd.ExecuteReader());
        
        while (reader.Read())
        {
            results.Add(new SeedResult
            {
                Seed = reader.GetString(0),
                Score = reader.GetDouble(1),
                SearchId = reader.IsDBNull(2) ? null : reader.GetString(2),
                Metadata = reader.IsDBNull(3) ? null : 
                    JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(3)),
                NaturalNegativeJokers = reader.IsDBNull(4) ? null : reader.GetString(4),
                DesiredNegativeJokers = reader.IsDBNull(5) ? null : reader.GetString(5),
                FoundAt = reader.GetDateTime(6)
            });
        }
        
        return results;
    }

    public async Task<IList<SeedResult>> SearchResultsAsync(string searchTerm, int limit = 100)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT seed, score, search_id, metadata, natural_negative_jokers, desired_negative_jokers, found_at 
            FROM seed_results 
            WHERE seed LIKE ? OR natural_negative_jokers LIKE ? OR desired_negative_jokers LIKE ?
            ORDER BY score DESC 
            LIMIT ?";
        
        var term = $"%{searchTerm}%";
        cmd.Parameters.Add(new DuckDBParameter(term));
        cmd.Parameters.Add(new DuckDBParameter(term));
        cmd.Parameters.Add(new DuckDBParameter(term));
        cmd.Parameters.Add(new DuckDBParameter(limit));

        var results = new List<SeedResult>();
        using var reader = await Task.Run(() => cmd.ExecuteReader());
        
        while (reader.Read())
        {
            results.Add(new SeedResult
            {
                Seed = reader.GetString(0),
                Score = reader.GetDouble(1),
                SearchId = reader.IsDBNull(2) ? null : reader.GetString(2),
                Metadata = reader.IsDBNull(3) ? null : 
                    JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(3)),
                NaturalNegativeJokers = reader.IsDBNull(4) ? null : reader.GetString(4),
                DesiredNegativeJokers = reader.IsDBNull(5) ? null : reader.GetString(5),
                FoundAt = reader.GetDateTime(6)
            });
        }
        
        return results;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
