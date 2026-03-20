using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Intrinsics;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;

namespace Motely.BrowserWasm;

/// <summary>
/// Static [JSExport] dispatch layer. Instance methods route through an int handle.
/// JS callers: createInstance() → id, then pass id to search/analyze/stop/destroy.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class MotelyWasmExports
{
    // ── Instance lifecycle ──

    [JSExport]
    public static int CreateInstance() => MotelyInstance.Create();

    [JSExport]
    public static void DestroyInstance(int id) => MotelyInstance.Destroy(id);

    // ── Search (all instance-scoped) ──

    [JSExport]
    public static Task<string> StartJamlSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount,
        int startBatch, int endBatch,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<long, long, long> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, int> onResult)
        => RunSearch(instanceId, jamlContent, new MotelySearchRequest
        {
            ThreadCount = ResolveThreads(threadCount),
            BatchCharCount = ResolveBatch(batchCharCount),
            StartBatch = startBatch >= 0 ? startBatch : null,
            EndBatch = endBatch > 0 ? endBatch : null,
        }, onProgress, onResult);

    [JSExport]
    public static Task<string> StartSeedListSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] seeds,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<long, long, long> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, int> onResult)
        => RunSearch(instanceId, jamlContent, new MotelySearchRequest
        {
            ThreadCount = ResolveThreads(threadCount),
            BatchCharCount = ResolveBatch(batchCharCount),
            Seeds = seeds,
        }, onProgress, onResult);

    [JSExport]
    public static Task<string> StartKeywordSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] keywords, string padding,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<long, long, long> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, int> onResult)
        => RunSearch(instanceId, jamlContent, new MotelySearchRequest
        {
            ThreadCount = ResolveThreads(threadCount),
            BatchCharCount = ResolveBatch(batchCharCount),
            Keywords = keywords,
            Padding = string.IsNullOrEmpty(padding) ? null : padding,
        }, onProgress, onResult);

    [JSExport]
    public static Task<string> StartRandomSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount, int count,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<long, long, long> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, int> onResult)
        => RunSearch(instanceId, jamlContent, new MotelySearchRequest
        {
            ThreadCount = ResolveThreads(threadCount),
            BatchCharCount = ResolveBatch(batchCharCount),
            RandomSeeds = count > 0 ? count : 1000,
        }, onProgress, onResult);

    [JSExport]
    public static Task<string> StartPalindromeSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<long, long, long> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, int> onResult)
        => RunSearch(instanceId, jamlContent, new MotelySearchRequest
        {
            ThreadCount = ResolveThreads(threadCount),
            BatchCharCount = ResolveBatch(batchCharCount),
            Palindrome = true,
        }, onProgress, onResult);

    [JSExport]
    public static Task StopSearch(int instanceId)
    {
        MotelyInstance.Get(instanceId).CancelSearch();
        return Task.CompletedTask;
    }

    // ── Analyze (instance-scoped for future streaming) ──

    [JSExport]
    public static Task<string> AnalyzeSeed(int instanceId, string seed, string deck, string stake)
    {
        // instanceId reserved for future per-instance state (streaming analysis, caching)
        try
        {
            var dto = MotelySeedAnalyzer.AnalyzeToDto(seed, deck, stake);
            return Task.FromResult(JsonSerializer.Serialize(dto, AnalysisJsonContext.Default.SeedAnalysisDto));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(
                new SeedAnalysisDto { Seed = seed, Deck = deck, Stake = stake, Error = ex.Message },
                AnalysisJsonContext.Default.SeedAnalysisDto));
        }
    }

    // ── Global (no instance needed) ──

    [JSExport]
    public static Task<string> GetVersion() => Task.FromResult(MotelyBuildVersion.For(typeof(MotelyCore).Assembly));

    [JSExport]
    public static Task<bool> IsSimdEnabled() => Task.FromResult(Vector128.IsHardwareAccelerated);

    [JSExport]
    public static Task<int> GetProcessorCount() => Task.FromResult(Environment.ProcessorCount);

    [JSExport]
    public static Task<bool> ValidateJaml(string jamlContent) => Task.FromResult(JamlConfigLoader.TryLoad(jamlContent, out _, out _));

    [JSExport]
    public static Task<string> ValidateJamlWithError(string jamlContent)
    {
        if (JamlConfigLoader.TryLoad(jamlContent, out _, out var error))
            return Task.FromResult("");
        return Task.FromResult(error ?? "Unknown validation error");
    }

    // ── Internals ──

    private static int ResolveThreads(int t) => t > 0 ? t : Environment.ProcessorCount;
    private static int ResolveBatch(int b) => b is >= 1 and <= 7 ? b : 4;

    private static async Task<string> RunSearch(
        int instanceId, string jamlContent, MotelySearchRequest request,
        Action<long, long, long> onProgress, Action<string, int> onResult)
    {
        var instance = MotelyInstance.Get(instanceId);
        if (instance.IsSearchActive)
            return "error: search already running on this instance";

        var token = instance.BeginSearch();
        try
        {
            var (status, _, _) = await Task.Run(() =>
                MotelySearchOrchestrator.RunSearch(jamlContent, request, onProgress, onResult, token));
            return status;
        }
        catch (OperationCanceledException) { return "cancelled"; }
        catch (Exception ex) { return $"error: {ex.Message}"; }
        finally { instance.EndSearch(); }
    }
}
