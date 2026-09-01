using Motely.Filters.Jaml;

namespace Motely.Tests;

/// <summary>
/// Pins <see cref="JamlSpeedProbe"/>: it really searches one 35⁴ batch on one thread, reports the
/// exact seed count it visited, scales by the caller's thread count, and describes itself in the
/// report's own shape. Motely is the calculator — the probe exists so no consumer ever has to print
/// "unknown until a run has been timed on this machine" when timing a run takes under a second.
/// </summary>
public sealed class JamlSpeedProbeTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private const string Jaml = """
        name: probe
        deck: Red
        stake: White
        must:
          - joker: Blueprint
            antes: [1]
        """;

    private static JamlConfig Load()
    {
        Assert.True(JamlConfigLoader.TryLoad(Jaml, out var config, out var error), error);
        return config!;
    }

    [Fact]
    public void Measure_VisitsExactlyOneProbeBatch_AndScalesByThreads()
    {
        var result = JamlSpeedProbe.Measure(Load(), 0, MotelyDeck.Red, MotelyStake.White, threads: 4);

        Assert.NotNull(result);
        var r = result.Value;
        output.WriteLine(r.Describe());

        Assert.Equal((long)Math.Pow(35, JamlSpeedProbe.ProbeBatchCharacterCount), r.SeedsSearched);
        Assert.True(r.ElapsedSeconds > 0, "the timed batch must take measurable time");
        Assert.Equal(4, r.Threads);
        Assert.True(r.PerThread > 0);
        Assert.Equal(r.PerThread * 4, r.Projected, precision: 6);
    }

    [Fact]
    public async Task MeasureAsync_AgreesWithSync()
    {
        var result = await JamlSpeedProbe.MeasureAsync(Load(), 0, MotelyDeck.Red, MotelyStake.White, threads: 1);

        Assert.NotNull(result);
        Assert.Equal((long)Math.Pow(35, JamlSpeedProbe.ProbeBatchCharacterCount), result.Value.SeedsSearched);
        Assert.Equal(result.Value.PerThread, result.Value.Projected);
    }

    [Fact]
    public void Describe_IsOneReportStyleLine()
    {
        var r = new JamlSpeedProbe.Result(SeedsSearched: 1_500_625, ElapsedSeconds: 0.3, Threads: 16);
        string line = r.Describe();

        Assert.StartsWith("  Probe:", line);
        Assert.DoesNotContain('\n', line);
        Assert.Contains("on 1 thread", line);
        Assert.Contains("× 16", line);
        Assert.Contains("/thread", line);
        Assert.Contains(JamlRarityReport.Speed(r.Projected), line);
    }

    [Fact]
    public void Cancelled_ReturnsNull_NeverThrows()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = JamlSpeedProbe.Measure(Load(), 0, MotelyDeck.Red, MotelyStake.White, threads: 2, cts.Token);

        Assert.Null(result);
    }

    [Fact]
    public void ThreadCount_IsClampedToAtLeastOne()
    {
        var r = new JamlSpeedProbe.Result(1_500_625, 0.3, Threads: 16);
        Assert.Equal(r.PerThread * 16, r.Projected, precision: 6);

        var result = JamlSpeedProbe.Measure(Load(), 0, MotelyDeck.Red, MotelyStake.White, threads: 0);
        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Threads);
    }
}
