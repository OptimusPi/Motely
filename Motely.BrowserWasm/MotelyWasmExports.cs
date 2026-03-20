using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Executors;

namespace Motely.BrowserWasm;

/// <summary>
/// Browser WASM exports. Thin wrappers around MotelyExports (shared orchestration).
/// Each method adds [JSExport] and async Task.Run so the browser thread isn't blocked.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class MotelyWasmExports
{
    private static CancellationTokenSource? _activeCts;

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

    /// <summary>
    /// Delegates to MotelyExports.RunSearch (shared orchestration) with cancellation + async.
    /// </summary>
    private static async Task<string> RunSearch(
        string jamlContent, MotelySearchRequest request,
        Action<long, long, long> onProgress, Action<string, int> onResult)
    {
        if (_activeCts != null)
            return "error: search already running";

        var cts = new CancellationTokenSource();
        _activeCts = cts;

        try
        {
            var (status, _, _) = await Task.Run(() =>
                MotelyExports.RunSearch(jamlContent, request, onProgress, onResult, cts.Token));
            return status;
        }
        catch (OperationCanceledException)
        {
            return "cancelled";
        }
        catch (InvalidOperationException ex)
        {
            return $"error: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }
        finally
        {
            _activeCts = null;
        }
    }
}
