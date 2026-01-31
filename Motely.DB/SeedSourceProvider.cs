using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DuckDB.NET.Data;
using Motely.DB;

namespace Motely.DB;

/// <summary>
/// The "One True Way" to get a seed database from a user-provided string.
/// Handles .txt/.csv conversion, re-importing prompts, and "results" table extraction.
/// </summary>
public static class SeedSourceProvider
{
    public static string? GetSeedDb(
        string input, 
        bool quiet = false,
        bool forceOverwrite = false,
        Action<string>? logCallback = null)
    {
        bool isAbsolute = Path.IsPathRooted(input);
        string storageDirectory = "SeedSources";
        Directory.CreateDirectory(storageDirectory);

        string originalPath = input;
        
        // If not absolute, try several locations:
        // 1. Current directory (relative to where app is running)
        // 2. SeedSources/ (the cache directory)
        if (!isAbsolute)
        {
            if (File.Exists(input))
            {
                originalPath = Path.GetFullPath(input);
            }
            else
            {
                string combinedPath = Path.Combine(storageDirectory, input);
                if (File.Exists(combinedPath))
                {
                    originalPath = combinedPath;
                }
            }
        }

        string baseName = Path.GetFileNameWithoutExtension(originalPath);
        string dbPath = Path.Combine(storageDirectory, baseName + ".db");
        string extension = Path.GetExtension(originalPath).ToLowerInvariant();

        // If user provided a .db file directly, use it as is
        if (extension == ".db")
        {
            if (!File.Exists(originalPath))
            {
               throw new FileNotFoundException($"Seed database file not found: {originalPath}");
            }
            EnsureSeedsTableExists(originalPath, quiet, logCallback);
            return originalPath;
        }

        // Check if DB cache exists
        if (File.Exists(dbPath))
        {
            // If it's a .txt/.csv request but .db exists, ASK if it's not quiet/forced
            if (!forceOverwrite && !quiet)
            {
                var dbInfo = new FileInfo(dbPath);
                var sizeMB = dbInfo.Length / (1024.0 * 1024.0);
                
                logCallback?.Invoke($"\n⚠️ Found existing database for this source: {Path.GetFileName(dbPath)} [{sizeMB:F0}MB]");
                logCallback?.Invoke($"   Use existing database or [R]e-import from source {Path.GetFileName(originalPath)}? [U/r]");
                
                var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (response == "r") 
                {
                    logCallback?.Invoke("   🔄 Re-importing from source...");
                    try { File.Delete(dbPath); } catch { /* ignore */ }
                }
                else
                {
                    logCallback?.Invoke("   ✅ Using existing database.");
                    EnsureSeedsTableExists(dbPath, quiet, logCallback);
                    return dbPath;
                }
            }
            else if (forceOverwrite)
            {
                try { File.Delete(dbPath); } catch { /* ignore */ }
            }
            else
            {
                // Quiet mode or just default behavior: use existing
                EnsureSeedsTableExists(dbPath, quiet, logCallback);
                return dbPath;
            }
        }

        // Import Phase
        if (!File.Exists(originalPath))
        {
            // Try adding extensions if user left them off
            if (string.IsNullOrEmpty(extension))
            {
                if (File.Exists(originalPath + ".txt")) extension = ".txt";
                else if (File.Exists(originalPath + ".csv")) extension = ".csv";
                
                if (!string.IsNullOrEmpty(extension))
                {
                    originalPath += extension;
                }
            }
            
            if (!File.Exists(originalPath))
            {
                throw new FileNotFoundException(
                    $"Seed source file not found: {input}. " +
                    $"Checked absolute/relative paths and Extensions: .db, .csv, .txt"
                );
            }
        }

        return extension switch
        {
            ".csv" => ConvertCsvToDuckDB(originalPath, dbPath, quiet, logCallback),
            ".txt" => ConvertTextToDuckDB(originalPath, dbPath, quiet, logCallback),
            _ => throw new NotSupportedException($"Unsupported seed source extension: {extension}")
        };
    }

    private static void EnsureSeedsTableExists(string dbPath, bool quiet, Action<string>? logCallback)
    {
        using var conn = DuckDBConnectionFactory.CreateConnection(dbPath);
        bool hasSeeds = DuckDBOperations.TableExists(conn, "seeds");
        bool hasResults = DuckDBOperations.TableExists(conn, "results");

        if (!hasSeeds && hasResults)
        {
            if (!quiet) logCallback?.Invoke("📊 Detected results database, extracting seeds to 'seeds' table...");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE seeds AS SELECT DISTINCT seed FROM results WHERE seed IS NOT NULL";
            cmd.ExecuteNonQuery();
            
            long seedCount = DuckDBOperations.GetRowCount(conn, "seeds");
            if (!quiet) logCallback?.Invoke($"✅ Extracted {seedCount:N0} seeds.");
        }
        else if (!hasSeeds)
        {
            throw new Exception($"Database {dbPath} does not have a 'seeds' table and no 'results' table found to extract from.");
        }
    }

    private static string ConvertCsvToDuckDB(string csvPath, string dbPath, bool quiet, Action<string>? logCallback)
    {
        if (!quiet) logCallback?.Invoke($"🔄 Converting CSV to DuckDB: {csvPath} -> {dbPath}");
        
        using (var conn = DuckDBConnectionFactory.CreateConnection(dbPath))
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE seeds (seed VARCHAR)";
            cmd.ExecuteNonQuery();

            using var appender = conn.CreateAppender("seeds");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int count = 0;
            
            // Streaming CSV read
            foreach (var line in File.ReadLines(csvPath).Skip(1)) // Skip header
            {
                var parts = line.Split(',');
                if (parts.Length > 0)
                {
                    var seed = parts[0].Trim('"', ' ');
                    if (!string.IsNullOrEmpty(seed) && seen.Add(seed))
                    {
                        var row = appender.CreateRow();
                        row.AppendValue(seed);
                        row.EndRow();
                        count++;
                    }
                }
            }
            appender.Close();
            if (!quiet) logCallback?.Invoke($"✅ Imported {count:N0} unique seeds.");
        }
        return dbPath;
    }

    private static string ConvertTextToDuckDB(string textPath, string dbPath, bool quiet, Action<string>? logCallback)
    {
        if (!quiet) logCallback?.Invoke($"🔄 Converting text file: {textPath} -> {dbPath}");
        
        using (var conn = DuckDBConnectionFactory.CreateConnection(dbPath))
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE seeds (seed VARCHAR)";
            cmd.ExecuteNonQuery();

            using var appender = conn.CreateAppender("seeds");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int count = 0;
            
            foreach (var line in File.ReadLines(textPath))
            {
                var seed = line.Trim();
                if (!string.IsNullOrEmpty(seed) && seen.Add(seed))
                {
                    var row = appender.CreateRow();
                    row.AppendValue(seed);
                    row.EndRow();
                    count++;
                }
            }
            appender.Close();
            
            if (!quiet) logCallback?.Invoke($"✅ Imported {count:N0} unique seeds.");
        }
        return dbPath;
    }
}
