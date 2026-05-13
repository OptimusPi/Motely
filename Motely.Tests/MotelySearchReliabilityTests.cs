using Motely.Filters;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Reliability regressions for <see cref="MotelySearch{TBaseFilter}"/>.
/// Both tests covered HIGH-severity items in ISSUES.md:
///   1. Single-thread <see cref="MotelySearch{TBaseFilter}.RunSearchAsync"/> used to run the
///      worker body synchronously on the caller, so awaiting it on the same context deadlocked.
///   2. Multi-thread workers used to swallow exceptions silently — the completion source
///      stayed unset and callers hung forever.
/// </summary>
public sealed class MotelySearchReliabilityTests
{
    /// <summary>Always-pass base filter so the worker actually runs for the seed list.</summary>
    private readonly struct PassFilterDesc : IMotelySeedFilterDesc<PassFilterDesc.PassFilter>
    {
        public PassFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new();

        public readonly struct PassFilter : IMotelySeedFilter
        {
            public readonly VectorMask Filter(ref MotelyVectorSearchContext _) =>
                VectorMask.AllBitsSet;
        }
    }

    /// <summary>Base filter that throws inside the SIMD loop.</summary>
    private readonly struct ThrowingFilterDesc : IMotelySeedFilterDesc<ThrowingFilterDesc.ThrowingFilter>
    {
        public ThrowingFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new();

        public readonly struct ThrowingFilter : IMotelySeedFilter
        {
            public readonly VectorMask Filter(ref MotelyVectorSearchContext _) =>
                throw new InvalidOperationException("boom from worker");
        }
    }

    [Fact]
    public async Task RunSearchAsync_SingleThread_DoesNotDeadlockAwaitOnSameContext()
    {
        var settings = new MotelySearchSettings<PassFilterDesc.PassFilter>(new PassFilterDesc())
            .WithListSearch(["AAAAAAAA"], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.CreateSearch();

        // The bug under fix: when totalWorkers == 1 the worker body used to run
        // synchronously inside Start(), meaning RunSearchAsync()'s returned Task
        // only completed after the search had already drained on the caller's
        // thread — fatal for `await search.RunSearchAsync()` on a sync ctx.
        // A bounded wait proves the Task yields and completes off-thread.
        var run = search.RunSearchAsync();
        var winner = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(run, winner);
        await run; // no exception → completed cleanly
        Assert.True(search.IsCompleted);
    }

    [Fact]
    public async Task RunSearchAsync_SingleThread_SurfacesWorkerException()
    {
        var settings = new MotelySearchSettings<ThrowingFilterDesc.ThrowingFilter>(new ThrowingFilterDesc())
            .WithListSearch(["AAAAAAAA"], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.CreateSearch();
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => search.RunSearchAsync());
        Assert.Contains("boom from worker", FlattenMessages(ex));
    }

    [Fact]
    public async Task RunSearchAsync_MultiThread_SurfacesWorkerException()
    {
        // Many lanes / multiple workers so the throw lands on a worker thread,
        // not the caller. Previously this would set _completionSource to nothing
        // and the await would hang forever.
        var seeds = Enumerable.Range(0, 1024).Select(i => $"S{i:D7}").ToArray();
        var settings = new MotelySearchSettings<ThrowingFilterDesc.ThrowingFilter>(new ThrowingFilterDesc())
            .WithListSearch(seeds, seeds.Length)
            .WithThreadCount(Math.Max(2, Environment.ProcessorCount))
            .WithQuietMode(true);

        using var search = settings.CreateSearch();
        var run = search.RunSearchAsync();
        var winner = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(run, winner); // didn't hang
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => run);
        Assert.Contains("boom from worker", FlattenMessages(ex));
    }

    [Fact]
    public void RunSearchUntilCompletion_MultiThread_SurfacesWorkerException()
    {
        var seeds = Enumerable.Range(0, 1024).Select(i => $"S{i:D7}").ToArray();
        var settings = new MotelySearchSettings<ThrowingFilterDesc.ThrowingFilter>(new ThrowingFilterDesc())
            .WithListSearch(seeds, seeds.Length)
            .WithThreadCount(Math.Max(2, Environment.ProcessorCount))
            .WithQuietMode(true);

        using var search = settings.CreateSearch();
        // Sync surface: previously the throw was lost and SignalSearchCompleted()
        // marked the search "clean". Now the first error rethrows.
        var ex = Assert.ThrowsAny<Exception>(() => search.RunSearchUntilCompletion());
        Assert.Contains("boom from worker", FlattenMessages(ex));
    }

    private static string FlattenMessages(Exception ex)
    {
        var msgs = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
            msgs.Add(current.Message);
        if (ex is AggregateException agg)
            foreach (var inner in agg.Flatten().InnerExceptions)
                msgs.Add(inner.Message);
        return string.Join(" | ", msgs);
    }
}
