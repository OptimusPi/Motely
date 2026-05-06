namespace Motely;

public static class SeedTextReader
{
    public static List<string> ReadSeeds(string path)
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
}
