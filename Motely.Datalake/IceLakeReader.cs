namespace Motely.Datalake;

public static class IceLakeReader
{
#if !BROWSER
    public static List<string> ReadSeeds(string parquetUrl, int limit = 0)
    {
        using var conn = new DuckDB.NET.Data.DuckDBConnection("Data Source=:memory:");
        conn.Open();

        using var cmd = conn.CreateCommand();
        var limitClause = limit > 0 ? $" LIMIT {limit}" : "";
        cmd.CommandText = $"SELECT seed FROM read_parquet('{parquetUrl}'){limitClause}";
        using var reader = cmd.ExecuteReader();

        var seeds = new List<string>();
        while (reader.Read())
            seeds.Add(reader.GetString(0));
        return seeds;
    }

    public static int CountSeeds(string parquetUrl)
    {
        using var conn = new DuckDB.NET.Data.DuckDBConnection("Data Source=:memory:");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM read_parquet('{parquetUrl}')";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
#else
    public static Task<List<string>> ReadSeedsAsync(string parquetUrl, int limit = 0)
    {
        return Task.FromResult(new List<string>());
    }

    public static Task<int> CountSeedsAsync(string parquetUrl)
    {
        return Task.FromResult(0);
    }
#endif
}
