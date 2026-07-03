using DuckDB.NET.Data;

namespace Motely.Data;

/// <summary>
/// Persists found seeds into a DuckLake lakehouse so a search never loses what it found.
/// Catalog metadata lives at the repo root (<c>motely.ducklake</c>); the Parquet data files
/// land under <c>Seeds/</c>. DuckLake (DuckDB 1.5+) gives snapshots, time-travel, and
/// multi-process read/write over the same seed set for free.
/// </summary>
public sealed class DuckLakeSeedSink : IDisposable
{
    /// <summary>Catalog file at the repo root.</summary>
    private const string Catalog = "motely.ducklake";

    /// <summary>Where the Parquet data files live, relative to the repo root.</summary>
    private const string DataPath = "Seeds/";

    private readonly DuckDBConnection _conn;
    private readonly string _filterId;

    public DuckLakeSeedSink(string filterId)
    {
        _filterId = filterId;
        _conn = new DuckDBConnection("Data Source=:memory:");
        _conn.Open();

        Execute("INSTALL ducklake; LOAD ducklake;");
        Execute($"ATTACH 'ducklake:{Catalog}' AS lake (DATA_PATH '{DataPath}');");
        Execute("USE lake;");
        Execute(
            """
            CREATE TABLE IF NOT EXISTS seeds (
                filter   VARCHAR,
                seed     VARCHAR,
                score    DOUBLE,
                found_at TIMESTAMP DEFAULT now()
            );
            """);
    }

    /// <summary>Persist one found seed and its score as the search finds it.</summary>
    public void Add(string seed, double score)
    {
        using DuckDBCommand cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO seeds (filter, seed, score) VALUES (?, ?, ?);";
        cmd.Parameters.Add(new DuckDBParameter(_filterId));
        cmd.Parameters.Add(new DuckDBParameter(seed));
        cmd.Parameters.Add(new DuckDBParameter(score));
        cmd.ExecuteNonQuery();
    }

    private void Execute(string sql)
    {
        using DuckDBCommand cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();
}
