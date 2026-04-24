using Motely.DB;
using Xunit;

namespace Motely.Tests;

public sealed class MotelyResultsDbTests
{
    [Fact]
    public void DuckLake_CanAppendQueryAndExportParquet()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "motely-ducklake-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(tempRoot);

        try
        {
            var dbPath = Path.Combine(tempRoot, "results.db");
            var exportPath = Path.Combine(tempRoot, "top-results.parquet");

            using (var db = new MotelyResultsDb(dbPath, tallyCount: 2))
            {
                db.AppendResult("AAAAA11111", 10, [1, 0]);
                db.AppendResult("BBBBB22222", 35, [5, 1]);
                db.AppendResult("CCCCC33333", 20, [2, 3]);

                Assert.Equal(3, db.Count);

                var top = db.GetTopResults(3);
                Assert.Equal(3, top.Count);
                Assert.Equal("BBBBB22222", top[0].Seed);
                Assert.Equal(35, top[0].Score);
                Assert.Equal("CCCCC33333", top[1].Seed);
                Assert.Equal("AAAAA11111", top[2].Seed);

                db.ExportParquet(exportPath);
            }

            var lakeDir = Path.Combine(tempRoot, "results_lake");
            var metadataFile = Path.Combine(lakeDir, "metadata.ducklake");
            var dataDir = Path.Combine(lakeDir, "data");

            Assert.True(Directory.Exists(lakeDir), "Expected DuckLake catalog directory to exist.");
            Assert.True(File.Exists(metadataFile), "Expected DuckLake metadata file to exist.");
            Assert.True(Directory.Exists(dataDir), "Expected DuckLake data directory to exist.");
            Assert.True(File.Exists(exportPath), "Expected exported Parquet file to exist.");
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures in temp test output.
            }
        }
    }
}