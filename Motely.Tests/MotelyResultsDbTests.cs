using Motely.Datalake;
using Xunit;

namespace Motely.Tests;

public sealed class MotelyResultsDbTests
{
    [Fact]
    public void DuckLake_CanAppendAndQueryResults()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "motely-datalake-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(tempRoot);

        try
        {
            using (var sink = MotelyLake.GetSink("test_filter", 2, tempRoot))
            {
                sink.AppendScoredResult("AAAAA11111", 10, [1, 0]);
                sink.AppendScoredResult("BBBBB22222", 35, [5, 1]);
                sink.AppendScoredResult("CCCCC33333", 20, [2, 3]);
            }

            var results = MotelyLake.QueryResults("test_filter", limit: 3, lakeRoot: tempRoot);
            Assert.Equal(3, results.Count);
            Assert.Equal("BBBBB22222", results[0].Seed);
            Assert.Equal(35, results[0].Score);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); }
            catch { }
        }
    }
}
