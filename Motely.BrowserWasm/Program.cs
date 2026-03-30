using System.Text.Json;
using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely;
using Motely.Analysis;
using Motely.BrowserWasm;
using Motely.Filters;

[assembly: JSPreferences(Space = ["^Motely\\.BrowserWasm\\.", "MotelyWasm."])]

[assembly: JSExport(typeof(IMotelyBrowserApi))]

public static partial class Program
{
    private static readonly object ScoreLock = new();
    private static CancellationTokenSource? _searchCts;
    private static Task? _searchTask;

    public static void Main()
    {
        new ServiceCollection()
            .AddBootsharp()
            .AddSingleton<IMotelyBrowserApi, MotelyBrowserApi>()
            .BuildServiceProvider()
            .RunBootsharp();
    }

    [JSEvent]
    public static partial void OnProgress(long searched, long found, long elapsedMs);

    [JSEvent]
    public static partial void OnResult(string seed, double score);

    [JSEvent]
    public static partial void OnComplete(string status, int seedsFound, double highestScore);

    [JSInvokable]
    public static string GetVersion() =>
        typeof(MotelyBrowserApi).Assembly.GetName().Version?.ToString() ?? "unknown";

    [JSInvokable]
    public static string? ValidateJaml(string jamlContent) =>
        JamlConfigLoader.TryLoad(jamlContent, out _, out var error) ? null : error;

    [JSInvokable]
    public static string AnalyzeSeed(string seed, string deck, string stake) =>
        JsonSerializer.Serialize(MotelySeedAnalyzer.AnalyzeToDto(seed, deck, stake));

    public static MotelySingleSearchContext CreateSeedInstance(string seed, string deck, string stake)
    {
        if (!Enum.TryParse<MotelyDeck>(deck, ignoreCase: true, out var deckEnum))
            throw new ArgumentException($"Invalid deck: {deck}");
        if (!Enum.TryParse<MotelyStake>(stake, ignoreCase: true, out var stakeEnum))
            throw new ArgumentException($"Invalid stake: {stake}");
        var router = new MotelySeedRouterDesc(seed, deckEnum, stakeEnum);
        return router.CreateContext();
    }

    [JSInvokable]
    public static void StartSearch(string jamlContent, int threadCount)
    {
        StopSearch();

        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out _))
        {
            OnComplete("invalid", 0, 0);
            return;
        }

        int threads = threadCount < 1 ? 1 : threadCount > 64 ? 64 : threadCount;
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        _searchTask = Task.Run(
            () =>
            {
                var count = 0;
                var highest = 0.0;

                try
                {
                    var settings = JamlSearchBuilder
                        .CreateSettings(config)
                        .WithDeck(config.Deck)
                        .WithStake(config.Stake)
                        .WithThreadCount(threads)
                        .WithBatchCharacterCount(3)
                        .WithSequentialSearch();

                    settings.WithProgressCallback(p =>
                        OnProgress(
                            p.SeedsSearched,
                            p.MatchingSeeds,
                            (long)p.ElapsedTime.TotalMilliseconds
                        )
                    );

                    settings.WithSeedMatchCallback(seed =>
                    {
                        lock (ScoreLock)
                            count++;
                        OnResult(seed, 0);
                    });

                    settings.WithScoredResultCallback(tally =>
                    {
                        lock (ScoreLock)
                        {
                            count++;
                            if (tally.Score > highest)
                                highest = tally.Score;
                        }

                        OnResult(tally.Seed, tally.Score);
                    });

                    using var search = settings.Start(ct);
                    search.AwaitCompletion();

                    int doneCount;
                    double best;
                    lock (ScoreLock)
                    {
                        doneCount = count;
                        best = highest;
                    }

                    OnComplete(
                        ct.IsCancellationRequested ? "cancelled" : "complete",
                        doneCount,
                        best
                    );
                }
                catch (OperationCanceledException)
                {
                    int doneCount;
                    double best;
                    lock (ScoreLock)
                    {
                        doneCount = count;
                        best = highest;
                    }

                    OnComplete("cancelled", doneCount, best);
                }
                catch
                {
                    OnComplete("error", 0, 0);
                }
            },
            ct
        );
    }

    [JSInvokable]
    public static void StopSearch()
    {
        try
        {
            _searchCts?.Cancel();
        }
        catch
        {
            // ignore
        }
    }
}
