#if !BROWSER

using DuckDB.NET.Data;

namespace Motely.Datalake;

public static class SeedReader
{
    public static List<string> ReadSeeds(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".parquet" => ReadParquetSeeds(path),
            _ => ReadTextSeeds(path),
        };
    }

    public static bool TryCreateProvider(string path, out IMotelySeedProvider? provider)
    {
        provider = null;
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".parquet" && File.Exists(path))
        {
            var seeds = ReadParquetSeeds(path);
            if (seeds.Count > 0)
            {
                provider = new MotelySeedListProvider(seeds, seeds.Count);
                return true;
            }
        }

        if (Directory.Exists(path))
        {
            var parquets = Directory.GetFiles(path, "*.parquet");
            if (parquets.Length > 0)
            {
                var allSeeds = new List<string>();
                foreach (var pq in parquets)
                    allSeeds.AddRange(ReadParquetSeeds(pq));
                if (allSeeds.Count > 0)
                {
                    provider = new MotelySeedListProvider(allSeeds.Distinct().ToList(), allSeeds.Count);
                    return true;
                }
            }
        }

        return false;
    }

    public static List<string> ParseInlineSeeds(string value)
    {
        var seeds = new List<string>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(part))
                seeds.Add(part);
        }
        return seeds;
    }

    private static List<string> ReadTextSeeds(string path)
    {
        var seeds = new List<string>();
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var commaIdx = trimmed.IndexOf(',');
            var seed = commaIdx >= 0 ? trimmed[..commaIdx].Trim() : trimmed;
            if (!string.IsNullOrEmpty(seed))
                seeds.Add(seed);
        }
        return seeds;
    }

    private static List<string> ReadParquetSeeds(string path)
    {
        using var conn = new DuckDBConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT DISTINCT seed FROM read_parquet('{path.Replace("\\", "/")}')";
        using var reader = cmd.ExecuteReader();

        var seeds = new List<string>();
        while (reader.Read())
        {
            var seed = reader.GetString(0);
            if (!string.IsNullOrEmpty(seed))
                seeds.Add(seed);
        }
        return seeds;
    }
}

#endif
