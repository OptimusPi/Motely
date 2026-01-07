#if !BROWSER
using DuckDB.NET.Data;
using System;

namespace Motely.DuckDB;

/// <summary>
/// Helper methods for working with DuckDB appenders.
/// 
/// NOTE: DuckDB appenders buffer data for performance. Buffered rows become visible
/// to queries after the appender is flushed or closed. For seed searching, this is
/// perfectly fine - we don't need real-time querying during the search. The buffering
/// significantly improves insert performance.
/// 
/// If you DO need real-time querying of appended data, use a separate read connection.
/// </summary>
public static class DuckDBAppenderHelpers
{
    /// <summary>
    /// Flush the appender to make buffered data visible to queries.
    /// After flushing, all buffered rows become visible to SELECT queries
    /// on the same connection.
    /// 
    /// Note: DuckDB.NET.Data doesn't expose Flush() directly, so we close
    /// and recreate the appender. This is less efficient but ensures data visibility.
    /// </summary>
    /// <param name="appender">The appender to flush</param>
    /// <param name="connection">The connection to recreate the appender on</param>
    /// <param name="tableName">The table name for the appender</param>
    /// <returns>A new appender ready for more inserts</returns>
    public static DuckDBAppender FlushAndRecreate(DuckDBAppender appender, DuckDBConnection connection, string tableName)
    {
        if (appender == null)
            throw new ArgumentNullException(nameof(appender));
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        // Close the appender - this flushes all buffered data
        appender.Close();
        
        // Create a new appender for continued inserts
        return connection.CreateAppender(tableName);
    }

    /// <summary>
    /// Check if an appender has buffered data that hasn't been flushed.
    /// 
    /// Note: DuckDB.NET.Data doesn't expose a way to check buffer state,
    /// so this always returns true if appender is not null (assumes data may be buffered).
    /// </summary>
    /// <param name="appender">The appender to check</param>
    /// <returns>True if appender exists (may have buffered data)</returns>
    public static bool HasBufferedData(DuckDBAppender? appender)
    {
        // DuckDB.NET.Data doesn't expose buffer state, so we assume
        // any open appender may have buffered data
        return appender != null;
    }

    /// <summary>
    /// Create a separate read connection for querying data while appender is open.
    /// This allows you to query data that's already been flushed, even while
    /// the appender continues to buffer new inserts.
    /// 
    /// IMPORTANT: The read connection will NOT see buffered data from the appender
    /// until the appender is flushed/closed. It will only see data that's already
    /// been committed to the database.
    /// </summary>
    /// <param name="dbPath">Path to the DuckDB database file</param>
    /// <returns>A new read-only connection for querying</returns>
    public static DuckDBConnection CreateReadConnection(string dbPath)
    {
        return DuckDBConnectionFactory.CreateConnection(dbPath);
    }
}

#endif
