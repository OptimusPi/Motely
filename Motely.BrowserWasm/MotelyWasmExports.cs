using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;

namespace Motely.BrowserWasm;

[SupportedOSPlatform("browser")]
public static partial class MotelyWasmExports
{
    private static CancellationTokenSource? _activeCts;
    private static IMotelySearch? _activeSearch;

    // ── SEARCH (sequential) ──

    [JSExport]
    public static Task<string> StartJamlSearch(
        string jamlContent, int threadCount, int batchCharCount,
        int startBatch, int endBatch,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<long, long, long> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, int> onResult)
    {
        return RunSearch(jamlContent, new MotelySearchRequest
        {
            ThreadCount = ResolveThreads(threadCount),
            BatchCharCount = ResolveBatch(batchCharCount),
            StartBatch = startBatch >= 0 ? startBatch : null,
            EndBatch = endBatch > 0 ? endBatch : null,
        }, onProgress, onResult);
    }

    // ── SEARCH (seed list / verify) ──

    [JSExport]
    public static Task<string> StartSeedListSearch(
        string jamlContent, int threadCount, int batchCharCount,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] seeds,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<long, long, long> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, int> onResult)
    {
        return RunSearch(jamlContent, new MotelySearchRequest
        {
            ThreadCount = ResolveThreads(threadCount),
            BatchCharCount = ResolveBatch(batchCharCount),
            Seeds = seeds,
        }, onProgress, onResult);
    }

    // ── SEARCH (keyword) ──

    [JSExport]
    public static Task<string> StartKeywordSearch(
        string jamlContent, int threadCount, int batchCharCount,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] keywords, string padding,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<long, long, long> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, int> onResult)
    {
        return RunSearch(jamlContent, new MotelySearchRequest
        {
            ThreadCount = ResolveThreads(threadCount),
            BatchCharCount = ResolveBatch(batchCharCount),
            Keywords = keywords,
            Padding = string.IsNullOrEmpty(padding) ? null : padding,
        }, onProgress, onResult);
    }

    // ── SEARCH (random) ──

    [JSExport]
    public static Task<string> StartRandomSearch(
        string jamlContent, int threadCount, int batchCharCount, int count,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<long, long, long> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, int> onResult)
    {
        return RunSearch(jamlContent, new MotelySearchRequest
        {
            ThreadCount = ResolveThreads(threadCount),
            BatchCharCount = ResolveBatch(batchCharCount),
            RandomSeeds = count > 0 ? count : 1000,
        }, onProgress, onResult);
    }

    // ── SEARCH (palindrome) ──

    [JSExport]
    public static Task<string> StartPalindromeSearch(
        string jamlContent, int threadCount, int batchCharCount,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<long, long, long> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, int> onResult)
    {
        return RunSearch(jamlContent, new MotelySearchRequest
        {
            ThreadCount = ResolveThreads(threadCount),
            BatchCharCount = ResolveBatch(batchCharCount),
            Palindrome = true,
        }, onProgress, onResult);
    }

    [JSExport]
    public static Task StopSearch()
    {
        try { _activeSearch?.Cancel(); } catch { }
        try { _activeCts?.Cancel(); } catch { }
        return Task.CompletedTask;
    }

    // ── ANALYZE ──

    [JSExport]
    public static Task<string> AnalyzeSeed(string seed, string deck, string stake)
    {
        try
        {
            var dto = MotelyExports.AnalyzeSeed(seed, deck, stake);
            return Task.FromResult(JsonSerializer.Serialize(dto, AnalysisJsonContext.Default.SeedAnalysisDto));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(
                new SeedAnalysisDto { Seed = seed, Deck = deck, Stake = stake, Error = ex.Message },
                AnalysisJsonContext.Default.SeedAnalysisDto));
        }
    }

    // ── SIMPLE GETTERS ──

    [JSExport]
    public static Task<string> GetVersion() => Task.FromResult(MotelyExports.GetVersion(typeof(MotelyCore).Assembly));

    [JSExport]
    public static Task<bool> IsSimdEnabled() => Task.FromResult(MotelyExports.IsSimdEnabled());

    [JSExport]
    public static Task<int> GetProcessorCount() => Task.FromResult(MotelyExports.GetProcessorCount());

    [JSExport]
    public static Task<bool> ValidateJaml(string jamlContent) => Task.FromResult(MotelyExports.ValidateJaml(jamlContent));

    [JSExport]
    public static Task<string> ValidateJamlWithError(string jamlContent) => Task.FromResult(MotelyExports.ValidateJamlWithError(jamlContent));

    // ── Internals ──

    private static int ResolveThreads(int t) => t > 0 ? t : Environment.ProcessorCount;
    private static int ResolveBatch(int b) => b is >= 1 and <= 7 ? b : 4;

    private static async Task<string> RunSearch(
        string jamlContent, MotelySearchRequest request,
        Action<long, long, long> onProgress, Action<string, int> onResult)
    {
        if (_activeSearch != null)
            return "error: search already running";

        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var loadError))
            return $"error: {loadError}";

        if (!config.HasAnyClauses)
            return "error: no clauses in JAML";

        var (plan, _, prepareError) = MotelySearchOrchestrator.PrepareSearch(config, request);
        if (prepareError != null || plan == null)
            return $"error: {prepareError ?? "Search could not be prepared."}";

        var settings = plan.Settings;

        settings.WithSeedMatchCallback(line =>
        {
            int comma = line.IndexOf(',');
            if (comma < 0) { onResult(line, 0); return; }
            var seed = line[..comma];
            int secondComma = line.IndexOf(',', comma + 1);
            var scoreSpan = secondComma >= 0
                ? line.AsSpan(comma + 1, secondComma - comma - 1)
                : line.AsSpan(comma + 1);
            int.TryParse(scoreSpan, out int score);
            onResult(seed, score);
        });

        settings.WithProgressCallback(p =>
            onProgress(p.SeedsSearched, p.MatchingSeeds, (long)p.ElapsedTime.TotalMilliseconds));

        var cts = new CancellationTokenSource();
        _activeCts = cts;

        try
        {
            using var search = settings.CreateSearch();
            _activeSearch = search;
            await Task.Run(() => search.Start(cts.Token));
            return cts.Token.IsCancellationRequested ? "cancelled" : "ok";
        }
        catch (OperationCanceledException)
        {
            return "cancelled";
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }
        finally
        {
            _activeSearch = null;
            _activeCts = null;
        }
    }
}
