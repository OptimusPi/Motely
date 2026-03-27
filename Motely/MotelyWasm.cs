using System.Diagnostics;
using System.Text.Json;
using Motely.Analysis;
using Motely.Filters;

namespace Motely.Core;

/// <summary>Interop surface exported to JavaScript (see <c>[assembly: JSExport(typeof(IMotelyWasm))]</c>).</summary>
public interface IMotelyWasm
{
    // ── Search ──
    string RunSearch(string jamlContent, int threadCount, int batchCharCount, int startBatch, int endBatch);
    string RunKeywordSearch(string jamlContent, int threadCount, string[] keywords, string? padding);
    string RunSeedListSearch(string jamlContent, int threadCount, string[] seeds);
    string RunRandomSearch(string jamlContent, int threadCount, int count);
    string RunPalindromeSearch(string jamlContent, int threadCount);

    // ── Analysis ──
    string AnalyzeSeed(string seed, string deck, string stake);

    // ── Shop streaming (returns interop instance) ──
    IMotelyShopStream CreateShopStream(string seed, string deck, string stake);

    string GetVersion();
}

/// <summary>Bootsharp interop instance — JS gets a live proxy, calls methods directly, GC cleans up.</summary>
public interface IMotelyShopStream : IDisposable
{
    void BeginAnte(int ante);
    string GetNextShopItemJson();
}

public sealed class MotelyWasm(IMotelyUI ui) : IMotelyWasm
{
    // ── Search ──────────────────────────────────────────────────────────────

    public string RunSearch(string jamlContent, int threadCount, int batchCharCount, int startBatch, int endBatch)
    {
        var settings = CreateSearchSettings(jamlContent);
        settings
            .WithThreadCount(Math.Max(1, threadCount))
            .WithBatchCharacterCount(batchCharCount)
            .WithSequentialSearch()
            .WithStartBatchIndex(startBatch)
            .WithEndBatchIndex(endBatch);
        return ExecuteSearch(settings);
    }

    public string RunKeywordSearch(string jamlContent, int threadCount, string[] keywords, string? padding)
    {
        var settings = CreateSearchSettings(jamlContent);
        char[]? paddingChars = string.IsNullOrEmpty(padding) ? null : padding!.ToCharArray();
        var keywordSeeds = Motely.GeneratePaddedSeedsForKeywords(keywords, paddingChars);
        var count = (int)Motely.GetPaddedSeedCountForKeywords(keywords, paddingChars);
        settings
            .WithThreadCount(Math.Max(1, threadCount))
            .WithProviderSearch(new MotelySeedListProvider(keywordSeeds, count));
        return ExecuteSearch(settings);
    }

    public string RunSeedListSearch(string jamlContent, int threadCount, string[] seeds)
    {
        var settings = CreateSearchSettings(jamlContent);
        settings
            .WithThreadCount(Math.Max(1, threadCount))
            .WithListSearch(seeds, seeds.Length);
        return ExecuteSearch(settings);
    }

    public string RunRandomSearch(string jamlContent, int threadCount, int count)
    {
        var settings = CreateSearchSettings(jamlContent);
        settings
            .WithThreadCount(Math.Max(1, threadCount))
            .WithRandomSearch(count);
        return ExecuteSearch(settings);
    }

    public string RunPalindromeSearch(string jamlContent, int threadCount)
    {
        var settings = CreateSearchSettings(jamlContent);
        settings
            .WithThreadCount(Math.Max(1, threadCount))
            .WithPalindromeSearch();
        return ExecuteSearch(settings);
    }

    private IMotelySearchSettings CreateSearchSettings(string jamlContent)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new ArgumentException($"Invalid JAML: {error}");

        var plan = JamlSearchBuilder.CreatePlan(config);
        return plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake);
    }

    private string ExecuteSearch(IMotelySearchSettings settings)
    {
        var sw = Stopwatch.StartNew();
        long found = 0;
        double highestScore = 0;

        settings.WithProgressCallback(p =>
            ui.NotifyProgress(p.SeedsSearched, p.MatchingSeeds, (long)p.ElapsedTime.TotalMilliseconds));

        settings.WithSeedMatchCallback(seed =>
        {
            Interlocked.Increment(ref found);
            ui.NotifyResult(seed, 0);
        });

        settings.WithScoredResultCallback(tally =>
        {
            Interlocked.Increment(ref found);
            double score = tally.Score;
            if (score > highestScore) highestScore = score;
            ui.NotifyResult(tally.Seed, score);
        });

        using var search = settings.Start();
        search.AwaitCompletion();
        sw.Stop();

        var status = search.IsCompleted ? "COMPLETED" : "STOPPED";
        ui.NotifyComplete(status, (int)search.MatchingSeeds, highestScore);

        return $"{status}: {search.TotalSeedsSearched:N0} searched, {search.MatchingSeeds} matched in {sw.Elapsed.TotalSeconds:F1}s";
    }

    // ── Analysis ────────────────────────────────────────────────────────────

    public string AnalyzeSeed(string seed, string deck, string stake)
    {
        var dto = MotelySeedAnalyzer.AnalyzeToDto(seed, deck, stake);
        return JsonSerializer.Serialize(dto);
    }

    // ── Shop streaming ──────────────────────────────────────────────────────

    public IMotelyShopStream CreateShopStream(string seed, string deck, string stake)
    {
        var deckE = Enum.Parse<MotelyDeck>(deck, ignoreCase: true);
        var stakeE = Enum.Parse<MotelyStake>(stake, ignoreCase: true);
        return new MotelyShopStreamImpl(seed.Trim(), deckE, stakeE);
    }

    public string GetVersion() => MotelyBuildVersion.For(typeof(MotelyWasm).Assembly);
}

internal sealed class MotelyShopStreamImpl(string seed, MotelyDeck deck, MotelyStake stake)
    : IMotelyShopStream
{
    private readonly MotelySeedRouterDesc _router = new(seed, deck, stake);
    private MotelySingleShopItemStream _stream;
    private bool _started;

    public void BeginAnte(int ante)
    {
        var ctx = _router.CreateContext();
        _stream = ctx.CreateShopItemStream(ante);
        _started = true;
    }

    public string GetNextShopItemJson()
    {
        if (!_started)
            throw new InvalidOperationException("Call BeginAnte first.");

        var ctx = _router.CreateContext();
        var item = ctx.GetNextShopItem(ref _stream);
        return JsonSerializer.Serialize(new
        {
            id = (int)item.Type,
            value = item.Value,
            type = item.Type.ToString(),
            name = FormatUtils.FormatItem(item),
        });
    }

    public void Dispose() => _router.Dispose();
}
