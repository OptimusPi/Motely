using Motely.DB.SeedSource;

namespace Motely.DB;

public static class MotelyLake
{
    public static ISeedResultSink GetSink(string filterId, int tallyCount = 0)
    {
        var db = new MotelyResultsDb(SeedStoragePaths.StandardLakeDirectory, tallyCount);
        return new LakeSink(db, filterId,
            Path.Combine(SeedStoragePaths.StandardLakeDirectory, $"{filterId}.parquet"));
    }

    private sealed class LakeSink(MotelyResultsDb db, string filterId, string outputPath) : ISeedResultSink
    {
        public string OutputPath => outputPath;

        public void AppendSeed(string seed)
        {
            var n = SeedReader.NormalizeSeedToken(seed);
            if (!string.IsNullOrWhiteSpace(n))
                db.AppendResult(filterId, n, 0, ReadOnlySpan<int>.Empty);
        }

        public void AppendScoredResult(string seed, int score, ReadOnlySpan<int> tallies)
        {
            var n = SeedReader.NormalizeSeedToken(seed);
            if (!string.IsNullOrWhiteSpace(n))
                db.AppendResult(filterId, n, score, tallies);
        }

        public void Dispose() => db.Dispose();
    }
}
