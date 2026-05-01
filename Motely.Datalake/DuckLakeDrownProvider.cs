#if !BROWSER

using DuckDB.NET.Data;

namespace Motely.Datalake;

public sealed class DuckLakeDrownProvider : IMotelySeedProvider, IDisposable
{
    private readonly string[] _seeds;
    private int _index;
    private readonly System.Threading.Lock _lock = new();

    public long SeedCount { get; }

    public DuckLakeDrownProvider(string lakeDir)
    {
        var seeds = new List<string>();

        foreach (var parquetFile in Directory.EnumerateFiles(lakeDir, "*.parquet"))
        {
            using var conn = new DuckDBConnection("Data Source=:memory:");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT DISTINCT seed FROM read_parquet('{parquetFile.Replace("\\", "/")}')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var seed = reader.GetString(0);
                if (!string.IsNullOrEmpty(seed))
                    seeds.Add(seed);
            }
        }

        _seeds = seeds.Distinct().ToArray();
        SeedCount = _seeds.Length;
    }

    public ReadOnlySpan<char> NextSeed()
    {
        lock (_lock)
        {
            if (_index >= _seeds.Length) return ReadOnlySpan<char>.Empty;
            return _seeds[_index++];
        }
    }

    public int NextSeeds(string[] buffer)
    {
        lock (_lock)
        {
            int count = 0;
            while (count < buffer.Length && _index < _seeds.Length)
            {
                buffer[count++] = _seeds[_index++];
            }
            return count;
        }
    }

    public void Dispose() { }
}

#endif
