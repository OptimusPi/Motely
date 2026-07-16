using Motely.Filters.Jaml;

namespace Motely.Tests;

// Pure, browser-free coverage of the shared top-seed sink: the bounded top-N collector and the
// JAML seeds: rewrite. This is the prod safety net — every branch the CLI and Motely.Wasm both
// depend on is proven here, with no File System API and no WASM in the loop.
public sealed class MotelyTopSeedSinkTests
{
    // ── Collector: bounded top-N by score ──

    [Fact]
    public void Collector_KeepsTopNByScoreDescending()
    {
        var c = new MotelyTopSeedSink.Collector(3);
        c.Consider("A", 10);
        c.Consider("B", 50);
        c.Consider("C", 20);
        c.Consider("D", 40);
        c.Consider("E", 30);

        Assert.Equal(["B", "D", "E"], c.GetSeeds());
    }

    [Fact]
    public void Collector_TieBreaksByInsertionOrder()
    {
        var c = new MotelyTopSeedSink.Collector(10);
        c.Consider("first", 5);
        c.Consider("second", 5);
        c.Consider("third", 5);

        Assert.Equal(["first", "second", "third"], c.GetSeeds());
    }

    [Fact]
    public void Collector_DedupesSeeds()
    {
        var c = new MotelyTopSeedSink.Collector(10);
        c.Consider("DUPE", 10);
        c.Consider("DUPE", 30);
        c.Consider("OTHER", 20);

        var seeds = c.GetSeeds();
        Assert.Equal(2, seeds.Count);
        Assert.Single(seeds, s => s == "DUPE");
    }

    [Fact]
    public void Collector_LimitAtLeastCount_ReturnsAll()
    {
        var c = new MotelyTopSeedSink.Collector(100);
        c.Consider("A", 1);
        c.Consider("B", 2);

        Assert.Equal(["B", "A"], c.GetSeeds());
    }

    [Fact]
    public void Collector_Empty_ReturnsEmpty()
    {
        var c = new MotelyTopSeedSink.Collector(5);
        Assert.Empty(c.GetSeeds());
    }

    [Fact]
    public void Collector_HeapEvictionIsOrderIndependent()
    {
        var asc = new MotelyTopSeedSink.Collector(3);
        foreach (var (s, v) in new[] { ("A", 1), ("B", 2), ("C", 3), ("D", 4), ("E", 5) })
            asc.Consider(s, v);

        var desc = new MotelyTopSeedSink.Collector(3);
        foreach (var (s, v) in new[] { ("E", 5), ("D", 4), ("C", 3), ("B", 2), ("A", 1) })
            desc.Consider(s, v);

        Assert.Equal(["E", "D", "C"], asc.GetSeeds());
        Assert.Equal(["E", "D", "C"], desc.GetSeeds());
    }

    // ── seeds: rewrite round-trip (existing seeds are a curated provider: they stay, in front,
    //    in order; new finds merge in after them — a save NEVER deletes a seed) ──

    private static readonly string[] NewSeeds = ["XXXXXXXX", "YYYYYYYY", "ZZZZZZZZ"];

    [Fact]
    public void Rewrite_BlockForm_MergesExistingInFront()
    {
        var doc = "name: t\nseeds:\n  - OLDAAAAA\n  - OLDBBBBB\n";

        Assert.True(
            MotelyTopSeedSink.TryRewriteAndValidate(doc, NewSeeds, out var updated, out var err),
            err
        );
        Assert.True(JamlConfigLoader.TryLoad(updated, out var cfg, out _));
        Assert.Equal(["OLDAAAAA", "OLDBBBBB", .. NewSeeds], cfg!.Seeds);
    }

    [Fact]
    public void Rewrite_InlineForm_MergesExistingInFront()
    {
        var doc = "name: t\nseeds: [OLDAAAAA, OLDBBBBB]\n";

        Assert.True(
            MotelyTopSeedSink.TryRewriteAndValidate(doc, NewSeeds, out var updated, out var err),
            err
        );
        Assert.True(JamlConfigLoader.TryLoad(updated, out var cfg, out _));
        Assert.Equal(["OLDAAAAA", "OLDBBBBB", .. NewSeeds], cfg!.Seeds);
    }

    [Fact]
    public void Rewrite_DedupesNewAgainstExisting()
    {
        var doc = "name: t\nseeds:\n  - XXXXXXXX\n  - OLDAAAAA\n";

        Assert.True(
            MotelyTopSeedSink.TryRewriteAndValidate(doc, NewSeeds, out var updated, out var err),
            err
        );
        Assert.True(JamlConfigLoader.TryLoad(updated, out var cfg, out _));
        // XXXXXXXX keeps its curated front spot; only the genuinely new seeds append.
        Assert.Equal(["XXXXXXXX", "OLDAAAAA", "YYYYYYYY", "ZZZZZZZZ"], cfg!.Seeds);
    }

    [Fact]
    public void Rewrite_NoSeedsKey_Appended()
    {
        var doc = "name: t\ndeck: Red\nstake: White\n";

        Assert.True(
            MotelyTopSeedSink.TryRewriteAndValidate(doc, NewSeeds, out var updated, out var err),
            err
        );
        Assert.True(JamlConfigLoader.TryLoad(updated, out var cfg, out _));
        Assert.Equal(NewSeeds, cfg!.Seeds);
    }

    [Fact]
    public void Rewrite_SeedsInMiddle_PreservesTrailingClauses()
    {
        var doc =
            "name: t\n"
            + "seeds:\n  - OLDAAAAA\n"
            + "should:\n  - voucher: Overstock Plus\n    antes: [1]\n    score: 1\n";

        Assert.True(
            MotelyTopSeedSink.TryRewriteAndValidate(doc, NewSeeds, out var updated, out var err),
            err
        );
        Assert.True(JamlConfigLoader.TryLoad(updated, out var cfg, out _));
        Assert.Equal(["OLDAAAAA", .. NewSeeds], cfg!.Seeds);
        // The clause that followed the seeds block must survive untouched.
        Assert.Contains("Overstock Plus", updated);
    }

    [Fact]
    public void Rewrite_NormalizesSeeds()
    {
        var doc = "name: t\nseeds: []\n";

        // lowercase -> upper, '0' -> 'O', surrounding whitespace trimmed.
        Assert.True(
            MotelyTopSeedSink.TryRewriteAndValidate(
                doc,
                ["aaaa0aaa", "  bbbbbbbb  "],
                out var updated,
                out var err
            ),
            err
        );
        Assert.True(JamlConfigLoader.TryLoad(updated, out var cfg, out _));
        Assert.Equal(["AAAAOAAA", "BBBBBBBB"], cfg!.Seeds);
    }

    [Fact]
    public void Rewrite_EmptyList_PreservesExistingSeeds()
    {
        var doc = "name: t\nseeds:\n  - OLDAAAAA\n";

        Assert.True(MotelyTopSeedSink.TryRewriteAndValidate(doc, [], out var updated, out _));
        Assert.True(JamlConfigLoader.TryLoad(updated, out var cfg, out _));
        Assert.Equal(["OLDAAAAA"], cfg!.Seeds);
    }

    [Fact]
    public void Rewrite_EmptyList_NoExisting_EmitsEmptySeeds()
    {
        var doc = "name: t\nseeds: []\n";

        Assert.True(MotelyTopSeedSink.TryRewriteAndValidate(doc, [], out var updated, out _));
        Assert.Contains("seeds: []", updated);
        Assert.True(JamlConfigLoader.TryLoad(updated, out var cfg, out _));
        Assert.Empty(cfg!.Seeds);
    }

    [Fact]
    public void Rewrite_PreservesCrlf()
    {
        var doc = "name: t\r\nseeds:\r\n  - OLDAAAAA\r\n";

        MotelyTopSeedSink.TryRewriteAndValidate(doc, NewSeeds, out var updated, out _);

        Assert.Contains("\r\n", updated);
        // No bare LFs left once the CRLFs are stripped.
        Assert.DoesNotContain("\n", updated.Replace("\r\n", "", StringComparison.Ordinal));
    }

    [Fact]
    public void Rewrite_PreservesLf()
    {
        var doc = "name: t\nseeds:\n  - OLDAAAAA\n";

        MotelyTopSeedSink.TryRewriteAndValidate(doc, NewSeeds, out var updated, out _);

        Assert.DoesNotContain("\r", updated);
    }

    [Fact]
    public void Rewrite_IsIdempotent()
    {
        var doc = "name: t\nseeds: []\n";

        MotelyTopSeedSink.TryRewriteAndValidate(doc, NewSeeds, out var once, out _);
        MotelyTopSeedSink.TryRewriteAndValidate(once, NewSeeds, out var twice, out _);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void EndToEnd_CollectorOutputWritesBackExactly()
    {
        // The exact production data flow minus IO: collect best -> rewrite -> reload.
        var c = new MotelyTopSeedSink.Collector(3);
        c.Consider("AAAAAAAA", 10);
        c.Consider("BBBBBBBB", 30);
        c.Consider("CCCCCCCC", 20);
        c.Consider("DDDDDDDD", 5);
        var best = c.GetSeeds(); // BBBB.., CCCC.., AAAA.. (top 3, score desc)

        var doc = "name: t\nseeds: []\n";
        Assert.True(
            MotelyTopSeedSink.TryRewriteAndValidate(doc, best, out var updated, out var err),
            err
        );
        Assert.True(JamlConfigLoader.TryLoad(updated, out var cfg, out _));
        Assert.Equal(best, cfg!.Seeds);
    }
}
