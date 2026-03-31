using System.Diagnostics;
using System.Reflection;
using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely;
using Motely.Analysis;
using Motely.Filters;

[assembly: JSExport(typeof(Motely.BrowserWasm.IMotelyProgram))]
[assembly: JSImport(typeof(Motely.BrowserWasm.IMotelyProgramCallbacks))]

namespace Motely.BrowserWasm;

/// <summary>JS → C# exports (called from browser)</summary>
public interface IMotelyProgram
{
    string GetVersion();
    string? ValidateJaml(string jamlContent);
    MotelySeedAnalysis AnalyzeSeed(string seed, MotelyDeck deck, MotelyStake stake);
    MotelySeedRouterDesc LoadSeed(string seed, MotelyDeck deck, MotelyStake stake);

    // ── Search ──────────────────────────────────────────────
    void StartSearch(string jamlContent, int threadCount, int batchCharCount, long startBatch, long endBatch);
    void StartSeedListSearch(string jamlContent, string seedsCsv);
    void StopSearch();
}

/// <summary>C# → JS imports (events pushed to browser)</summary>
public interface IMotelyProgramCallbacks
{
    void OnProgress(long seedsSearched, long matchingSeeds, long elapsedMs);
    void OnResult(string seed, int score);
    void OnComplete(string status, long seedsSearched, long matchingSeeds);
}

public class MotelyProgram : IMotelyProgram
{
    private readonly IMotelyProgramCallbacks _callbacks;
    private CancellationTokenSource? _cts;
    private IMotelySearch? _activeSearch;

    public MotelyProgram(IMotelyProgramCallbacks callbacks)
    {
        _callbacks = callbacks;
    }

    public string GetVersion()
    {
        var asm = typeof(MotelyProgram).Assembly;
        return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "0.0.0";
    }

    public string? ValidateJaml(string jamlContent)
    {
        return JamlConfigLoader.TryLoad(jamlContent, out _, out var error)
            ? null
            : error ?? "JAML validation failed.";
    }

    public MotelySeedAnalysis AnalyzeSeed(string seed, MotelyDeck deck, MotelyStake stake)
    {
        return MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(seed, deck, stake));
    }

    public MotelySeedRouterDesc LoadSeed(string seed, MotelyDeck deck, MotelyStake stake)
    {
        return new MotelySeedRouterDesc(seed, deck, stake);
    }

    // ── Search ──────────────────────────────────────────────

    public void StartSearch(string jamlContent, int threadCount, int batchCharCount, long startBatch, long endBatch)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new InvalidOperationException($"Invalid JAML: {error}");

        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(Math.Max(1, threadCount))
            .WithBatchCharacterCount(batchCharCount)
            .WithSequentialSearch();

        if (startBatch > 0) settings.WithStartBatchIndex(startBatch);
        if (endBatch > 0) settings.WithEndBatchIndex(endBatch);

        WireCallbacksAndRun(settings, plan.ShouldClauseCount > 0);
    }

    public void StartSeedListSearch(string jamlContent, string seedsCsv)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new InvalidOperationException($"Invalid JAML: {error}");

        var seeds = seedsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithListSearch(seeds, seeds.Length);

        WireCallbacksAndRun(settings, plan.ShouldClauseCount > 0);
    }

    public void StopSearch()
    {
        _cts?.Cancel();
        _activeSearch?.Cancel();
    }

    private void WireCallbacksAndRun(IMotelySearchSettings settings, bool hasScoring)
    {
        StopSearch(); // cancel any existing search

        _cts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();

        settings.WithProgressCallback(progress =>
        {
            _callbacks.OnProgress(progress.SeedsSearched, progress.MatchingSeeds, sw.ElapsedMilliseconds);
        });

        if (hasScoring)
        {
            settings.WithScoredResultCallback(tally =>
            {
                _callbacks.OnResult(tally.Seed, tally.Score);
            });
        }
        else
        {
            settings.WithSeedMatchCallback(seed =>
            {
                _callbacks.OnResult(seed, 0);
            });
        }

        var search = settings.CreateSearch();
        _activeSearch = search;
        var ct = _cts.Token;

        try
        {
            search.Start(ct);
            search.AwaitCompletion();
            _callbacks.OnComplete("completed", search.TotalSeedsSearched, search.MatchingSeeds);
        }
        catch (OperationCanceledException)
        {
            _callbacks.OnComplete("cancelled", search.TotalSeedsSearched, search.MatchingSeeds);
        }
        catch (Exception ex)
        {
            _callbacks.OnComplete($"error: {ex.Message}", search.TotalSeedsSearched, search.MatchingSeeds);
        }
        finally
        {
            sw.Stop();
            search.Dispose();
            _activeSearch = null;
        }
    }
}

public static class Program
{
    public static void Main()
    {
        new ServiceCollection()
            .AddBootsharp()
            .AddSingleton<IMotelyProgram, MotelyProgram>()
            .BuildServiceProvider()
            .RunBootsharp();
    }
}
